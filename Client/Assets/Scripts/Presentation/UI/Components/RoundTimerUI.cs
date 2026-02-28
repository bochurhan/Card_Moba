using UnityEngine;
using UnityEngine.UI;
using TMPro;
using CardMoba.BattleCore.RoundStateMachine;
using CardMoba.Protocol.Enums;
using CardMoba.Client.GameLogic.RoundFlow;

namespace CardMoba.Client.Presentation.UI.Components
{
    /// <summary>
    /// 回合计时条 UI 组件。
    /// 
    /// 职责：
    ///   - 显示当前阶段名称（操作窗口期 / 结算中... 等）
    ///   - 显示倒计时数字和进度条
    ///   - 进度条颜色随时间动态变化（绿→黄→红）
    ///   - 计时归零后显示"等待结算..."遮罩
    /// 
    /// 使用方式：
    ///   1. 挂载到战斗场景 Canvas 上的 TimerPanel 节点
    ///   2. 在 Inspector 中绑定子节点引用
    ///   3. 由 BattleUIManager 调用 BindTimer / ShowPhase
    /// </summary>
    public class RoundTimerUI : MonoBehaviour
    {
        // ══════════════════════════════════════
        // Inspector 引用
        // ══════════════════════════════════════

        [Header("── 阶段名称 ──")]
        [SerializeField] private TextMeshProUGUI _phaseNameText;

        [Header("── 倒计时 ──")]
        [SerializeField] private TextMeshProUGUI _timerText;

        [Header("── 进度条 ──")]
        [SerializeField] private Slider _timerBar;
        [SerializeField] private Image _timerBarFill;

        [Header("── 等待结算遮罩 ──")]
        [SerializeField] private GameObject _waitingOverlay;
        [SerializeField] private TextMeshProUGUI _waitingText;

        [Header("── 颜色配置 ──")]
        [SerializeField] private Color _colorSafe   = new Color(0.2f, 0.8f, 0.3f);   // 绿色 >50%
        [SerializeField] private Color _colorWarn   = new Color(1.0f, 0.8f, 0.1f);   // 黄色 >20%
        [SerializeField] private Color _colorDanger = new Color(0.9f, 0.2f, 0.2f);   // 红色 ≤20%

        // ══════════════════════════════════════
        // 内部状态
        // ══════════════════════════════════════

        /// <summary>绑定的客户端计时器（由 BattleUIManager 注入）</summary>
        private RoundPhaseTimerClient _timer;

        /// <summary>当前阶段持续总秒数（用于计算进度条比例）</summary>
        private float _totalSeconds;

        /// <summary>当前是否正在倒计时</summary>
        private bool _isRunning;

        // ══════════════════════════════════════
        // 公开接口
        // ══════════════════════════════════════

        /// <summary>
        /// 绑定计时器数据源，同时订阅事件。
        /// 由 BattleUIManager 在 Awake 时调用。
        /// </summary>
        /// <param name="timer">GameLogic 层的 RoundPhaseTimerClient 实例</param>
        public void BindTimer(RoundPhaseTimerClient timer)
        {
            // 先解绑旧的计时器（防止多次绑定时重复注册）
            if (_timer != null)
            {
                _timer.OnPhaseChangedWithDuration -= HandlePhaseChanged;
                _timer.OnTimerExpired             -= HandleTimerExpired;
            }

            _timer = timer;

            if (_timer != null)
            {
                _timer.OnPhaseChangedWithDuration += HandlePhaseChanged;
                _timer.OnTimerExpired             += HandleTimerExpired;
            }
        }

        /// <summary>
        /// 直接显示非计时阶段文字（结算中 / 回合开始 等）。
        /// 此方法会隐藏计时条，只显示纯文字提示。
        /// </summary>
        /// <param name="phaseLabel">显示的文字，如"结算中..."</param>
        public void ShowStaticPhase(string phaseLabel)
        {
            _isRunning = false;

            if (_phaseNameText != null)
                _phaseNameText.text = phaseLabel;

            if (_timerText != null)
                _timerText.text = "";

            if (_timerBar != null)
                _timerBar.gameObject.SetActive(false);

            SetWaiting(false);
        }

        // ══════════════════════════════════════
        // Unity 生命周期
        // ══════════════════════════════════════

        private void Awake()
        {
            // 初始隐藏遮罩
            SetWaiting(false);
        }

        private void Update()
        {
            if (!_isRunning || _timer == null) return;

            // ── 每帧更新倒计时数字 ──
            float remaining = _timer.RemainingSeconds;
            if (remaining < 0f) remaining = 0f;

            if (_timerText != null)
                _timerText.text = Mathf.CeilToInt(remaining).ToString() + "s";

            // ── 更新进度条 ──
            if (_timerBar != null && _totalSeconds > 0f)
            {
                float ratio = remaining / _totalSeconds;
                _timerBar.value = ratio;
                UpdateBarColor(ratio);
            }
        }

        private void OnDestroy()
        {
            // 清理事件订阅，防止空引用
            if (_timer != null)
            {
                _timer.OnPhaseChangedWithDuration -= HandlePhaseChanged;
                _timer.OnTimerExpired             -= HandleTimerExpired;
            }
        }

        // ══════════════════════════════════════
        // 事件处理
        // ══════════════════════════════════════

        /// <summary>
        /// 阶段切换时：刷新标题文字、重置进度条，开始倒计时显示。
        /// </summary>
        private void HandlePhaseChanged(RoundPhase phase, float totalSeconds)
        {
            _totalSeconds = totalSeconds;
            _isRunning = true;

            // 显示阶段名称
            if (_phaseNameText != null)
                _phaseNameText.text = GetPhaseDisplayName(phase);

            // 进度条重置为满值
            if (_timerBar != null)
            {
                _timerBar.gameObject.SetActive(true);
                _timerBar.minValue = 0f;
                _timerBar.maxValue = 1f;
                _timerBar.value = 1f;
            }

            UpdateBarColor(1f);
            SetWaiting(false);

            Debug.Log($"[RoundTimerUI] 阶段切换 → {phase}, 持续 {totalSeconds}s");
        }

        /// <summary>
        /// 计时归零时：停止刷新，显示"等待结算"遮罩。
        /// </summary>
        private void HandleTimerExpired()
        {
            _isRunning = false;

            if (_timerText != null)
                _timerText.text = "0s";

            if (_timerBar != null)
                _timerBar.value = 0f;

            UpdateBarColor(0f);
            SetWaiting(true);

            Debug.Log("[RoundTimerUI] 计时归零 → 等待结算...");
        }

        // ══════════════════════════════════════
        // 私有工具方法
        // ══════════════════════════════════════

        /// <summary>
        /// 根据剩余比例设置进度条颜色：绿→黄→红。
        /// </summary>
        private void UpdateBarColor(float ratio)
        {
            if (_timerBarFill == null) return;

            if (ratio > 0.5f)
                _timerBarFill.color = _colorSafe;
            else if (ratio > 0.2f)
                // 黄绿之间线性插值，视觉过渡更平滑
                _timerBarFill.color = Color.Lerp(_colorWarn, _colorSafe, (ratio - 0.2f) / 0.3f);
            else
                // 黄→红插值
                _timerBarFill.color = Color.Lerp(_colorDanger, _colorWarn, ratio / 0.2f);
        }

        /// <summary>
        /// 控制"等待结算"遮罩的显示/隐藏。
        /// </summary>
        private void SetWaiting(bool waiting)
        {
            if (_waitingOverlay != null)
                _waitingOverlay.SetActive(waiting);

            if (_waitingText != null && waiting)
                _waitingText.text = "等待结算中...";
        }

        /// <summary>
        /// 将枚举阶段转换为中文显示名。
        /// </summary>
        private string GetPhaseDisplayName(RoundPhase phase)
        {
            return phase switch
            {
                RoundPhase.RoundStartSettle  => "回合开始",
                RoundPhase.OperationWindow   => "操作窗口期",
                RoundPhase.CommandLock       => "指令锁定",
                RoundPhase.PlanCardSettle    => "定策结算",
                RoundPhase.DeathJudge        => "濒死判定",
                RoundPhase.RoundEnd          => "回合结束",
                _                            => phase.ToString()
            };
        }
    }
}
