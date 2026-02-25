using System.Collections.Generic;
using CardMoba.Protocol.Enums;

namespace CardMoba.ConfigModels.Card
{
    /// <summary>
    /// 卡牌静态配置 —— 定义一张卡牌的所有不变属性。
    /// 这是"配置表"里一行数据的 C# 映射，运行时只读。
    /// 
    /// 设计原则（符合《定策牌结算机制 V4.0》）：
    /// - 一张卡牌可以有多个效果（Effects 列表）
    /// - 每个效果独立归属到对应的结算堆叠层
    /// - 单效果被反制不影响同卡牌其他效果
    /// </summary>
    public class CardConfig
    {
        // ── 基础信息 ──

        /// <summary>卡牌唯一ID（全局唯一，配置表主键）</summary>
        public int CardId { get; set; }

        /// <summary>卡牌显示名称</summary>
        public string CardName { get; set; } = string.Empty;

        /// <summary>卡牌描述文本（展示给玩家看的效果说明）</summary>
        public string Description { get; set; } = string.Empty;

        // ── 类型分类 ──

        /// <summary>双轨类型：瞬策牌 or 定策牌</summary>
        public CardTrackType TrackType { get; set; }

        /// <summary>
        /// 卡牌标签（词条）：标记卡牌的用途分类和特殊行为。
        /// 
        /// 用途分类：Damage, Defense, Counter, Buff, Debuff, Control, Resource, Support, Legendary
        /// 特殊行为：CrossLane, Recycle, Exhaust, Innate, Retain, Execute
        /// 
        /// 注意：实际结算层由 Effects 列表中的 EffectType 决定，而非 Tags。
        /// </summary>
        public CardTag Tags { get; set; } = CardTag.None;

        /// <summary>目标类型：决定这张牌默认作用于谁</summary>
        public CardTargetType TargetType { get; set; }

        /// <summary>
        /// 效果生效范围 —— 决定目标解析方式。
        /// 
        /// - Self: 自身
        /// - SingleEnemy/SingleAlly: 需要玩家选择单个目标
        /// - CurrentLaneEnemies/AllEnemies: AOE，由 TargetResolver 自动解析
        /// - SpecifiedLane: 跨路支援时指定目标路
        /// </summary>
        public EffectRange EffectRange { get; set; } = EffectRange.SingleEnemy;

        /// <summary>
        /// 卡牌所属结算层 —— 显式声明，不再依赖 EffectType ID 推断。
        /// 
        /// 对于多效果卡牌（跨层），以主要效果的层为准，其他效果在该层之后依次结算。
        /// </summary>
        public SettlementLayer Layer { get; set; } = SettlementLayer.DamageTrigger;

        // ── 费用 ──

        /// <summary>能量消耗（每回合玩家获得固定能量，出牌消耗能量）</summary>
        public int EnergyCost { get; set; }

        // ── 效果列表 ──

        /// <summary>
        /// 卡牌效果列表 —— 支持多子类型卡牌。
        /// 
        /// 例：「铁斩波」有两个效果：
        /// - 效果1：获得4护甲（堆叠1层）
        /// - 效果2：造成5点伤害（堆叠2层）
        /// 
        /// 结算时，两个效果会被拆分到各自的堆叠层分别结算。
        /// </summary>
        public List<CardEffect> Effects { get; set; } = new List<CardEffect>();

        // ── 兼容性字段（保持旧代码兼容） ──

        /// <summary>
        /// [兼容] 基础效果值 —— 用于简单卡牌（只有一个效果时的快捷访问）
        /// 新卡牌请使用 Effects 列表
        /// </summary>
        public int EffectValue
        {
            get => Effects.Count > 0 ? Effects[0].Value : 0;
            set
            {
                if (Effects.Count == 0)
                    Effects.Add(new CardEffect());
                Effects[0].Value = value;
            }
        }

        /// <summary>效果持续回合数（0=即时生效无持续，>0=持续N回合的buff/debuff）</summary>
        public int Duration { get; set; }

        // ── 稀有度 ──

        /// <summary>稀有度等级（1=普通，2=稀有，3=史诗，4=传说）</summary>
        public int Rarity { get; set; } = 1;

        // ── 便捷方法 ──

        /// <summary>
        /// 检查卡牌是否包含指定标签。
        /// </summary>
        public bool HasTag(CardTag tag)
        {
            return (Tags & tag) == tag;
        }

        /// <summary>
        /// 检查卡牌是否为传说牌。
        /// </summary>
        public bool IsLegendary => HasTag(CardTag.Legendary) || Rarity == 4;

        /// <summary>
        /// 获取卡牌中属于指定堆叠层的所有效果。
        /// </summary>
        public List<CardEffect> GetEffectsForLayer(SettlementLayer layer)
        {
            List<CardEffect> result = new List<CardEffect>();
            foreach (var effect in Effects)
            {
                if (effect.GetSettlementLayer() == layer)
                    result.Add(effect);
            }
            return result;
        }
    }
}