namespace CardMoba.Client.GameLogic.RoundFlow
{
    /// <summary>
    /// 回合各阶段时长配置（客户端本地定义）。
    ///
    /// 时长由服务端权威计时，客户端镜像倒计时仅用于 UI 展示。
    /// 所有时长单位为毫秒（ms）。
    /// </summary>
    public static class RoundPhaseConfig
    {
        // ══════════════════════════════════════════════════════════
        // 标准阶段时长（毫秒）
        // ══════════════════════════════════════════════════════════

        /// <summary>回合开始结算期时长：处理 buff/debuff、抽牌动画（无玩家操作）</summary>
        public const int RoundStartSettleMs = 2_000;

        /// <summary>同步操作窗口期时长：测试模式 30s，时间到自动锁定操作</summary>
        public const int OperationWindowMs = 30_000;

        /// <summary>指令最终锁定期时长：收尾缓冲，等待全员锁定确认到达服务端</summary>
        public const int CommandLockMs = 3_000;

        /// <summary>定策牌统一结算期时长：结算动画播放时间</summary>
        public const int PlanCardSettleMs = 5_000;

        /// <summary>濒死判定期时长：濒死动画/音效播放时间</summary>
        public const int DeathJudgeMs = 3_000;

        /// <summary>回合结束期时长：弃牌动画、数值归零动画</summary>
        public const int RoundEndMs = 3_000;

        // ══════════════════════════════════════════════════════════
        // 便捷方法
        // ══════════════════════════════════════════════════════════

        /// <summary>根据阶段枚举获取对应的默认时长（毫秒）。</summary>
        public static int GetDurationMs(RoundPhase phase)
        {
            switch (phase)
            {
                case RoundPhase.RoundStartSettle: return RoundStartSettleMs;
                case RoundPhase.OperationWindow:  return OperationWindowMs;
                case RoundPhase.CommandLock:      return CommandLockMs;
                case RoundPhase.PlanCardSettle:   return PlanCardSettleMs;
                case RoundPhase.DeathJudge:       return DeathJudgeMs;
                case RoundPhase.RoundEnd:         return RoundEndMs;
                default:                          return RoundStartSettleMs;
            }
        }

        /// <summary>根据阶段枚举获取对应的时长（秒），用于 UI 展示。</summary>
        public static int GetDurationSeconds(RoundPhase phase)
        {
            return (GetDurationMs(phase) + 999) / 1000;
        }

        /// <summary>
        /// 判断该阶段是否允许玩家进行操作。
        /// 只有 OperationWindow 阶段允许玩家输入。
        /// </summary>
        public static bool IsPlayerActionAllowed(RoundPhase phase)
        {
            return phase == RoundPhase.OperationWindow;
        }
    }
}
