namespace CardMoba.Protocol.Enums
{
    /// <summary>
    /// 效果生效范围 —— 决定卡牌效果可以作用于哪些目标。
    /// 用于目标解析器（TargetResolver）根据范围类型自动解析实际目标列表。
    /// </summary>
    public enum EffectRange
    {
        /// <summary>无目标/自动目标</summary>
        None = 0,

        // ── 单体目标（需玩家选择）────────────────────────────

        /// <summary>自身</summary>
        Self = 1,

        /// <summary>单个敌方单位</summary>
        SingleEnemy = 2,

        /// <summary>单个友方单位</summary>
        SingleAlly = 3,

        /// <summary>单个任意单位（敌我皆可）</summary>
        SingleAny = 4,

        // ── 范围目标（自动解析）──────────────────────────────

        /// <summary>当前路所有敌方</summary>
        CurrentLaneEnemies = 10,

        /// <summary>当前路所有友方</summary>
        CurrentLaneAllies = 11,

        /// <summary>当前路所有单位</summary>
        CurrentLaneAll = 12,

        /// <summary>所有敌方（跨路）</summary>
        AllEnemies = 20,

        /// <summary>所有友方（跨路）</summary>
        AllAllies = 21,

        /// <summary>所有单位（全场）</summary>
        AllUnits = 22,

        // ── 特殊范围 ─────────────────────────────────────────

        /// <summary>相邻路（支援范围）</summary>
        AdjacentLanes = 30,

        /// <summary>指定路（跨路支援时指定）</summary>
        SpecifiedLane = 31,

        /// <summary>随机敌方</summary>
        RandomEnemy = 40,

        /// <summary>随机友方</summary>
        RandomAlly = 41,
    }
}
