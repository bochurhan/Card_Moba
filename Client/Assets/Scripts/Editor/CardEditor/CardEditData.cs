using System;
using System.Collections.Generic;
using CardMoba.Protocol.Enums;

namespace CardMoba.Client.Editor.CardEditor
{
    /// <summary>
    /// 卡牌编辑数据（编辑器内部使用）——完整对齐 CardConfig 字段。
    /// </summary>
    [Serializable]
    public class CardEditData
    {
        // ─── 基础信息 ───────────────────────────────────────────────
        public int    CardId;
        public string CardName    = "";
        public string Description = "";

        // ─── 类型与范围 ──────────────────────────────────────────────
        public CardTrackType   TrackType   = CardTrackType.Instant;
        public CardTargetType  TargetType  = CardTargetType.CurrentEnemy;
        public HeroClass       HeroClass   = HeroClass.Universal;
        public EffectRange     EffectRange = EffectRange.SingleEnemy;
        public SettlementLayer Layer       = SettlementLayer.DamageTrigger;

        // ─── 费用与稀有度 ────────────────────────────────────────────
        public int EnergyCost = 1;
        public int Rarity     = 1;

        // ─── 标签（全量，对齐 CardTag [Flags]）──────────────────────
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

        // ─── 内嵌效果列表（替代旧版 EffectIds 外键）────────────────
        public List<EffectEditData> Effects = new();

        // ─── 折叠状态（编辑器 UI 专用，不序列化）───────────────────
        [NonSerialized] public bool FoldoutExpanded = true;

        // ════════════════════════════════════════════════════════════
        // 标签互转工具
        // ════════════════════════════════════════════════════════════

        /// <summary>将 bool 字段组合为 CardTag 标志枚举</summary>
        public CardTag GetTags()
        {
            CardTag result = CardTag.None;
            if (TagDamage)   result |= CardTag.Damage;
            if (TagDefense)  result |= CardTag.Defense;
            if (TagCounter)  result |= CardTag.Counter;
            if (TagBuff)     result |= CardTag.Buff;
            if (TagDebuff)   result |= CardTag.Debuff;
            if (TagControl)  result |= CardTag.Control;
            if (TagCrossLane)result |= CardTag.CrossLane;
            if (TagExhaust)  result |= CardTag.Exhaust;
            if (TagRecycle)  result |= CardTag.Recycle;
            if (TagReflect)  result |= CardTag.Reflect;
            if (TagLegendary)result |= CardTag.Legendary;
            return result;
        }

        /// <summary>从 CardTag 标志枚举拆分为 bool 字段</summary>
        public void SetTags(CardTag tags)
        {
            TagDamage    = (tags & CardTag.Damage)    != 0;
            TagDefense   = (tags & CardTag.Defense)   != 0;
            TagCounter   = (tags & CardTag.Counter)   != 0;
            TagBuff      = (tags & CardTag.Buff)      != 0;
            TagDebuff    = (tags & CardTag.Debuff)    != 0;
            TagControl   = (tags & CardTag.Control)   != 0;
            TagCrossLane = (tags & CardTag.CrossLane) != 0;
            TagExhaust   = (tags & CardTag.Exhaust)   != 0;
            TagRecycle   = (tags & CardTag.Recycle)   != 0;
            TagReflect   = (tags & CardTag.Reflect)   != 0;
            TagLegendary = (tags & CardTag.Legendary) != 0;
        }

        /// <summary>获取标签名称列表（用于 JSON / CSV 序列化）</summary>
        public List<string> GetTagList()
        {
            var list = new List<string>();
            if (TagDamage)   list.Add("Damage");
            if (TagDefense)  list.Add("Defense");
            if (TagCounter)  list.Add("Counter");
            if (TagBuff)     list.Add("Buff");
            if (TagDebuff)   list.Add("Debuff");
            if (TagControl)  list.Add("Control");
            if (TagResource) list.Add("Resource");
            if (TagSupport)  list.Add("Support");
            if (TagCrossLane)list.Add("CrossLane");
            if (TagExhaust)  list.Add("Exhaust");
            if (TagRecycle)  list.Add("Recycle");
            if (TagReflect)  list.Add("Reflect");
            if (TagLegendary)list.Add("Legendary");
            if (TagInnate)   list.Add("Innate");
            if (TagRetain)   list.Add("Retain");
            return list;
        }

        /// <summary>从标签名称列表设置 bool 字段</summary>
        public void SetTagList(List<string> tags)
        {
            TagDamage    = tags.Contains("Damage");
            TagDefense   = tags.Contains("Defense");
            TagCounter   = tags.Contains("Counter");
            TagBuff      = tags.Contains("Buff");
            TagDebuff    = tags.Contains("Debuff");
            TagControl   = tags.Contains("Control");
            TagResource  = tags.Contains("Resource");
            TagSupport   = tags.Contains("Support");
            TagCrossLane = tags.Contains("CrossLane");
            TagExhaust   = tags.Contains("Exhaust");
            TagRecycle   = tags.Contains("Recycle");
            TagReflect   = tags.Contains("Reflect");
            TagLegendary = tags.Contains("Legendary");
            TagInnate    = tags.Contains("Innate");
            TagRetain    = tags.Contains("Retain");
        }

        /// <summary>深复制（用于卡牌复制功能）</summary>
        public CardEditData Clone(int newId)
        {
            var c = new CardEditData
            {
                CardId      = newId,
                CardName    = CardName + " (复制)",
                Description = Description,
                TrackType   = TrackType,
                TargetType  = TargetType,
                HeroClass   = HeroClass,
                EffectRange = EffectRange,
                Layer       = Layer,
                EnergyCost  = EnergyCost,
                Rarity      = Rarity,
                TagDamage   = TagDamage,   TagDefense  = TagDefense,
                TagCounter  = TagCounter,  TagBuff     = TagBuff,
                TagDebuff   = TagDebuff,   TagControl  = TagControl,
                TagResource = TagResource, TagSupport  = TagSupport,
                TagCrossLane= TagCrossLane,TagExhaust  = TagExhaust,
                TagRecycle  = TagRecycle,  TagReflect  = TagReflect,
                TagLegendary= TagLegendary,TagInnate   = TagInnate,
                TagRetain   = TagRetain
            };
            foreach (var e in Effects)
                c.Effects.Add(e.Clone());
            return c;
        }
    }

    // ════════════════════════════════════════════════════════════════
    // 效果编辑数据 —— 完整对齐 CardEffect 字段
    // ════════════════════════════════════════════════════════════════

    /// <summary>
    /// 效果编辑数据 —— 字段完整对齐 Shared/ConfigModels/Card/CardEffect.cs。
    /// </summary>
    [Serializable]
    public class EffectEditData
    {
        // ─── 基础字段 ────────────────────────────────────────────────
        public EffectType     EffectType     = EffectType.Damage;
        public int            Value          = 5;
        public int            Duration       = 0;
        public CardTargetType?TargetOverride = null;
        public bool           IsDelayed      = false;
        public string         TriggerCondition = "";

        // ─── Buff 附加声明 ───────────────────────────────────────────
        public bool           AppliesBuff       = false;
        public BuffType       BuffType          = BuffType.Unknown;
        public BuffStackRule  BuffStackRule     = BuffStackRule.RefreshDuration;
        public bool           IsBuffDispellable = true;

        // ─── 优先级控制 ──────────────────────────────────────────────
        public int            Priority    = 500;
        public int            SubPriority = 0;

        // ─── 编辑器 UI 专用 ──────────────────────────────────────────
        [NonSerialized] public bool FoldoutExpanded  = true;
        [NonSerialized] public bool BuffSectionExpanded = false;

        /// <summary>根据字段自动生成人类可读描述（供预览面板使用）</summary>
        public string GenerateDescription()
        {
            string desc = EffectType switch
            {
                EffectType.Damage          => $"造成 {Value} 点伤害",
                EffectType.Shield          => $"获得 {Value} 点护盾",
                EffectType.Armor           => $"获得 {Value} 点护甲",
                EffectType.AttackBuff      => $"获得 {Value} 点力量",
                EffectType.AttackDebuff    => $"削减 {Value} 点力量",
                EffectType.Heal            => $"回复 {Value} 点生命",
                EffectType.Counter         => "反制敌方效果",
                EffectType.Reflect         => $"反伤 {Value}%",
                EffectType.DamageReduction => $"减免 {Value}% 伤害",
                EffectType.Invincible      => $"无敌 {Value} 回合",
                EffectType.Vulnerable      => $"施加易伤 {Value}%",
                EffectType.Weak            => $"施加虚弱 {Value}%",
                EffectType.Stun            => $"眩晕 {Value} 回合",
                EffectType.Silence         => $"沉默 {Value} 回合",
                EffectType.Slow            => $"迟缓 {Value} 回合",
                EffectType.Draw            => $"抽 {Value} 张牌",
                EffectType.Discard         => $"丢弃 {Value} 张牌",
                EffectType.GainEnergy      => $"获得 {Value} 点能量",
                EffectType.Lifesteal       => $"吸血 {Value}%",
                EffectType.Thorns          => $"荆棘反伤 {Value}%",
                EffectType.ArmorOnHit      => $"受击获得 {Value} 护甲",
                EffectType.Pierce          => $"穿透 {Value} 点护甲",
                EffectType.DoubleStrength  => $"双倍力量 {Value} 回合",
                _ => $"{EffectType}: {Value}"
            };

            if (Duration > 0)
                desc += $"（持续 {Duration} 回合）";

            if (AppliesBuff && BuffType != BuffType.Unknown)
                desc += $" [Buff:{BuffType}]";

            if (IsDelayed)
                desc += " [延迟]";

            return desc;
        }

        /// <summary>深复制</summary>
        public EffectEditData Clone()
        {
            return new EffectEditData
            {
                EffectType        = EffectType,
                Value             = Value,
                Duration          = Duration,
                TargetOverride    = TargetOverride,
                IsDelayed         = IsDelayed,
                TriggerCondition  = TriggerCondition,
                AppliesBuff       = AppliesBuff,
                BuffType          = BuffType,
                BuffStackRule     = BuffStackRule,
                IsBuffDispellable = IsBuffDispellable,
                Priority          = Priority,
                SubPriority       = SubPriority,
                FoldoutExpanded   = true,
                BuffSectionExpanded = BuffSectionExpanded
            };
        }
    }
}