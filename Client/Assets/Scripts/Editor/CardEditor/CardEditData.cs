using System;
using System.Collections.Generic;
using CardMoba.Protocol.Enums;

namespace CardMoba.Client.Editor.CardEditor
{
    /// <summary>
    /// 卡牌编辑数据（编辑器内部使用）
    /// </summary>
    [Serializable]
    public class CardEditData
    {
        public int CardId;
        public string CardName = "";
        public string Description = "";
        public CardTrackType TrackType = CardTrackType.Instant;
        public CardTargetType TargetType = CardTargetType.CurrentEnemy;
        public int EnergyCost = 1;
        public int Rarity = 1;
        
        // 标签使用 bool 数组便于复选框编辑
        public bool TagDamage;
        public bool TagDefense;
        public bool TagCounter;
        public bool TagBuff;
        public bool TagDebuff;
        public bool TagResource;
        public bool TagExhaust;
        public bool TagCrossLane;  // 跨路生效
        
        // 关联的效果ID列表
        public List<string> EffectIds = new();

        /// <summary>
        /// 获取 CardTag 枚举值
        /// </summary>
        public CardTag GetTags()
        {
            CardTag result = 0;
            if (TagDamage) result |= CardTag.Damage;
            if (TagDefense) result |= CardTag.Defense;
            if (TagCounter) result |= CardTag.Counter;
            // 注意：CardTag 枚举可能没有所有标签，需要根据实际情况扩展
            return result;
        }

        /// <summary>
        /// 从 CardTag 枚举设置标签
        /// </summary>
        public void SetTags(CardTag tags)
        {
            TagDamage = (tags & CardTag.Damage) != 0;
            TagDefense = (tags & CardTag.Defense) != 0;
            TagCounter = (tags & CardTag.Counter) != 0;
        }

        /// <summary>
        /// 获取标签列表（用于JSON序列化）
        /// </summary>
        public List<string> GetTagList()
        {
            var list = new List<string>();
            if (TagDamage) list.Add("Damage");
            if (TagDefense) list.Add("Defense");
            if (TagCounter) list.Add("Counter");
            if (TagBuff) list.Add("Buff");
            if (TagDebuff) list.Add("Debuff");
            if (TagResource) list.Add("Resource");
            if (TagExhaust) list.Add("Exhaust");
            if (TagCrossLane) list.Add("CrossLane");
            return list;
        }

        /// <summary>
        /// 从标签名称列表设置
        /// </summary>
        public void SetTagList(List<string> tags)
        {
            TagDamage = tags.Contains("Damage");
            TagDefense = tags.Contains("Defense");
            TagCounter = tags.Contains("Counter");
            TagBuff = tags.Contains("Buff");
            TagDebuff = tags.Contains("Debuff");
            TagResource = tags.Contains("Resource");
            TagExhaust = tags.Contains("Exhaust");
            TagCrossLane = tags.Contains("CrossLane");
        }
    }

    /// <summary>
    /// 效果编辑数据
    /// </summary>
    [Serializable]
    public class EffectEditData
    {
        public string EffectId = "";
        public int CardId;
        public EffectTypeEnum EffectType = EffectTypeEnum.DealDamage;
        public int Value;
        public int Duration;
        public string TargetOverride = "";
        public bool IsDelayed;
        public string Description = "";
    }

    /// <summary>
    /// 效果类型枚举（带中文显示名）
    /// </summary>
    public enum EffectTypeEnum
    {
        [InspectorName("造成伤害")]
        DealDamage = 201,
        
        [InspectorName("获得护盾")]
        GainShield = 101,
        
        [InspectorName("获得护甲")]
        GainArmor = 102,
        
        [InspectorName("回复生命")]
        Heal = 301,
        
        [InspectorName("吸血(%)")]
        Lifesteal = 302,
        
        [InspectorName("获得力量")]
        GainStrength = 111,
        
        [InspectorName("反伤(%)")]
        Thorns = 121,
        
        [InspectorName("易伤(%)")]
        Vulnerable = 221,
        
        [InspectorName("反制伤害")]
        CounterFirstDamage = 401,
        
        [InspectorName("抽牌")]
        DrawCard = 501,
        
        [InspectorName("获得能量")]
        GainEnergy = 502,
    }

    /// <summary>
    /// InspectorName 属性（用于枚举显示名）
    /// </summary>
    [AttributeUsage(AttributeTargets.Field)]
    public class InspectorNameAttribute : Attribute
    {
        public string DisplayName { get; }
        public InspectorNameAttribute(string name) => DisplayName = name;
    }
}
