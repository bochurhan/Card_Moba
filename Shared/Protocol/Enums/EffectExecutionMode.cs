namespace CardMoba.Protocol.Enums
{
    /// <summary>
    /// 效果执行模式 —— 决定卡牌效果"如何"被执行。
    ///
    /// 三种模式覆盖所有卡牌效果类型：
    ///   Immediate   —— 打出后直接调用 Handler，无额外检查（Strike/Defend 等基础牌）
    ///   Conditional —— 结算时检查 EffectConditions，满足才调用 Handler（观察弱点等）
    ///   Passive     —— 打出时向 BuffManager 注册本回合临时 Buff，由触发器驱动（反制等被动效果）
    ///
    /// 注意：Passive 模式复用 Buff 的生命周期管理，duration=1 表示本回合有效。
    /// </summary>
    public enum EffectExecutionMode
    {
        /// <summary>
        /// 直接执行 —— 默认模式。
        /// 打出卡牌后立即调用对应 Handler，不检查任何运行时条件。
        /// 适用：Strike（攻击）、Defend（防御）、Heal（治愈）等常规效果。
        /// </summary>
        Immediate = 0,

        /// <summary>
        /// 条件执行 —— 结算时检查 EffectConditions。
        /// 仅当所有条件均满足时，才调用对应 Handler 执行效果。
        /// 适用：观察弱点（若敌方本回合出了伤害牌则获得力量）等。
        /// </summary>
        Conditional = 1,

        /// <summary>
        /// 被动触发 —— 打出时通过 BuffManager 注册本回合临时 Buff。
        /// 效果在 Buff 触发器触发时执行（如反制牌在 BeforePlayCard 时机响应）。
        /// 适用：反制效果、观察弱点等需要响应对手行为的效果。
        ///
        /// 注意：复用 BuffManager 生命周期，duration = PassiveDuration（默认1=本回合）。
        /// </summary>
        Passive = 2,
    }
}
