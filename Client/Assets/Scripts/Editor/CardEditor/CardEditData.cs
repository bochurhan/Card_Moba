using System;
using System.Collections.Generic;
using CardMoba.Protocol.Enums;

namespace CardMoba.Client.Editor.CardEditor
{
    [Serializable]
    public class CardEditData
    {
        public int CardId;
        public string CardName = string.Empty;
        public string Description = string.Empty;

        public CardTrackType TrackType = CardTrackType.Instant;
        public CardTargetType TargetType = CardTargetType.CurrentEnemy;
        public HeroClass HeroClass = HeroClass.Universal;
        public EffectRange EffectRange = EffectRange.SingleEnemy;
        public SettlementLayer Layer = SettlementLayer.DamageTrigger;

        public int EnergyCost = 1;
        public int Rarity = 1;
        public string UpgradedCardConfigId = string.Empty;

        public bool TagDamage;
        public bool TagDefense;
        public bool TagCounter;
        public bool TagBuff;
        public bool TagDebuff;
        public bool TagControl;
        public bool TagResource;
        public bool TagSupport;
        public bool TagCrossLane;
        public bool TagExhaust;
        public bool TagRecycle;
        public bool TagReflect;
        public bool TagLegendary;
        public bool TagInnate;
        public bool TagRetain;
        public bool TagStatus;

        public List<PlayConditionEditData> PlayConditions = new();
        public List<EffectEditData> Effects = new();

        [NonSerialized] public bool FoldoutExpanded = true;
        [NonSerialized] public bool PlayConditionsFoldout;

        public CardTag GetTags()
        {
            CardTag result = CardTag.None;
            if (TagDamage) result |= CardTag.Damage;
            if (TagDefense) result |= CardTag.Defense;
            if (TagCounter) result |= CardTag.Counter;
            if (TagBuff) result |= CardTag.Buff;
            if (TagDebuff) result |= CardTag.Debuff;
            if (TagControl) result |= CardTag.Control;
            if (TagResource) result |= CardTag.Resource;
            if (TagSupport) result |= CardTag.Support;
            if (TagCrossLane) result |= CardTag.CrossLane;
            if (TagExhaust) result |= CardTag.Exhaust;
            if (TagRecycle) result |= CardTag.Recycle;
            if (TagReflect) result |= CardTag.Reflect;
            if (TagLegendary) result |= CardTag.Legendary;
            if (TagInnate) result |= CardTag.Innate;
            if (TagRetain) result |= CardTag.Retain;
            if (TagStatus) result |= CardTag.Status;
            return result;
        }

        public void SetTags(CardTag tags)
        {
            TagDamage = (tags & CardTag.Damage) != 0;
            TagDefense = (tags & CardTag.Defense) != 0;
            TagCounter = (tags & CardTag.Counter) != 0;
            TagBuff = (tags & CardTag.Buff) != 0;
            TagDebuff = (tags & CardTag.Debuff) != 0;
            TagControl = (tags & CardTag.Control) != 0;
            TagResource = (tags & CardTag.Resource) != 0;
            TagSupport = (tags & CardTag.Support) != 0;
            TagCrossLane = (tags & CardTag.CrossLane) != 0;
            TagExhaust = (tags & CardTag.Exhaust) != 0;
            TagRecycle = (tags & CardTag.Recycle) != 0;
            TagReflect = (tags & CardTag.Reflect) != 0;
            TagLegendary = (tags & CardTag.Legendary) != 0;
            TagInnate = (tags & CardTag.Innate) != 0;
            TagRetain = (tags & CardTag.Retain) != 0;
            TagStatus = (tags & CardTag.Status) != 0;
        }

        public List<string> GetTagList()
        {
            var list = new List<string>();
            if (TagDamage) list.Add("Damage");
            if (TagDefense) list.Add("Defense");
            if (TagCounter) list.Add("Counter");
            if (TagBuff) list.Add("Buff");
            if (TagDebuff) list.Add("Debuff");
            if (TagControl) list.Add("Control");
            if (TagResource) list.Add("Resource");
            if (TagSupport) list.Add("Support");
            if (TagCrossLane) list.Add("CrossLane");
            if (TagExhaust) list.Add("Exhaust");
            if (TagRecycle) list.Add("Recycle");
            if (TagReflect) list.Add("Reflect");
            if (TagLegendary) list.Add("Legendary");
            if (TagInnate) list.Add("Innate");
            if (TagRetain) list.Add("Retain");
            if (TagStatus) list.Add("Status");
            return list;
        }

        public void SetTagList(List<string> tags)
        {
            TagDamage = tags.Contains("Damage");
            TagDefense = tags.Contains("Defense");
            TagCounter = tags.Contains("Counter");
            TagBuff = tags.Contains("Buff");
            TagDebuff = tags.Contains("Debuff");
            TagControl = tags.Contains("Control");
            TagResource = tags.Contains("Resource");
            TagSupport = tags.Contains("Support");
            TagCrossLane = tags.Contains("CrossLane");
            TagExhaust = tags.Contains("Exhaust");
            TagRecycle = tags.Contains("Recycle");
            TagReflect = tags.Contains("Reflect");
            TagLegendary = tags.Contains("Legendary");
            TagInnate = tags.Contains("Innate");
            TagRetain = tags.Contains("Retain");
            TagStatus = tags.Contains("Status");
        }

        public CardEditData Clone(int newId)
        {
            var clone = new CardEditData
            {
                CardId = newId,
                CardName = CardName + " (复制)",
                Description = Description,
                TrackType = TrackType,
                TargetType = TargetType,
                HeroClass = HeroClass,
                EffectRange = EffectRange,
                Layer = Layer,
                EnergyCost = EnergyCost,
                Rarity = Rarity,
                UpgradedCardConfigId = UpgradedCardConfigId,
                TagDamage = TagDamage,
                TagDefense = TagDefense,
                TagCounter = TagCounter,
                TagBuff = TagBuff,
                TagDebuff = TagDebuff,
                TagControl = TagControl,
                TagResource = TagResource,
                TagSupport = TagSupport,
                TagCrossLane = TagCrossLane,
                TagExhaust = TagExhaust,
                TagRecycle = TagRecycle,
                TagReflect = TagReflect,
                TagLegendary = TagLegendary,
                TagInnate = TagInnate,
                TagRetain = TagRetain,
                TagStatus = TagStatus
            };

            foreach (var playCondition in PlayConditions)
                clone.PlayConditions.Add(playCondition.Clone());

            foreach (var effect in Effects)
                clone.Effects.Add(effect.Clone());

            return clone;
        }
    }

    [Serializable]
    public class EffectEditData
    {
        public EffectType EffectType = EffectType.Damage;
        public int Value = 5;
        public string ValueExpression = string.Empty;
        public int Duration;
        public CardTargetType? TargetOverride;
        public string BuffConfigId = string.Empty;
        public string GenerateCardConfigId = string.Empty;
        public string GenerateCardZone = "Hand";
        public bool GenerateCardIsTemp;
        public string ProjectionLifetime = string.Empty;
        public int RepeatCount = 1;
        public List<EffectConditionEditData> EffectConditions = new();
        public int Priority = 500;
        public int SubPriority;

        [NonSerialized] public bool FoldoutExpanded = true;
        [NonSerialized] public bool ConditionsFoldout;

        public string GenerateDescription()
        {
            string valueText = string.IsNullOrWhiteSpace(ValueExpression)
                ? Value.ToString()
                : ValueExpression;

            string desc = EffectType switch
            {
                EffectType.Damage => $"造成 {valueText} 点伤害",
                EffectType.Shield => $"获得 {valueText} 点护盾",
                EffectType.Armor => $"获得 {valueText} 点护甲",
                EffectType.AttackBuff => $"获得 {valueText} 点力量",
                EffectType.AttackDebuff => $"削减 {valueText} 点力量",
                EffectType.Heal => $"回复 {valueText} 点生命",
                EffectType.Counter => "反制敌方效果",
                EffectType.Reflect => $"反伤 {valueText}%",
                EffectType.DamageReduction => $"减免 {valueText}% 伤害",
                EffectType.Invincible => $"无敌 {valueText} 回合",
                EffectType.Vulnerable => $"施加易伤 {valueText}%",
                EffectType.Weak => $"施加虚弱 {valueText}%",
                EffectType.Stun => $"眩晕 {valueText} 回合",
                EffectType.Silence => $"沉默 {valueText} 回合",
                EffectType.Slow => $"迟缓 {valueText} 回合",
                EffectType.Draw => $"抽 {valueText} 张牌",
                EffectType.Discard => $"丢弃 {valueText} 张牌",
                EffectType.GainEnergy => $"获得 {valueText} 点能量",
                EffectType.Lifesteal => $"吸血 {valueText}%",
                EffectType.Thorns => $"荆棘反伤 {valueText}%",
                EffectType.ArmorOnHit => $"受击获得 {valueText} 护甲",
                EffectType.Pierce => $"穿透 {valueText} 点护甲",
                EffectType.AddBuff when !string.IsNullOrWhiteSpace(BuffConfigId) => $"附加 Buff {BuffConfigId}",
                EffectType.GenerateCard when !string.IsNullOrWhiteSpace(GenerateCardConfigId) => $"生成卡牌 {GenerateCardConfigId}",
                EffectType.MoveSelectedCardToDeckTop => "选择 1 张弃牌堆中的牌，置于牌库顶部",
                EffectType.ReturnSourceCardToHandAtRoundEnd => "回合结束时若此牌在弃牌堆，则返回手牌",
                EffectType.UpgradeCardsInHand => $"升级当前手牌（{ProjectionLifetime}）",
                _ => $"{EffectType}: {valueText}"
            };

            if (RepeatCount > 1)
                desc += $" x{RepeatCount}";

            if (Duration > 0)
                desc += $"（持续 {Duration} 回合）";

            return desc;
        }

        public EffectEditData Clone()
        {
            var clone = new EffectEditData
            {
                EffectType = EffectType,
                Value = Value,
                ValueExpression = ValueExpression,
                Duration = Duration,
                TargetOverride = TargetOverride,
                BuffConfigId = BuffConfigId,
                GenerateCardConfigId = GenerateCardConfigId,
                GenerateCardZone = GenerateCardZone,
                GenerateCardIsTemp = GenerateCardIsTemp,
                ProjectionLifetime = ProjectionLifetime,
                RepeatCount = RepeatCount,
                Priority = Priority,
                SubPriority = SubPriority,
                FoldoutExpanded = true
            };

            foreach (var condition in EffectConditions)
                clone.EffectConditions.Add(condition.Clone());

            return clone;
        }
    }

    [Serializable]
    public class PlayConditionEditData
    {
        public EffectConditionType ConditionType = EffectConditionType.MyDeckIsEmpty;
        public int Threshold;
        public bool Negate;
        public string FailMessage = string.Empty;

        public PlayConditionEditData Clone() => new()
        {
            ConditionType = ConditionType,
            Threshold = Threshold,
            Negate = Negate,
            FailMessage = FailMessage
        };
    }

    [Serializable]
    public class EffectConditionEditData
    {
        public EffectConditionType ConditionType = EffectConditionType.MyDeckIsEmpty;
        public int Threshold;
        public bool Negate;

        public EffectConditionEditData Clone() => new()
        {
            ConditionType = ConditionType,
            Threshold = Threshold,
            Negate = Negate
        };
    }
}
