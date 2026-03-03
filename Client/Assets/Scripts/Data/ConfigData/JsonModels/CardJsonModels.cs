using System;
using System.Collections.Generic;

namespace CardMoba.Client.Data.ConfigData.JsonModels
{
    /// <summary>
    /// JSON 配置文件反序列化模型。
    /// 这些类用于 Unity JsonUtility 将 JSON 文本转换为 C# 对象。
    /// 转换后会进一步映射为业务层的 CardConfig / CardEffect。
    /// </summary>

    // ══════════════════════════════════════════════════════════════
    // Cards.json 结构
    // ══════════════════════════════════════════════════════════════

    /// <summary>
    /// cards.json 根结构
    /// </summary>
    [Serializable]
    public class CardsFileData
    {
        /// <summary>配置版本号</summary>
        public string version;

        /// <summary>所有卡牌数据列表</summary>
        public List<CardJsonData> cards;
    }

    /// <summary>
    /// 单张卡牌的 JSON 数据（DTO）
    /// </summary>
    [Serializable]
    public class CardJsonData
    {
        /// <summary>卡牌唯一ID</summary>
        public int cardId;

        /// <summary>卡牌名称</summary>
        public string cardName;

        /// <summary>卡牌描述文本</summary>
        public string description;

        /// <summary>双轨类型："Instant" 或 "Plan"</summary>
        public string trackType;

        /// <summary>目标类型："Self", "CurrentEnemy", "AllEnemies" 等</summary>
        public string targetType;

        /// <summary>所属职业："Universal", "Warrior", "Assassin", "Mage", "Support", "Tank"</summary>
        public string heroClass;

        /// <summary>标签列表：["Damage", "Defense", "Exhaust"] 等</summary>
        public List<string> tags;

        /// <summary>能量消耗</summary>
        public int energyCost;

        /// <summary>稀有度：1=普通, 2=稀有, 3=史诗, 4=传说</summary>
        public int rarity;

        /// <summary>持续回合数（0=即时）</summary>
        public int duration;

        /// <summary>关联的效果ID列表（旧关联模式）：["E1001-1", "E1001-2"]</summary>
        public List<string> effectIds;

        /// <summary>内嵌效果列表（新内嵌模式，与 effectIds 二选一）</summary>
        public List<EffectJsonData> effects;
    }

    // ══════════════════════════════════════════════════════════════
    // Effects.json 结构
    // ══════════════════════════════════════════════════════════════

    /// <summary>
    /// effects.json 根结构
    /// </summary>
    [Serializable]
    public class EffectsFileData
    {
        /// <summary>配置版本号</summary>
        public string version;

        /// <summary>所有效果数据列表</summary>
        public List<EffectJsonData> effects;
    }

    /// <summary>
    /// 单个效果的 JSON 数据（DTO）
    /// V3.0 更新：新增 effectParams 支持复杂效果参数
    /// </summary>
    [Serializable]
    public class EffectJsonData
    {
        /// <summary>效果唯一ID（如 "E1001-1"）</summary>
        public string effectId;

        /// <summary>效果类型编号（决定结算层和行为）</summary>
        public int effectType;

        /// <summary>效果数值（主数值）</summary>
        public int value;

        /// <summary>持续回合数</summary>
        public int duration;

        /// <summary>目标覆盖类型（可选，空则使用卡牌默认目标）</summary>
        public string targetOverride;

        /// <summary>触发条件（可选）</summary>
        public string triggerCondition;

        /// <summary>是否为延迟效果（本回合锁定，下回合生效）</summary>
        public bool isDelayed;

        /// <summary>效果描述（调试用）</summary>
        public string description;

        // ═══════════════════════════════════════════════════════════
        // V3.0 新增字段 - 支持 Handler 模块化架构
        // ═══════════════════════════════════════════════════════════

        /// <summary>
        /// 结算层级 (0-3)
        /// 0=反制, 1=防御, 2=伤害, 3=功能
        /// 若为空则由 effectType 自动推断
        /// </summary>
        public int layer;

        /// <summary>
        /// 效果参数（用于复杂效果）
        /// 如：反伤效果 {"reflectPercent": 30}
        ///     护甲效果 {"armorValue": 20, "duration": 2}
        /// </summary>
        public EffectParams effectParams;

        /// <summary>
        /// 子效果列表（用于组合效果）
        /// 如：一张卡同时造成伤害和添加护盾
        /// </summary>
        public List<SubEffectData> subEffects;
    }

    /// <summary>
    /// 效果参数（用于复杂效果的额外配置）
    /// </summary>
    [Serializable]
    public class EffectParams
    {
        /// <summary>百分比值（用于反伤、吸血等）</summary>
        public int percent;

        /// <summary>次要数值（如额外护甲、额外伤害）</summary>
        public int secondaryValue;

        /// <summary>可反制的效果类型列表（用于反制牌）</summary>
        public List<int> counterableTypes;

        /// <summary>触发器类型（用于被动效果）</summary>
        public string triggerType;

        /// <summary>触发器参数</summary>
        public string triggerParam;
    }

    /// <summary>
    /// 子效果数据（用于组合效果）
    /// </summary>
    [Serializable]
    public class SubEffectData
    {
        /// <summary>子效果类型</summary>
        public int effectType;

        /// <summary>子效果数值</summary>
        public int value;

        /// <summary>子效果目标（可选）</summary>
        public string targetOverride;

        /// <summary>子效果持续回合</summary>
        public int duration;
    }
}
