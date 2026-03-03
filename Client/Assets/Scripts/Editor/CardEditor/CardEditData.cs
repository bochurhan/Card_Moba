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

        // ─── 打出条件（PlayConditions）──────────────────────────────
        /// <summary>
        /// 卡牌打出/提交前必须全部满足的条件列表（ANY 一个不满足则阻止使用）。
        /// 对应 Shared/ConfigModels/Card/CardConfig.PlayConditions。
        /// </summary>
        public List<PlayConditionEditData> PlayConditions = new();

        // ─── 内嵌效果列表（替代旧版 EffectIds 外键）────────────────
        public List<EffectEditData> Effects = new();

        // ─── 折叠状态（编辑器 UI 专用，不序列化）───────────────────
        [NonSerialized] public bool FoldoutExpanded          = true;
        [NonSerialized] public bool PlayConditionsFoldout    = false;

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

        // ─── 执行模式（ExecutionMode）────────────────────────────────
        /// <summary>
        /// 效果执行模式：
        ///   Immediate  — 直接执行 Handler（默认）
        ///   Conditional — 满足 EffectConditions 才执行
        ///   Passive    — 打出时注册触发器，由 TriggerManager 在指定时机触发
        /// </summary>
        public EffectExecutionMode ExecutionMode = EffectExecutionMode.Immediate;

        // ─── 执行条件（Conditional 模式）────────────────────────────
        /// <summary>ExecutionMode == Conditional 时生效，所有条件 AND 判定。</summary>
        public List<EffectConditionEditData> EffectConditions = new();

        // ─── 被动触发配置（Passive 模式）────────────────────────────
        /// <summary>ExecutionMode == Passive 时：触发时机（对应 TriggerTiming 枚举整数值）</summary>
        public int  PassiveTriggerTiming = 202;   // 默认 AfterDealDamage
        /// <summary>ExecutionMode == Passive 时：触发器持续回合数（-1 表示永久跟随卡牌）</summary>
        public int  PassiveDuration      = 1;
        /// <summary>ExecutionMode == Passive 时：最大触发次数（-1 无限）</summary>
        public int  PassiveMaxTriggers   = -1;

        // ─── Buff 附加声明 ───────────────────────────────────────────
        public bool           AppliesBuff       = false;
        public BuffType       BuffType          = BuffType.Unknown;
        public BuffStackRule  BuffStackRule     = BuffStackRule.RefreshDuration;
        public bool           IsBuffDispellable = true;

        // ─── 优先级控制 ──────────────────────────────────────────────
        public int            Priority    = 500;
        public int            SubPriority = 0;

        // ─── 编辑器 UI 专用 ──────────────────────────────────────────
        [NonSerialized] public bool FoldoutExpanded        = true;
        [NonSerialized] public bool BuffSectionExpanded    = false;
        [NonSerialized] public bool ConditionsFoldout      = false;
        [NonSerialized] public bool PassiveConfigFoldout   = false;

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

        /// <summary>根据执行模式生成前缀描述</summary>
        private string GetModePrefixDescription()
        {
            return ExecutionMode switch
            {
                EffectExecutionMode.Passive     => $"[被动·{PassiveTriggerTiming}] ",
                EffectExecutionMode.Conditional => "[条件] 若满足条件: ",
                _                               => ""
            };
        }

        /// <summary>深复制</summary>
        public EffectEditData Clone()
        {
            var c = new EffectEditData
            {
                EffectType        = EffectType,
                Value             = Value,
                Duration          = Duration,
                TargetOverride    = TargetOverride,
                IsDelayed         = IsDelayed,
                TriggerCondition  = TriggerCondition,
                ExecutionMode     = ExecutionMode,
                PassiveTriggerTiming = PassiveTriggerTiming,
                PassiveDuration   = PassiveDuration,
                PassiveMaxTriggers= PassiveMaxTriggers,
                AppliesBuff       = AppliesBuff,
                BuffType          = BuffType,
                BuffStackRule     = BuffStackRule,
                IsBuffDispellable = IsBuffDispellable,
                Priority          = Priority,
                SubPriority       = SubPriority,
                FoldoutExpanded   = true,
                BuffSectionExpanded = BuffSectionExpanded
            };
            foreach (var cond in EffectConditions)
                c.EffectConditions.Add(cond.Clone());
            return c;
        }
    }

    // ════════════════════════════════════════════════════════════════
    // 打出条件编辑数据 —— 对应 CardConfig.PlayConditions[i]
    // ════════════════════════════════════════════════════════════════

    /// <summary>
    /// 卡牌打出条件：打出/提交前检查，不满足则阻止使用并显示错误提示。
    /// </summary>
    [Serializable]
    public class PlayConditionEditData
    {
        /// <summary>条件类型</summary>
        public EffectConditionType ConditionType = EffectConditionType.MyDeckIsEmpty;
        /// <summary>参考值（如"牌库张数 ≤ N"中的 N）</summary>
        public int  Threshold = 0;
        /// <summary>是否对条件取反（如"牌库不为空"变为"牌库为空"）</summary>
        public bool Negate    = false;
        /// <summary>不满足时显示的 UI 提示文本</summary>
        public string FailMessage = "";

        public PlayConditionEditData Clone() => new()
        {
            ConditionType = ConditionType,
            Threshold     = Threshold,
            Negate        = Negate,
            FailMessage   = FailMessage
        };
    }

    // ════════════════════════════════════════════════════════════════
    // 效果条件编辑数据 —— 对应 CardEffect.EffectConditions[i]
    // ════════════════════════════════════════════════════════════════

    /// <summary>
    /// 效果执行条件（ExecutionMode == Conditional 时使用）：
    /// 所有条件 AND 判定，全部通过才执行 Handler。
    /// </summary>
    [Serializable]
    public class EffectConditionEditData
    {
        /// <summary>条件类型</summary>
        public EffectConditionType ConditionType = EffectConditionType.MyDeckIsEmpty;
        /// <summary>参考值（视条件语义而定）</summary>
        public int  Threshold = 0;
        /// <summary>是否对条件取反</summary>
        public bool Negate    = false;

        public EffectConditionEditData Clone() => new()
        {
            ConditionType = ConditionType,
            Threshold     = Threshold,
            Negate        = Negate
        };
    }
}
