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
    /// 效果编辑数据 —— 直接使用 Shared 的 EffectType 枚举
    /// </summary>
    [Serializable]
    public class EffectEditData
    {
        public string EffectId = "";
        public int CardId;
        /// <summary>效果类型 —— 使用 Shared/Protocol/Enums/EffectType</summary>
        public EffectType EffectType = EffectType.Damage;
        public int Value;
        public int Duration;
        public string TargetOverride = "";
        public bool IsDelayed;
        public string Description = "";
    }
}