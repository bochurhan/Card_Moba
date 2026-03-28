using UnityEngine;
using System;

namespace CardMoba.Client.GameLogic.RoundFlow
{
    /// <summary>
    /// 客户端回合阶段计时器 —— 纯镜像，不驱动任何结算。
    /// 
    /// 职责：
    ///   1. 接收服务端推送的阶段切换消息（PhaseStartTimestampMs + Phase）
    ///   2. 在本地倒计时，驱动 UI 展示
    ///   3. 操作窗口期到期时，本地锁定玩家输入（防止乐观操作）
    ///   4. 若本地时间与服务端时间戳差距 > 阈值，自动校正倒计时
    /// 
    /// 不做的事（权威由服务端负责）：
    ///   - 不推进阶段（阶段切换由服务端推送）
    ///   - 不触发结算（结算由 RoundManager + SettlementEngine 负责）
    /// </summary>
    public class RoundPhaseTimerClient : MonoBehaviour
    {
        // ══════════════════════════════════════════════════════════
        // 配置
        // ══════════════════════════════════════════════════════════

        // ══════════════════════════════════════════════════════════
        // 运行时状态
        // ══════════════════════════════════════════════════════════

        /// <summary>当前阶段</summary>
        public RoundPhase CurrentPhase { get; private set; } = RoundPhase.RoundStartSettle;

        /// <summary>当前阶段剩余秒数（用于 UI 绑定）</summary>
        public float RemainingSeconds { get; private set; }

        /// <summary>当前阶段总时长（秒），用于进度条计算</summary>
        public float TotalSeconds { get; private set; }

        /// <summary>剩余时间比例（0~1），用于进度条</summary>
        public float RemainingRatio => TotalSeconds > 0 ? RemainingSeconds / TotalSeconds : 0f;

        /// <summary>当前是否允许玩家操作</summary>
        public bool IsOperationAllowed => RoundPhaseConfig.IsPlayerActionAllowed(CurrentPhase);

        /// <summary>当前阶段是否已超时（本地判断，最终以服务端推送为准）</summary>
        public bool IsLocallyExpired => RemainingSeconds <= 0f;

        // ── 事件 ──

        /// <summary>阶段切换事件（新阶段）</summary>
        public event Action<RoundPhase> OnPhaseChanged;

        /// <summary>
        /// 阶段切换事件（新阶段 + 总时长秒数）。
        /// RoundTimerUI 使用此事件以获取 totalSeconds 来计算进度条比例。
        /// </summary>
        public event Action<RoundPhase, float> OnPhaseChangedWithDuration;

        /// <summary>本地倒计时归零事件（操作窗口到期时触发 UI 锁定）</summary>
        public event Action OnLocalTimerExpired;

        /// <summary>
        /// 本地倒计时归零事件（别名，供 RoundTimerUI 统一使用）。
        /// 与 OnLocalTimerExpired 同步触发，语义相同。
        /// </summary>
        public event Action OnTimerExpired;

        /// <summary>每帧剩余时间更新事件（秒）</summary>
        public event Action<float> OnRemainingSecondsUpdated;

        // ── 内部 ──

        private bool _isRunning;
        private bool _expiredFired;

        // ══════════════════════════════════════════════════════════
        // Unity 生命周期
        // ══════════════════════════════════════════════════════════

        private void Update()
        {
            if (!_isRunning || IsLocallyExpired) return;

            RemainingSeconds -= Time.deltaTime;

            if (RemainingSeconds < 0f)
                RemainingSeconds = 0f;

            // 通知 UI 更新
            OnRemainingSecondsUpdated?.Invoke(RemainingSeconds);

            // 仅触发一次过期事件
            if (RemainingSeconds <= 0f && !_expiredFired)
            {
                _expiredFired = true;
                _isRunning = false;
                Debug.Log($"[RoundPhaseTimer] 阶段 {CurrentPhase} 本地倒计时归零");
                OnLocalTimerExpired?.Invoke();
                OnTimerExpired?.Invoke();
            }
        }

        private void OnDestroy()
        {
            // 清理所有事件监听，防止内存泄漏
            OnPhaseChanged = null;
            OnPhaseChangedWithDuration = null;
            OnLocalTimerExpired = null;
            OnTimerExpired = null;
            OnRemainingSecondsUpdated = null;
        }

        // ══════════════════════════════════════════════════════════
        // 公共接口（由网络层调用）
        // ══════════════════════════════════════════════════════════

        /// <summary>
        /// 接收服务端推送的阶段切换，重置本地计时器。
        /// 
        /// 由 NetworkMessageDispatch 在收到 PhaseChangeMessage 后调用。
        /// </summary>
        /// <param name="newPhase">新阶段枚举</param>
        /// <param name="serverTimestampMs">服务端记录的阶段开始 UTC 时间戳（毫秒）</param>
        public void OnServerPhaseChange(RoundPhase newPhase, long serverTimestampMs)
        {
            CurrentPhase = newPhase;

            // 从服务端时间戳计算出已经过去的时间，作为本地校正
            long nowMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            long elapsedMs = nowMs - serverTimestampMs;

            // 如果本地时间比服务端时间戳早（时钟偏差），取0
            if (elapsedMs < 0) elapsedMs = 0;

            int totalMs = RoundPhaseConfig.GetDurationMs(newPhase);
            int remainingMs = totalMs - (int)elapsedMs;
            if (remainingMs < 0) remainingMs = 0;

            TotalSeconds = totalMs / 1000f;
            RemainingSeconds = remainingMs / 1000f;
            _isRunning = remainingMs > 0;
            _expiredFired = remainingMs <= 0;

            Debug.Log($"[RoundPhaseTimer] 切换到阶段 {newPhase}，"
                + $"总时长={TotalSeconds:F0}s，剩余={RemainingSeconds:F1}s"
                + $"（服务端延迟补偿={elapsedMs}ms）");

            OnPhaseChanged?.Invoke(newPhase);
            OnPhaseChangedWithDuration?.Invoke(newPhase, TotalSeconds);

            // 如果服务端消息到达时阶段已过期（极端网络延迟），立即触发
            if (_expiredFired)
            {
                OnLocalTimerExpired?.Invoke();
                OnTimerExpired?.Invoke();
            }
        }

        /// <summary>
        /// 强制停止计时（对局结束时调用）。
        /// </summary>
        public void StopTimer()
        {
            _isRunning = false;
            RemainingSeconds = 0f;
            Debug.Log("[RoundPhaseTimer] 计时器已停止（对局结束）");
        }
    }
}
