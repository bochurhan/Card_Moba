using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using CardMoba.ConfigModels.Card;
using CardMoba.Protocol.Enums;

namespace CardMoba.Server.Host.Config
{
    /// <summary>
    /// 服务端卡牌配置目录。
    /// 负责从 StreamingAssets 读取 cards.json，并转换为 Shared 可复用的 CardConfig。
    /// </summary>
    public sealed class ServerCardCatalog
    {
        private readonly Dictionary<int, CardConfig> _cards = new Dictionary<int, CardConfig>();

        public ServerCardCatalog(IHostEnvironment environment)
        {
            if (environment == null)
                throw new ArgumentNullException(nameof(environment));

            string path = Path.GetFullPath(Path.Combine(
                environment.ContentRootPath,
                "..",
                "..",
                "..",
                "Client",
                "Assets",
                "StreamingAssets",
                "Config",
                "cards.json"));

            if (!File.Exists(path))
                throw new FileNotFoundException($"未找到服务端卡牌配置文件：{path}");

            Load(path);
        }

        public IReadOnlyCollection<CardConfig> AllCards => _cards.Values;

        public CardConfig? GetCard(string configId)
        {
            if (string.IsNullOrWhiteSpace(configId))
                return null;

            return int.TryParse(configId, out var numericId)
                ? GetCard(numericId)
                : null;
        }

        public CardConfig? GetCard(int cardId)
        {
            return _cards.TryGetValue(cardId, out var card) ? card : null;
        }

        private void Load(string path)
        {
            var options = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
            };
            var json = File.ReadAllText(path);
            var data = JsonSerializer.Deserialize<CardsFileJson>(json, options);
            if (data?.Cards == null)
                throw new InvalidOperationException("cards.json 解析失败，未读取到 cards 列表。");

            foreach (var cardData in data.Cards)
            {
                if (cardData.CardId <= 0)
                    continue;

                var config = ConvertToCardConfig(cardData);
                _cards[config.CardId] = config;
            }
        }

        private static CardConfig ConvertToCardConfig(CardJson card)
        {
            var config = new CardConfig
            {
                CardId = card.CardId,
                CardName = card.CardName ?? string.Empty,
                Description = card.Description ?? string.Empty,
                TrackType = ParseEnum(card.TrackType, CardTrackType.Instant),
                TargetType = ParseEnum(card.TargetType, CardTargetType.Self),
                HeroClass = ParseEnum(card.HeroClass, HeroClass.Universal),
                EffectRange = ParseEnum(card.EffectRange, EffectRange.SingleEnemy),
                Layer = ParseEnum(card.Layer, SettlementLayer.DamageTrigger),
                Tags = ParseTags(card.Tags),
                EnergyCost = card.EnergyCost,
                UpgradedCardConfigId = card.UpgradedCardConfigId ?? string.Empty,
                Rarity = card.Rarity > 0 ? card.Rarity : 1,
            };

            if (card.PlayConditions != null)
            {
                foreach (var condition in card.PlayConditions)
                {
                    config.PlayConditions.Add(new EffectCondition
                    {
                        ConditionType = ParseEnum(condition.ConditionType, EffectConditionType.MyDeckIsEmpty),
                        ConditionValue = condition.Threshold,
                        Negate = condition.Negate,
                        ConditionBuffType = ParseEnum(condition.ConditionBuffType, BuffType.Unknown),
                    });
                }
            }

            if (card.Effects != null)
            {
                foreach (var effect in card.Effects)
                {
                    config.Effects.Add(new CardEffect
                    {
                        EffectType = (EffectType)effect.EffectType,
                        Value = effect.Value,
                        ValueExpression = effect.ValueExpression ?? string.Empty,
                        BuffConfigId = effect.BuffConfigId ?? string.Empty,
                        GenerateCardConfigId = effect.GenerateCardConfigId ?? string.Empty,
                        GenerateCardZone = string.IsNullOrWhiteSpace(effect.GenerateCardZone) ? "Hand" : effect.GenerateCardZone,
                        GenerateCardIsTemp = effect.GenerateCardIsTemp,
                        ProjectionLifetime = effect.ProjectionLifetime ?? string.Empty,
                        Duration = effect.Duration,
                        RepeatCount = effect.RepeatCount > 0 ? effect.RepeatCount : 1,
                        TargetOverride = string.IsNullOrWhiteSpace(effect.TargetOverride)
                            ? null
                            : ParseCardTargetOverride(effect.TargetOverride),
                        Priority = effect.Priority == 0 ? 500 : effect.Priority,
                        SubPriority = effect.SubPriority,
                    });

                    var appendedEffect = config.Effects[config.Effects.Count - 1];
                    if (effect.EffectConditions != null)
                    {
                        foreach (var condition in effect.EffectConditions)
                        {
                            appendedEffect.EffectConditions.Add(new EffectCondition
                            {
                                ConditionType = ParseEnum(condition.ConditionType, EffectConditionType.MyDeckIsEmpty),
                                ConditionValue = condition.Threshold,
                                Negate = condition.Negate,
                                ConditionBuffType = ParseEnum(condition.ConditionBuffType, BuffType.Unknown),
                            });
                        }
                    }
                }
            }

            return config;
        }

        private static CardTag ParseTags(List<string>? tags)
        {
            if (tags == null || tags.Count == 0)
                return CardTag.None;

            var result = CardTag.None;
            foreach (var tag in tags)
            {
                if (Enum.TryParse<CardTag>(tag, true, out var parsed))
                    result |= parsed;
            }

            return result;
        }

        private static T ParseEnum<T>(string? raw, T defaultValue) where T : struct
        {
            if (string.IsNullOrWhiteSpace(raw))
                return defaultValue;

            return Enum.TryParse<T>(raw, true, out var parsed) ? parsed : defaultValue;
        }

        private static CardTargetType ParseCardTargetOverride(string raw)
        {
            return CardTargetTypeParser.TryParse(raw, out var parsed)
                ? parsed
                : CardTargetType.Self;
        }

        private sealed class CardsFileJson
        {
            public string Version { get; set; } = string.Empty;
            public List<CardJson> Cards { get; set; } = new List<CardJson>();
        }

        private sealed class CardJson
        {
            public int CardId { get; set; }
            public string? CardName { get; set; }
            public string? Description { get; set; }
            public string? TrackType { get; set; }
            public string? TargetType { get; set; }
            public string? HeroClass { get; set; }
            public string? EffectRange { get; set; }
            public string? Layer { get; set; }
            public List<string>? Tags { get; set; }
            public int EnergyCost { get; set; }
            public int Rarity { get; set; }
            public string? UpgradedCardConfigId { get; set; }
            public List<EffectJson>? Effects { get; set; }
            public List<ConditionJson>? PlayConditions { get; set; }
        }

        private sealed class EffectJson
        {
            public int EffectType { get; set; }
            public int Value { get; set; }
            public string? ValueExpression { get; set; }
            public int RepeatCount { get; set; }
            public int Duration { get; set; }
            public string? TargetOverride { get; set; }
            public string? BuffConfigId { get; set; }
            public string? GenerateCardConfigId { get; set; }
            public string? GenerateCardZone { get; set; }
            public bool GenerateCardIsTemp { get; set; }
            public string? ProjectionLifetime { get; set; }
            public int Priority { get; set; }
            public int SubPriority { get; set; }
            public List<ConditionJson>? EffectConditions { get; set; }
        }

        private sealed class ConditionJson
        {
            public string? ConditionType { get; set; }
            public int Threshold { get; set; }
            public bool Negate { get; set; }
            public string? ConditionBuffType { get; set; }
        }
    }
}
