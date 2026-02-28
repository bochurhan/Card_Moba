namespace CardMoba.Protocol.Enums
{
    /// <summary>
    /// 单回合的 7 个阶段（严格按顺序流转，由服务端推送，客户端只读）。
    /// 对应设计文档 §4.2 七阶段回合流程。
    /// </summary>
    public enum RoundPhase
    {
        /// <summary>未知/未初始化</summary>
        Unknown = 0,

        /// <summary>1. 回合开始结算期：抽牌、能量回满、Buff/Debuff 衰减结算</summary>
        RoundStartSettle = 1,

        /// <summary>2. 同步操作窗口期：玩家打出瞬策牌 / 提交定策牌</summary>
        OperationWindow = 2,

        /// <summary>3. 指令最终锁定期：锁定所有定策牌，非法指令作废</summary>
        CommandLock = 3,

        /// <summary>4. 定策牌统一结算期：按优先级同步结算所有定策牌</summary>
        PlanCardSettle = 4,

        /// <summary>5. 濒死判定期：统一执行死亡判定，处理复活/撤离逻辑</summary>
        DeathJudge = 5,

        /// <summary>6. 回合结束期：强制弃光手牌、清空能量、清理临时效果</summary>
        RoundEnd = 6,
    }
}
