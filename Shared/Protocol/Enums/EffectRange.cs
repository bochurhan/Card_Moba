namespace CardMoba.Protocol.Enums
{
    /// <summary>
    /// 效果生效范围 —— 决定卡牌效果可以作用于哪些目标。
    /// Effect range — Determines which targets a card effect can affect.
    /// 
    /// 用于目标解析器（TargetResolver）根据范围类型自动解析实际目标列表。
    /// - 单体类型：需要玩家选择目标，记录在 RawTargetGroup 中
    /// - 范围类型：无需选择，由 TargetResolver 根据当前战斗状态解析
    /// </summary>
    public enum EffectRange
    {
        /// <summary>无目标/自动目标 (No target / auto-target)</summary>
        None = 0,

        // ── 单体目标（需玩家选择）──

        /// <summary>自身 (Self)</summary>
        Self = 1,

        /// <summary>单个敌方单位 (Single enemy)</summary>
        SingleEnemy = 2,

        /// <summary>单个友方单位 (Single ally)</summary>
        SingleAlly = 3,

        /// <summary>单个任意单位（敌我皆可）(Single any unit)</summary>
        SingleAny = 4,

        // ── 范围目标（自动解析）──

        /// <summary>当前路所有敌方 (All enemies in current lane)</summary>
        CurrentLaneEnemies = 10,

        /// <summary>当前路所有友方 (All allies in current lane)</summary>
        CurrentLaneAllies = 11,

        /// <summary>当前路所有单位 (All units in current lane)</summary>
        CurrentLaneAll = 12,

        /// <summary>所有敌方（跨路）(All enemies across lanes)</summary>
        AllEnemies = 20,

        /// <summary>所有友方（跨路）(All allies across lanes)</summary>
        AllAllies = 21,

        /// <summary>所有单位（全场）(All units in battle)</summary>
        AllUnits = 22,

        // ── 特殊范围 ──

        /// <summary>相邻路（支援范围）(Adjacent lanes - for support cards)</summary>
        AdjacentLanes = 30,

        /// <summary>指定路（跨路支援时指定）(Specified lane - for cross-lane support)</summary>
        SpecifiedLane = 31,

        /// <summary>随机敌方 (Random enemy)</summary>
        RandomEnemy = 40,

        /// <summary>随机友方 (Random ally)</summary>
        RandomAlly = 41,
    }
}
