namespace CardMoba.Protocol.Enums
{
    /// <summary>
    /// 定策牌结算堆叠层 —— 决定效果的结算顺序。
    /// Settlement stack layer — Determines effect resolution order.
    ///
    /// V2 五层结构（层序永久不可颠倒）：
    ///   Layer 0 Counter     → 反制效果
    ///   Layer 1 Defense     → 防御/数值修正（护甲、护盾、力量增减）
    ///   Layer 2 Damage      → 主动伤害（含快照隔离，己方顺序依赖）
    ///   Layer 3 Resource    → 资源类（抽牌、能量、生成牌）
    ///   Layer 4 BuffSpecial → Buff/控制/治疗/传说特殊
    ///
    /// ⚠️ 与 V1 的差异：V1 只有 4 层（0-3），V2 将 Utility 拆分为 Resource(3) + BuffSpecial(4)。
    /// </summary>
    public enum SettlementLayer
    {
        /// <summary>
        /// Layer 0：反制层
        /// - 结算反制定策牌，被反制的牌本回合无效
        /// </summary>
        Counter = 0,

        /// <summary>
        /// Layer 1：防御与数值修正层
        /// - 护甲、护盾、无敌、伤害减免、力量增减（AttackBuff/Debuff）
        /// - Layer 1 结束后拍摄防御快照，供 Layer 2 计算使用
        /// </summary>
        Defense = 1,

        /// <summary>
        /// Layer 2：伤害层（含快照隔离机制）
        /// - 同一玩家的伤害牌按提交顺序依次结算（顺序依赖）
        /// - 不同玩家之间以 Layer 1 结束时的防御快照为计算基准（互相隔离）
        /// - 每张牌完整走 A(计算)→B(写入)→C(触发) 三阶段后消化 PendingQueue
        /// </summary>
        Damage = 2,

        /// <summary>
        /// Layer 3：资源层
        /// - 抽牌（Draw）、弃牌（Discard）、能量回复（GainEnergy）、生成牌（GenerateCard）
        /// </summary>
        Resource = 3,

        /// <summary>
        /// Layer 4：Buff 与特殊效果层
        /// - 治疗（Heal）、控制（Stun/Silence/Slow）、Buff/Debuff 施加（AddBuff）
        /// - 易伤（Vulnerable）、虚弱（Weak）、传说特殊效果
        /// </summary>
        BuffSpecial = 4,

        // ── 向后兼容别名（V1 代码暂时可用，后续逐步迁移）──

        /// <summary>[V1 兼容] 等同于 Defense</summary>
        DefenseModifier = 1,

        /// <summary>[V1 兼容] 等同于 Damage</summary>
        DamageTrigger = 2,

        /// <summary>[V1 兼容] 等同于 BuffSpecial</summary>
        Utility = 4,
    }
}