using CardMoba.Protocol.Enums;

namespace CardMoba.ConfigModels.Card
{
    /// <summary>
    /// 卡牌单效果配置 —— 描述卡牌的一个独立效果。
    /// 
    /// 一张卡牌可以有多个效果，每个效果：
    /// - 有独立的效果类型（决定行为）
    /// - 有独立的数值
    /// - 根据效果类型自动归属到对应的结算堆叠层
    /// 
    /// 这符合《定策牌结算机制》多子类型拆分铁律：
    /// "单效果被反制不影响同卡牌其他未被反制的效果正常结算"
    /// </summary>
    public class CardEffect
    {
        /// <summary>效果类型（决定具体行为和所属堆叠层）</summary>
        public EffectType EffectType { get; set; }

        /// <summary>效果数值（伤害量、护甲量、抽牌数等）</summary>
        public int Value { get; set; }

        /// <summary>效果持续回合数（0=即时生效，>0=持续N回合）</summary>
        public int Duration { get; set; }

        /// <summary>效果目标类型（覆盖卡牌默认目标类型）</summary>
        public CardTargetType? TargetOverride { get; set; }

        /// <summary>
        /// 触发条件（用于触发式效果，如"受到伤害时"、"击杀敌人时"）
        /// 空字符串表示无条件触发
        /// </summary>
        public string TriggerCondition { get; set; } = string.Empty;

        /// <summary>
        /// 是否为跨回合效果（本回合锁定，下回合生效）
        /// </summary>
        public bool IsDelayed { get; set; }

        /// <summary>
        /// 获取该效果所属的结算堆叠层。
        /// </summary>
        public SettlementLayer GetSettlementLayer()
        {
            // 根据效果类型编号判断所属堆叠层
            int typeCode = (int)EffectType;

            // 100-199：堆叠1层（防御与数值修正）
            if (typeCode >= 100 && typeCode < 200)
                return SettlementLayer.防御数值层;

            // 200-299：堆叠2层（主动伤害与触发式效果）
            if (typeCode >= 200 && typeCode < 300)
                return SettlementLayer.伤害触发层;

            // 300-399：堆叠3层（控制、资源等收尾效果）
            if (typeCode >= 300 && typeCode < 400)
                return SettlementLayer.收尾效果层;

            // 400-499：堆叠0层（反制效果）
            if (typeCode >= 400 && typeCode < 500)
                return SettlementLayer.反制结算层;

            // 默认归入收尾效果层
            return SettlementLayer.收尾效果层;
        }

        /// <summary>
        /// 判断该效果是否为触发式效果（需要在堆叠2层步骤2处理）
        /// </summary>
        public bool IsTriggerEffect()
        {
            int typeCode = (int)EffectType;
            // 210-299 为触发式效果
            return typeCode >= 210 && typeCode < 300;
        }
    }
}
