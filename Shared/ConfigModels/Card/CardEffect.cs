using System.Collections.Generic;
using CardMoba.Protocol.Enums;

namespace CardMoba.ConfigModels.Card
{
    /// <summary>
    /// 卡牌单效果配置 —— 描述卡牌的一个独立效果。
    /// 
    /// V3.0 架构：
    /// - 每个效果对应一个 Handler（由 EffectType 决定）
    /// - 自动归属到 4 层结算栈 (0=反制, 1=防御, 2=伤害, 3=功能)
    /// - 支持复杂效果参数和子效果组合
    /// 
    /// 这符合《定策牌结算机制》多子类型拆分铁律：
    /// "单效果被反制不影响同卡牌其他未被反制的效果正常结算"
    /// </summary>
    public class CardEffect
    {
        // ═══════════════════════════════════════════════════════════
        // 基础属性
        // ═══════════════════════════════════════════════════════════

        /// <summary>效果类型（决定具体行为和所属堆叠层）</summary>
        public EffectType EffectType { get; set; }

        /// <summary>效果类型简写（等同于 EffectType）</summary>
        public EffectType Type => EffectType;

        /// <summary>效果所属结算层（可在配置中显式指定，未指定则自动推断）</summary>
        public SettlementLayer Layer { get; set; } = SettlementLayer.Utility;

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

        // ═══════════════════════════════════════════════════════════
        // V3.0 新增 - Handler 架构支持
        // ═══════════════════════════════════════════════════════════

        /// <summary>
        /// 效果参数（用于复杂效果的额外配置）
        /// </summary>
        public EffectParams Params { get; set; }

        /// <summary>
        /// 子效果列表（用于组合效果）
        /// </summary>
        public List<SubEffect> SubEffects { get; set; }

        // ═══════════════════════════════════════════════════════════
        // 方法
        // ═══════════════════════════════════════════════════════════

        /// <summary>
        /// 获取该效果所属的结算堆叠层（V3.0 四层架构）。
        /// Layer 0: 反制 (Counter)
        /// Layer 1: 防御 (Defense/Shield/Armor)
        /// Layer 2: 伤害 (Damage)
        /// Layer 3: 功能 (Utility - Heal, Stun, Draw, etc.)
        /// </summary>
        public int GetSettlementLayerV3()
        {
            // V3.0 新效果类型 (1-10)
            return EffectType switch
            {
                // Layer 0: 反制
                EffectType.Counter => 0,
                
                // Layer 1: 防御/修正
                EffectType.Shield => 1,
                EffectType.Armor => 1,
                EffectType.AttackBuff => 1,
                EffectType.Reflect => 1,
                
                // Layer 2: 伤害
                EffectType.Damage => 2,
                
                // Layer 3: 功能
                EffectType.Heal => 3,
                EffectType.Stun => 3,
                EffectType.Vulnerable => 3,
                EffectType.Draw => 3,
                
                // 兼容旧版：根据编号范围判断
                _ => GetLegacyLayer()
            };
        }
        
        /// <summary>
        /// 兼容旧版效果类型的层级判断
        /// </summary>
        private int GetLegacyLayer()
        {
            int typeCode = (int)EffectType;
            
            // 400-499: Layer 0 (反制)
            if (typeCode >= 400 && typeCode < 500)
                return 0;
            
            // 100-199: Layer 1 (防御)
            if (typeCode >= 100 && typeCode < 200)
                return 1;
            
            // 200-299: Layer 2 (伤害/触发)
            if (typeCode >= 200 && typeCode < 300)
                return 2;
            
            // 300-399: Layer 3 (功能)
            return 3;
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

    // ═══════════════════════════════════════════════════════════════
    // V3.0 新增类型
    // ═══════════════════════════════════════════════════════════════

    /// <summary>
    /// 效果参数 - 用于复杂效果的额外配置
    /// </summary>
    public class EffectParams
    {
        /// <summary>百分比值（用于反伤、吸血等）</summary>
        public int Percent { get; set; }

        /// <summary>次要数值（如额外护甲、额外伤害）</summary>
        public int SecondaryValue { get; set; }

        /// <summary>可反制的效果类型列表（用于反制牌）</summary>
        public List<EffectType> CounterableTypes { get; set; }

        /// <summary>触发器类型（用于被动效果）</summary>
        public string TriggerType { get; set; }

        /// <summary>触发器参数</summary>
        public string TriggerParam { get; set; }
    }

    /// <summary>
    /// 子效果 - 用于组合效果
    /// </summary>
    public class SubEffect
    {
        /// <summary>子效果类型</summary>
        public EffectType EffectType { get; set; }

        /// <summary>子效果数值</summary>
        public int Value { get; set; }

        /// <summary>子效果目标（可选）</summary>
        public CardTargetType? TargetOverride { get; set; }

        /// <summary>子效果持续回合</summary>
        public int Duration { get; set; }
    }
}
