namespace CardMoba.Protocol.Enums
{
    /// <summary>
    /// 效果执行模式 —— 决定卡牌效果"如何"被执行。
    ///
    /// 三种模式覆盖所有卡牌效果类型：
    ///   Immediate   —— 打出后直接调用 Handler，无额外检查（Strike/Defend 等基础牌）
    ///   Conditional —— 结算时检查 EffectConditions，满足才调用 Handler（观察弱点等）
    ///   Passive     —— 打出时向 BuffManager 注册本回合临时 Buff，由触发器驱动（反制等被动效果）
    /// </summary>
    public enum EffectExecutionMode
    {
        /// <summary>直接执行 —— 默认模式，打出后立即调用 Handler。</summary>
        Immediate = 0,

        /// <summary>条件执行 —— 结算时检查 EffectConditions，全部满足才执行。</summary>
        Conditional = 1,

        /// <summary>被动触发 —— 打出时注册临时 Buff，由触发器时机驱动（如反制）。</summary>
        Passive = 2,
    }
}
