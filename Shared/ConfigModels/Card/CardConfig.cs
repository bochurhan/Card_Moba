using System.Collections.Generic;
using CardMoba.Protocol.Enums;

namespace CardMoba.ConfigModels.Card
{
    /// <summary>
    /// 卡牌配置语义模型。
    /// 对应一张牌的静态定义，不包含战斗内实例状态。
    /// </summary>
    public class CardConfig
    {
        public int CardId { get; set; }

        public string CardName { get; set; } = string.Empty;

        public string Description { get; set; } = string.Empty;

        public CardTrackType TrackType { get; set; }

        public HeroClass HeroClass { get; set; } = HeroClass.Universal;

        public CardTag Tags { get; set; } = CardTag.None;

        public CardTargetType TargetType { get; set; }

        public EffectRange EffectRange { get; set; } = EffectRange.SingleEnemy;

        /// <summary>
        /// 卡牌层级摘要，仅用于作者侧和审阅侧表达。
        /// 实际执行以效果适配后的 EffectUnit.Layer 为准。
        /// </summary>
        public SettlementLayer Layer { get; set; } = SettlementLayer.DamageTrigger;

        public int EnergyCost { get; set; }

        public List<EffectCondition> PlayConditions { get; set; } = new();

        public List<CardEffect> Effects { get; set; } = new();

        public int Rarity { get; set; } = 1;

        public bool HasTag(CardTag tag)
        {
            return (Tags & tag) == tag;
        }

        public bool IsLegendary => HasTag(CardTag.Legendary) || Rarity == 4;
    }
}
