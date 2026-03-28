namespace CardMoba.Protocol.Enums
{
    /// <summary>
    /// 卡牌效果目标类型 —— 决定卡牌结算时作用于谁。
    /// Card target type — Determines who the card effect applies to during settlement.
    ///
    /// V2 变更：追加 Opponent / AllOpponents / All 作为语义别名，保持原 ID 不变。
    /// </summary>
    public enum CardTargetType
    {
        /// <summary>无目标（系统效果等）</summary>
        None = 0,

        /// <summary>自身</summary>
        Self = 1,

        /// <summary>敌方当前对手（同路对位敌人）</summary>
        CurrentEnemy = 2,

        /// <summary>敌方任意（需要选择）</summary>
        AnyEnemy = 3,

        /// <summary>友方任意（需要选择）</summary>
        AnyAlly = 4,

        /// <summary>全体敌方</summary>
        AllEnemies = 5,

        /// <summary>全体友方</summary>
        AllAllies = 6,

        /// <summary>全体（敌方 + 己方）</summary>
        All = 7,

        // ── V2 语义别名（供 TargetResolver 统一识别）──

        /// <summary>[V2 别名] 单个对手，等同于 CurrentEnemy</summary>
        Opponent = 2,

        /// <summary>[V2 别名] 所有对手，等同于 AllEnemies</summary>
        AllOpponents = 5,
    }
}