namespace CardMoba.Protocol.Enums
{
    /// <summary>
    /// 效果条件类型 —— 可检查的运行时状态，用于 CardEffect.EffectConditions 和卡牌打出条件。
    ///
    /// 分为五组：
    ///   100-199: 敌方卡牌状态（本回合定策/结算情况）
    ///   200-299: 我方卡牌/手牌状态
    ///   300-399: 我方角色状态
    ///   400-499: 敌方角色状态
    ///   500-599: 全局/环境状态
    /// </summary>
    public enum EffectConditionType
    {
        // ═════════════════════════════════════════════
        // 100-199: 敌方卡牌状态
        // ═════════════════════════════════════════════

        /// <summary>
        /// 敌方本回合提交了伤害牌（定策/瞬策均算）。
        /// 用于：观察弱点（本回合中，若敌方出了伤害牌，获得力量）。
        /// </summary>
        EnemyPlayedDamageCard = 100,

        /// <summary>
        /// 敌方本回合提交了防御牌。
        /// </summary>
        EnemyPlayedDefenseCard = 101,

        /// <summary>
        /// 敌方本回合提交了反制牌。
        /// </summary>
        EnemyPlayedCounterCard = 102,

        /// <summary>
        /// 敌方本回合提交的牌数量满足条件（配合 ConditionValue 使用，如 &gt;= 2 张）。
        /// </summary>
        EnemyPlayedCardCountAtLeast = 103,

        // ═════════════════════════════════════════════
        // 200-299: 我方卡牌/手牌状态
        // ═════════════════════════════════════════════

        /// <summary>
        /// 我方牌库为空（无剩余可抽牌）。
        /// 用于：华丽收场（牌库为空时才可打出）。
        /// </summary>
        MyDeckIsEmpty = 200,

        /// <summary>
        /// 我方手牌数量满足条件（配合 ConditionValue 使用，如 &lt;= 1 张）。
        /// </summary>
        MyHandCardCountAtMost = 201,

        /// <summary>
        /// 我方手牌数量满足条件（配合 ConditionValue 使用，如 &gt;= 3 张）。
        /// </summary>
        MyHandCardCountAtLeast = 202,

        /// <summary>
        /// 我方本回合已打出的牌数量满足条件（配合 ConditionValue 使用）。
        /// </summary>
        MyPlayedCardCountAtLeast = 203,

        // ═════════════════════════════════════════════
        // 300-399: 我方角色状态
        // ═════════════════════════════════════════════

        /// <summary>
        /// 我方生命值低于指定百分比（配合 ConditionValue 使用，如 &lt;= 50 表示半血以下）。
        /// </summary>
        MyHpPercentAtMost = 300,

        /// <summary>
        /// 我方生命值高于指定百分比（配合 ConditionValue 使用）。
        /// </summary>
        MyHpPercentAtLeast = 301,

        /// <summary>
        /// 我方拥有指定类型的 Buff（配合 ConditionBuffType 使用）。
        /// </summary>
        MyHasBuffType = 302,

        /// <summary>
        /// 我方力量值满足条件（配合 ConditionValue 使用，如 &gt;= 3）。
        /// </summary>
        MyStrengthAtLeast = 303,

        // ═════════════════════════════════════════════
        // 400-499: 敌方角色状态
        // ═════════════════════════════════════════════

        /// <summary>
        /// 敌方生命值低于指定百分比（配合 ConditionValue 使用，如 &lt;= 30 表示残血）。
        /// </summary>
        EnemyHpPercentAtMost = 400,

        /// <summary>
        /// 敌方拥有指定类型的 Buff（配合 ConditionBuffType 使用）。
        /// </summary>
        EnemyHasBuffType = 401,

        /// <summary>
        /// 敌方处于眩晕状态。
        /// </summary>
        EnemyIsStunned = 402,

        // ═════════════════════════════════════════════
        // 500-599: 全局/环境状态
        // ═════════════════════════════════════════════

        /// <summary>
        /// 当前回合数满足条件（配合 ConditionValue 使用，如 &gt;= 5 表示第5回合后）。
        /// </summary>
        RoundNumberAtLeast = 500,
    }
}
