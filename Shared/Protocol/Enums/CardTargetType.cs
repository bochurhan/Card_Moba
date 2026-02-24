namespace CardMoba.Protocol.Enums
{
    /// <summary>
    /// 卡牌效果目标类型 —— 决定卡牌结算时作用于谁。
    /// </summary>
    public enum CardTargetType
    {
        /// <summary>无目标（自身buff等）</summary>
        None = 0,

        /// <summary>指定敌方单体</summary>
        SingleEnemy = 1,

        /// <summary>己方单体（如治疗队友）</summary>
        SingleAlly = 2,

        /// <summary>自身</summary>
        Self = 3,

        /// <summary>敌方全体（当前路）</summary>
        AllEnemiesInLane = 4,

        /// <summary>己方全体（当前路）</summary>
        AllAlliesInLane = 5,
    }
}
