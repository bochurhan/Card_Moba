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

        /// <summary>标签列表：["Damage", "Defense", "Exhaust"] 等</summary>
        public List<string> tags;

        /// <summary>能量消耗</summary>
        public int energyCost;

        /// <summary>稀有度：1=普通, 2=稀有, 3=史诗, 4=传说</summary>
        public int rarity;

        /// <summary>持续回合数（0=即时）</summary>
        public int duration;

        /// <summary>关联的效果ID列表：["E1001-1", "E1001-2"]</summary>
        public List<string> effectIds;
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
    /// </summary>
    [Serializable]
    public class EffectJsonData
    {
        /// <summary>效果唯一ID（如 "E1001-1"）</summary>
        public string effectId;

        /// <summary>效果类型编号（决定结算层和行为）</summary>
        public int effectType;

        /// <summary>效果数值</summary>
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
    }
}
