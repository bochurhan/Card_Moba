namespace CardMoba.Protocol.Enums
{
    /// <summary>
    /// 卡牌效果目标类型 —— 决定卡牌结算时作用于谁。
    /// Card target type — Determines who the card effect applies to during settlement.
    /// </summary>
    public enum CardTargetType
    {
        /// <summary>无目标（系统效果等）(No target: system effects)</summary>
        None = 0,

        /// <summary>自身 (Self)</summary>
        Self = 1,

        /// <summary>敌方当前对手（同路对位敌人）(Current lane opponent)</summary>
        CurrentEnemy = 2,

        /// <summary>敌方任意（需要选择）(Any enemy: requires selection)</summary>
        AnyEnemy = 3,

        /// <summary>友方任意（需要选择）(Any ally: requires selection)</summary>
        AnyAlly = 4,

        /// <summary>全体敌方（当前路或场景）(All enemies)</summary>
        AllEnemies = 5,

        /// <summary>全体友方（当前路或场景）(All allies)</summary>
        AllAllies = 6,
    }
}