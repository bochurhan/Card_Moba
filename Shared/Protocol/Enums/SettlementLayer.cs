namespace CardMoba.Protocol.Enums
{
    /// <summary>
    /// 定策牌结算堆叠层 —— 决定效果的结算顺序。
    /// Settlement stack layer — Determines effect resolution order.
    /// 
    /// 根据《定策牌结算机制 V4.0》，层级顺序永久不可颠倒：
    /// 堆叠0层 → 堆叠1层 → 堆叠2层 → 堆叠3层
    /// </summary>
    public enum SettlementLayer
    {
        /// <summary>
        /// 堆叠0层：反制效果结算层
        /// Layer 0: Counter effects resolution
        /// - 结算上回合提交的反制定策牌
        /// - 校验触发条件，执行无效化/惩罚效果
        /// - 统计本回合所有有效定策牌
        /// </summary>
        Counter = 0,

        /// <summary>
        /// 堆叠1层：防御与数值修正层
        /// Layer 1: Defense and stat modifiers
        /// - 护甲、护盾、伤害减免、无敌、免伤
        /// - 攻击/力量增减、破甲、穿透、易伤、虚弱
        /// - 锁定后续伤害计算的基准数值
        /// </summary>
        DefenseModifier = 1,

        /// <summary>
        /// 堆叠2层：主动伤害与触发式效果闭环层
        /// Layer 2: Active damage and triggered effects
        /// - 步骤1：所有伤害牌同步、一次性结算
        /// - 步骤2：触发式效果（反伤、吸血等）同步闭环结算
        /// - 连锁封顶：触发效果不再触发新的连锁
        /// </summary>
        DamageTrigger = 2,

        /// <summary>
        /// 堆叠3层：全局非依赖效果收尾层
        /// Layer 3: Global non-dependent effects (utility)
        /// - 子阶段1：控制、资源、支援类效果
        /// - 子阶段2：传说特殊牌专属结算
        /// </summary>
        Utility = 3,
    }
}