namespace CardMoba.Protocol.Enums
{
    /// <summary>
    /// 效果条件类型 —— CardEffect.EffectConditions 中可检查的运行时状态。
    ///
    /// 分组：
    ///   100-199: 敌方卡牌状态（本回合定策/出牌情况）
    ///   200-299: 我方卡牌/手牌状态
    ///   300-399: 我方角色状态
    ///   400-499: 敌方角色状态
    ///   500-599: 全局/环境状态
    /// </summary>
    public enum EffectConditionType
    {
        // ── 100-199: 敌方卡牌状态 ──────────────────────────────

        /// <summary>敌方本回合提交了伤害牌（定策/瞬策均算）。</summary>
        EnemyPlayedDamageCard = 100,

        /// <summary>敌方本回合提交了防御牌。</summary>
        EnemyPlayedDefenseCard = 101,

        /// <summary>敌方本回合提交了反制牌。</summary>
        EnemyPlayedCounterCard = 102,

        /// <summary>敌方本回合提交的牌数量 ≥ ConditionValue。</summary>
        EnemyPlayedCardCountAtLeast = 103,

        // ── 200-299: 我方卡牌/手牌状态 ────────────────────────

        /// <summary>我方牌库为空（无剩余可抽牌）。</summary>
        MyDeckIsEmpty = 200,

        /// <summary>我方手牌数量 ≤ ConditionValue。</summary>
        MyHandCardCountAtMost = 201,

        /// <summary>我方手牌数量 ≥ ConditionValue。</summary>
        MyHandCardCountAtLeast = 202,

        /// <summary>我方本回合已打出的牌数量 ≥ ConditionValue。</summary>
        MyPlayedCardCountAtLeast = 203,

        // ── 300-399: 我方角色状态 ──────────────────────────────

        /// <summary>我方生命值百分比 ≤ ConditionValue（如50表示半血以下）。</summary>
        MyHpPercentAtMost = 300,

        /// <summary>我方生命值百分比 ≥ ConditionValue。</summary>
        MyHpPercentAtLeast = 301,

        /// <summary>我方拥有指定类型 Buff（配合 ConditionBuffType 使用）。</summary>
        MyHasBuffType = 302,

        /// <summary>我方力量值 ≥ ConditionValue。</summary>
        MyStrengthAtLeast = 303,

        // ── 400-499: 敌方角色状态 ──────────────────────────────

        /// <summary>敌方生命值百分比 ≤ ConditionValue（如30表示残血）。</summary>
        EnemyHpPercentAtMost = 400,

        /// <summary>敌方拥有指定类型 Buff（配合 ConditionBuffType 使用）。</summary>
        EnemyHasBuffType = 401,

        /// <summary>敌方处于眩晕状态。</summary>
        EnemyIsStunned = 402,

        // ── 500-599: 全局/环境状态 ─────────────────────────────

        /// <summary>当前回合数 ≥ ConditionValue。</summary>
        RoundNumberAtLeast = 500,
    }
}
