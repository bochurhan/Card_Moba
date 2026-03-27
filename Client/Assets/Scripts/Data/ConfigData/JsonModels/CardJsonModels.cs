using System;
using System.Collections.Generic;

namespace CardMoba.Client.Data.ConfigData.JsonModels
{
    [Serializable]
    public class CardsFileData
    {
        public string version;
        public List<CardJsonData> cards;
    }

    [Serializable]
    public class CardJsonData
    {
        public int cardId;
        public string cardName;
        public string description;
        public string trackType;
        public string targetType;
        public string heroClass;
        public string effectRange;
        public string layer;
        public List<string> tags;
        public int energyCost;
        public int rarity;
        public string upgradedCardConfigId;
        public List<EffectJsonData> effects;
        public List<PlayConditionJsonData> playConditions;
    }

    [Serializable]
    public class PlayConditionJsonData
    {
        public string conditionType;
        public int threshold;
        public bool negate;
        public string failMessage;
        public string conditionBuffType;
    }

    [Serializable]
    public class EffectJsonData
    {
        public int effectType;
        public int value;
        public string valueExpression;
        public int repeatCount;
        public int duration;
        public string targetOverride;
        public string buffConfigId;
        public string generateCardConfigId;
        public string generateCardZone;
        public bool generateCardIsTemp;
        public string projectionLifetime;
        public int priority;
        public int subPriority;
        public List<EffectConditionJsonData> effectConditions;
    }

    [Serializable]
    public class EffectConditionJsonData
    {
        public string conditionType;
        public int threshold;
        public bool negate;
        public string conditionBuffType;
    }
}
