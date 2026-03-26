using System;
using System.Collections.Generic;
using System.IO;
using CardMoba.Client.Data.ConfigData.JsonModels;
using CardMoba.ConfigModels.Card;
using CardMoba.Protocol.Enums;
using UnityEngine;

namespace CardMoba.Client.Data.ConfigData
{
    /// <summary>
    /// 运行时卡牌配置管理器。
    /// 只读取当前契约的 cards.json 单表结构。
    /// </summary>
    public class CardConfigManager
    {
        private static CardConfigManager _instance;

        public static CardConfigManager Instance => _instance ??= new CardConfigManager();

        private CardConfigManager()
        {
        }

        private Dictionary<int, CardConfig> _cardDict;
        private bool _isLoaded;
        private string _version = "0.0.0";

        public bool IsLoaded => _isLoaded;

        public string Version => _version;

        public int CardCount => _cardDict?.Count ?? 0;

        public IReadOnlyDictionary<int, CardConfig> AllCards => _cardDict;

        public void LoadAll()
        {
            if (_isLoaded)
            {
                Debug.LogWarning("[CardConfigManager] 配置已加载，跳过重复加载。");
                return;
            }

            _cardDict = new Dictionary<int, CardConfig>();

            try
            {
                LoadCards();
                _isLoaded = true;
                Debug.Log($"[CardConfigManager] 配置加载完成。版本: {_version}, 卡牌: {_cardDict.Count}");
            }
            catch (Exception ex)
            {
                _isLoaded = false;
                Debug.LogError($"[CardConfigManager] 配置加载失败: {ex.Message}\n{ex.StackTrace}");
            }
        }

        public void Unload()
        {
            _cardDict?.Clear();
            _cardDict = null;
            _isLoaded = false;
            _version = "0.0.0";
            Debug.Log("[CardConfigManager] 配置已卸载。");
        }

        public void Reload()
        {
            Unload();
            LoadAll();
        }

        public CardConfig GetCard(int cardId)
        {
            if (!EnsureLoaded())
                return null;

            return _cardDict.TryGetValue(cardId, out var config) ? config : null;
        }

        public CardConfig CloneCard(int cardId)
        {
            var original = GetCard(cardId);
            if (original == null)
                return null;

            return new CardConfig
            {
                CardId = original.CardId,
                CardName = original.CardName,
                Description = original.Description,
                TrackType = original.TrackType,
                HeroClass = original.HeroClass,
                Tags = original.Tags,
                TargetType = original.TargetType,
                EffectRange = original.EffectRange,
                Layer = original.Layer,
                EnergyCost = original.EnergyCost,
                Rarity = original.Rarity,
                PlayConditions = CloneConditions(original.PlayConditions),
                Effects = CloneEffects(original.Effects)
            };
        }

        public List<CardConfig> GetCardsByTag(CardTag tag)
        {
            if (!EnsureLoaded())
                return new List<CardConfig>();

            var result = new List<CardConfig>();
            foreach (var card in _cardDict.Values)
            {
                if (card.HasTag(tag))
                    result.Add(card);
            }

            return result;
        }

        public List<CardConfig> GetCardsByRarity(int rarity)
        {
            if (!EnsureLoaded())
                return new List<CardConfig>();

            var result = new List<CardConfig>();
            foreach (var card in _cardDict.Values)
            {
                if (card.Rarity == rarity)
                    result.Add(card);
            }

            return result;
        }

        public List<CardConfig> GetCardsByTrackType(CardTrackType trackType)
        {
            if (!EnsureLoaded())
                return new List<CardConfig>();

            var result = new List<CardConfig>();
            foreach (var card in _cardDict.Values)
            {
                if (card.TrackType == trackType)
                    result.Add(card);
            }

            return result;
        }

        public bool HasCard(int cardId)
        {
            return _isLoaded && _cardDict.ContainsKey(cardId);
        }

        private void LoadCards()
        {
            string json = ReadJsonFile("cards.json");
            if (string.IsNullOrEmpty(json))
            {
                Debug.LogWarning("[CardConfigManager] cards.json 为空或不存在，跳过卡牌加载。");
                return;
            }

            var data = JsonUtility.FromJson<CardsFileData>(json);
            if (data?.cards == null)
            {
                Debug.LogWarning("[CardConfigManager] cards.json 解析失败。");
                return;
            }

            _version = string.IsNullOrWhiteSpace(data.version) ? "1.0.0" : data.version;

            foreach (var cardData in data.cards)
            {
                if (cardData.cardId <= 0)
                {
                    Debug.LogWarning($"[CardConfigManager] 发现无效 cardId={cardData.cardId}，已跳过。");
                    continue;
                }

                var config = ConvertToCardConfig(cardData);
                if (_cardDict.ContainsKey(config.CardId))
                    Debug.LogWarning($"[CardConfigManager] 发现重复 CardId={config.CardId}，后者将覆盖前者。");

                _cardDict[config.CardId] = config;
            }
        }

        private string ReadJsonFile(string fileName)
        {
            string path = Path.Combine(Application.streamingAssetsPath, "Config", fileName);

#if UNITY_ANDROID && !UNITY_EDITOR
            using (var request = UnityEngine.Networking.UnityWebRequest.Get(path))
            {
                request.SendWebRequest();
                while (!request.isDone) { }

                if (request.result == UnityEngine.Networking.UnityWebRequest.Result.Success)
                    return request.downloadHandler.text;

                Debug.LogError($"[CardConfigManager] 无法读取配置文件: {path}, 错误: {request.error}");
                return null;
            }
#else
            if (!File.Exists(path))
            {
                Debug.LogWarning($"[CardConfigManager] 配置文件不存在: {path}");
                return null;
            }

            return File.ReadAllText(path);
#endif
        }

        private CardConfig ConvertToCardConfig(CardJsonData data)
        {
            var config = new CardConfig
            {
                CardId = data.cardId,
                CardName = data.cardName ?? string.Empty,
                Description = data.description ?? string.Empty,
                TrackType = ParseEnum(data.trackType, CardTrackType.Instant),
                TargetType = ParseEnum(data.targetType, CardTargetType.Self),
                HeroClass = ParseEnum(data.heroClass, HeroClass.Universal),
                EffectRange = ParseEnum(data.effectRange, EffectRange.SingleEnemy),
                Layer = ParseEnum(data.layer, SettlementLayer.DamageTrigger),
                Tags = ParseTags(data.tags),
                EnergyCost = data.energyCost,
                Rarity = data.rarity > 0 ? data.rarity : 1,
                Effects = new List<CardEffect>(),
                PlayConditions = new List<EffectCondition>()
            };

            if (data.playConditions != null)
            {
                foreach (var conditionData in data.playConditions)
                {
                    config.PlayConditions.Add(ConvertToEffectCondition(
                        conditionData.conditionType,
                        conditionData.threshold,
                        conditionData.negate,
                        conditionData.conditionBuffType));
                }
            }

            if (data.effects != null)
            {
                foreach (var effectData in data.effects)
                    config.Effects.Add(ConvertToCardEffect(effectData));
            }

            return config;
        }

        private CardEffect ConvertToCardEffect(EffectJsonData data)
        {
            var effect = new CardEffect
            {
                EffectType = (EffectType)data.effectType,
                Value = data.value,
                ValueExpression = data.valueExpression ?? string.Empty,
                BuffConfigId = data.buffConfigId ?? string.Empty,
                GenerateCardConfigId = data.generateCardConfigId ?? string.Empty,
                GenerateCardZone = string.IsNullOrWhiteSpace(data.generateCardZone) ? "Hand" : data.generateCardZone,
                GenerateCardIsTemp = data.generateCardIsTemp,
                Duration = data.duration,
                RepeatCount = data.repeatCount > 0 ? data.repeatCount : 1,
                Priority = data.priority == 0 ? 500 : data.priority,
                SubPriority = data.subPriority,
                EffectConditions = new List<EffectCondition>()
            };

            if (!string.IsNullOrWhiteSpace(data.targetOverride))
                effect.TargetOverride = ParseEnum(data.targetOverride, CardTargetType.Self);

            if (data.effectConditions != null)
            {
                foreach (var conditionData in data.effectConditions)
                {
                    effect.EffectConditions.Add(ConvertToEffectCondition(
                        conditionData.conditionType,
                        conditionData.threshold,
                        conditionData.negate,
                        conditionData.conditionBuffType));
                }
            }

            return effect;
        }

        private static EffectCondition ConvertToEffectCondition(
            string conditionType,
            int threshold,
            bool negate,
            string conditionBuffType)
        {
            var condition = new EffectCondition
            {
                ConditionType = ParseEnumStatic(conditionType, EffectConditionType.MyDeckIsEmpty),
                ConditionValue = threshold,
                Negate = negate
            };

            if (!string.IsNullOrWhiteSpace(conditionBuffType))
                condition.ConditionBuffType = ParseEnumStatic(conditionBuffType, BuffType.Unknown);

            return condition;
        }

        private CardTag ParseTags(List<string> tagNames)
        {
            if (tagNames == null || tagNames.Count == 0)
                return CardTag.None;

            CardTag result = CardTag.None;
            foreach (var name in tagNames)
            {
                if (Enum.TryParse<CardTag>(name, true, out var tag))
                    result |= tag;
                else
                    Debug.LogWarning($"[CardConfigManager] 未知标签名: {name}");
            }

            return result;
        }

        private T ParseEnum<T>(string value, T defaultValue) where T : struct
        {
            if (string.IsNullOrWhiteSpace(value))
                return defaultValue;

            if (Enum.TryParse<T>(value, true, out var result))
                return result;

            Debug.LogWarning($"[CardConfigManager] 无法解析枚举 {typeof(T).Name}: {value}，将使用默认值 {defaultValue}");
            return defaultValue;
        }

        private static T ParseEnumStatic<T>(string value, T defaultValue) where T : struct
        {
            if (string.IsNullOrWhiteSpace(value))
                return defaultValue;

            return Enum.TryParse<T>(value, true, out var result) ? result : defaultValue;
        }

        private bool EnsureLoaded()
        {
            if (_isLoaded)
                return true;

            Debug.LogError("[CardConfigManager] 配置尚未加载，请先调用 LoadAll()");
            return false;
        }

        private static List<CardEffect> CloneEffects(List<CardEffect> effects)
        {
            var result = new List<CardEffect>();
            if (effects == null)
                return result;

            foreach (var effect in effects)
            {
                result.Add(new CardEffect
                {
                    EffectType = effect.EffectType,
                    Value = effect.Value,
                    ValueExpression = effect.ValueExpression,
                    BuffConfigId = effect.BuffConfigId,
                    GenerateCardConfigId = effect.GenerateCardConfigId,
                    GenerateCardZone = effect.GenerateCardZone,
                    GenerateCardIsTemp = effect.GenerateCardIsTemp,
                    Duration = effect.Duration,
                    RepeatCount = effect.RepeatCount,
                    TargetOverride = effect.TargetOverride,
                    EffectConditions = CloneConditions(effect.EffectConditions),
                    Priority = effect.Priority,
                    SubPriority = effect.SubPriority
                });
            }

            return result;
        }

        private static List<EffectCondition> CloneConditions(List<EffectCondition> conditions)
        {
            var result = new List<EffectCondition>();
            if (conditions == null)
                return result;

            foreach (var condition in conditions)
            {
                result.Add(new EffectCondition
                {
                    ConditionType = condition.ConditionType,
                    ConditionValue = condition.ConditionValue,
                    ConditionBuffType = condition.ConditionBuffType,
                    Negate = condition.Negate
                });
            }

            return result;
        }
    }
}
