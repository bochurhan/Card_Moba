using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using CardMoba.ConfigModels.Card;
using CardMoba.Protocol.Enums;
using CardMoba.Client.Data.ConfigData.JsonModels;

namespace CardMoba.Client.Data.ConfigData
{
    /// <summary>
    /// 卡牌配置管理器（单例）。
    /// 
    /// 职责：
    /// - 从 StreamingAssets/Config/ 加载 JSON 配置文件
    /// - 将 JSON 数据转换为业务层的 CardConfig / CardEffect 对象
    /// - 提供按 ID、标签等条件查询卡牌配置的接口
    /// 
    /// 使用方式：
    /// 1. 游戏启动时调用 CardConfigManager.Instance.LoadAll()
    /// 2. 运行时通过 GetCard(cardId) 获取卡牌配置
    /// 
    /// 注意：
    /// - 配置数据是只读的，不要直接修改返回的对象
    /// - 如需修改（如构建卡组），请使用 CloneCard() 获取副本
    /// </summary>
    public class CardConfigManager
    {
        // ══════════════════════════════════════════════════════════════
        // 单例模式
        // ══════════════════════════════════════════════════════════════

        private static CardConfigManager _instance;

        /// <summary>获取单例实例</summary>
        public static CardConfigManager Instance => _instance ??= new CardConfigManager();

        /// <summary>私有构造函数，防止外部实例化</summary>
        private CardConfigManager() { }

        // ══════════════════════════════════════════════════════════════
        // 数据缓存
        // ══════════════════════════════════════════════════════════════

        /// <summary>卡牌配置字典：CardId → CardConfig</summary>
        private Dictionary<int, CardConfig> _cardDict;

        /// <summary>效果配置字典：EffectId → CardEffect</summary>
        private Dictionary<string, CardEffect> _effectDict;

        /// <summary>是否已加载</summary>
        private bool _isLoaded = false;

        /// <summary>配置版本号</summary>
        private string _version = "0.0.0";

        // ══════════════════════════════════════════════════════════════
        // 公开属性
        // ══════════════════════════════════════════════════════════════

        /// <summary>是否已完成加载</summary>
        public bool IsLoaded => _isLoaded;

        /// <summary>当前配置版本</summary>
        public string Version => _version;

        /// <summary>已加载的卡牌数量</summary>
        public int CardCount => _cardDict?.Count ?? 0;

        /// <summary>已加载的效果数量</summary>
        public int EffectCount => _effectDict?.Count ?? 0;

        /// <summary>所有卡牌配置（只读）</summary>
        public IReadOnlyDictionary<int, CardConfig> AllCards => _cardDict;

        // ══════════════════════════════════════════════════════════════
        // 初始化方法
        // ══════════════════════════════════════════════════════════════

        /// <summary>
        /// 加载所有配置文件。
        /// 应在游戏启动时调用一次（如 GameEntry.Start）。
        /// </summary>
        public void LoadAll()
        {
            if (_isLoaded)
            {
                Debug.LogWarning("[CardConfigManager] 配置已加载，跳过重复加载。如需重新加载请先调用 Unload()");
                return;
            }

            Debug.Log("[CardConfigManager] 开始加载配置...");

            _cardDict = new Dictionary<int, CardConfig>();
            _effectDict = new Dictionary<string, CardEffect>();

            try
            {
                // 1. 先加载效果表（卡牌表依赖效果表）
                LoadEffects();

                // 2. 再加载卡牌表
                LoadCards();

                _isLoaded = true;
                Debug.Log($"[CardConfigManager] 配置加载完成！版本: {_version}, 卡牌: {_cardDict.Count} 张, 效果: {_effectDict.Count} 个");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[CardConfigManager] 配置加载失败: {ex.Message}\n{ex.StackTrace}");
                _isLoaded = false;
            }
        }

        /// <summary>
        /// 卸载配置，释放内存。
        /// </summary>
        public void Unload()
        {
            _cardDict?.Clear();
            _effectDict?.Clear();
            _cardDict = null;
            _effectDict = null;
            _isLoaded = false;
            _version = "0.0.0";
            Debug.Log("[CardConfigManager] 配置已卸载");
        }

        /// <summary>
        /// 重新加载配置。
        /// </summary>
        public void Reload()
        {
            Unload();
            LoadAll();
        }

        // ══════════════════════════════════════════════════════════════
        // 查询方法
        // ══════════════════════════════════════════════════════════════

        /// <summary>
        /// 根据 ID 获取卡牌配置。
        /// </summary>
        /// <param name="cardId">卡牌ID</param>
        /// <returns>卡牌配置，若不存在返回 null</returns>
        public CardConfig GetCard(int cardId)
        {
            if (!EnsureLoaded()) return null;
            return _cardDict.TryGetValue(cardId, out var config) ? config : null;
        }

        /// <summary>
        /// 根据 ID 获取卡牌配置的副本（可安全修改）。
        /// </summary>
        /// <param name="cardId">卡牌ID</param>
        /// <returns>卡牌配置副本，若不存在返回 null</returns>
        public CardConfig CloneCard(int cardId)
        {
            var original = GetCard(cardId);
            if (original == null) return null;

            return new CardConfig
            {
                CardId = original.CardId,
                CardName = original.CardName,
                Description = original.Description,
                TrackType = original.TrackType,
                TargetType = original.TargetType,
                Tags = original.Tags,
                EnergyCost = original.EnergyCost,
                Duration = original.Duration,
                Rarity = original.Rarity,
                Effects = new List<CardEffect>(original.Effects) // 浅拷贝效果列表
            };
        }

        /// <summary>
        /// 根据 ID 获取效果配置。
        /// </summary>
        /// <param name="effectId">效果ID（如 "E1001-1"）</param>
        /// <returns>效果配置，若不存在返回 null</returns>
        public CardEffect GetEffect(string effectId)
        {
            if (!EnsureLoaded()) return null;
            return _effectDict.TryGetValue(effectId, out var effect) ? effect : null;
        }

        /// <summary>
        /// 获取所有包含指定标签的卡牌。
        /// </summary>
        /// <param name="tag">要筛选的标签</param>
        /// <returns>符合条件的卡牌列表</returns>
        public List<CardConfig> GetCardsByTag(CardTag tag)
        {
            if (!EnsureLoaded()) return new List<CardConfig>();

            var result = new List<CardConfig>();
            foreach (var card in _cardDict.Values)
            {
                if (card.HasTag(tag))
                    result.Add(card);
            }
            return result;
        }

        /// <summary>
        /// 获取指定稀有度的所有卡牌。
        /// </summary>
        /// <param name="rarity">稀有度等级</param>
        /// <returns>符合条件的卡牌列表</returns>
        public List<CardConfig> GetCardsByRarity(int rarity)
        {
            if (!EnsureLoaded()) return new List<CardConfig>();

            var result = new List<CardConfig>();
            foreach (var card in _cardDict.Values)
            {
                if (card.Rarity == rarity)
                    result.Add(card);
            }
            return result;
        }

        /// <summary>
        /// 获取指定轨道类型的所有卡牌。
        /// </summary>
        /// <param name="trackType">轨道类型</param>
        /// <returns>符合条件的卡牌列表</returns>
        public List<CardConfig> GetCardsByTrackType(CardTrackType trackType)
        {
            if (!EnsureLoaded()) return new List<CardConfig>();

            var result = new List<CardConfig>();
            foreach (var card in _cardDict.Values)
            {
                if (card.TrackType == trackType)
                    result.Add(card);
            }
            return result;
        }

        /// <summary>
        /// 检查卡牌是否存在。
        /// </summary>
        public bool HasCard(int cardId)
        {
            return _isLoaded && _cardDict.ContainsKey(cardId);
        }

        // ══════════════════════════════════════════════════════════════
        // 私有方法 - 加载逻辑
        // ══════════════════════════════════════════════════════════════

        /// <summary>
        /// 加载效果配置表。
        /// </summary>
        private void LoadEffects()
        {
            string json = ReadJsonFile("effects.json");
            if (string.IsNullOrEmpty(json))
            {
                Debug.LogWarning("[CardConfigManager] effects.json 为空或不存在，跳过效果加载");
                return;
            }

            var data = JsonUtility.FromJson<EffectsFileData>(json);
            if (data == null || data.effects == null)
            {
                Debug.LogWarning("[CardConfigManager] effects.json 解析失败");
                return;
            }

            foreach (var effectData in data.effects)
            {
                if (string.IsNullOrEmpty(effectData.effectId))
                {
                    Debug.LogWarning("[CardConfigManager] 发现空 effectId，已跳过");
                    continue;
                }

                var effect = ConvertToCardEffect(effectData);
                _effectDict[effectData.effectId] = effect;
            }

            Debug.Log($"[CardConfigManager] 效果表加载完成: {_effectDict.Count} 个效果");
        }

        /// <summary>
        /// 加载卡牌配置表。
        /// </summary>
        private void LoadCards()
        {
            string json = ReadJsonFile("cards.json");
            if (string.IsNullOrEmpty(json))
            {
                Debug.LogWarning("[CardConfigManager] cards.json 为空或不存在，跳过卡牌加载");
                return;
            }

            var data = JsonUtility.FromJson<CardsFileData>(json);
            if (data == null || data.cards == null)
            {
                Debug.LogWarning("[CardConfigManager] cards.json 解析失败");
                return;
            }

            _version = data.version ?? "1.0.0";

            foreach (var cardData in data.cards)
            {
                if (cardData.cardId <= 0)
                {
                    Debug.LogWarning($"[CardConfigManager] 发现无效 cardId: {cardData.cardId}，已跳过");
                    continue;
                }

                // 转换基础属性
                var config = ConvertToCardConfig(cardData);

                // 关联效果
                config.Effects = new List<CardEffect>();
                if (cardData.effectIds != null)
                {
                    foreach (var effectId in cardData.effectIds)
                    {
                        if (_effectDict.TryGetValue(effectId, out var effect))
                        {
                            config.Effects.Add(effect);
                        }
                        else
                        {
                            Debug.LogWarning($"[CardConfigManager] 卡牌 {config.CardId}({config.CardName}) 引用了不存在的效果: {effectId}");
                        }
                    }
                }

                // 检查重复
                if (_cardDict.ContainsKey(config.CardId))
                {
                    Debug.LogWarning($"[CardConfigManager] 发现重复 CardId: {config.CardId}，后者覆盖前者");
                }

                _cardDict[config.CardId] = config;
            }

            Debug.Log($"[CardConfigManager] 卡牌表加载完成: {_cardDict.Count} 张卡牌");
        }

        // ══════════════════════════════════════════════════════════════
        // 私有方法 - 文件读取
        // ══════════════════════════════════════════════════════════════

        /// <summary>
        /// 从 StreamingAssets/Config/ 读取 JSON 文件。
        /// </summary>
        /// <param name="fileName">文件名（如 "cards.json"）</param>
        /// <returns>JSON 文本内容</returns>
        private string ReadJsonFile(string fileName)
        {
            string path = Path.Combine(Application.streamingAssetsPath, "Config", fileName);

#if UNITY_ANDROID && !UNITY_EDITOR
            // Android 平台需要使用 UnityWebRequest
            // 同步读取（注意：正式项目建议改为异步）
            using (var request = UnityEngine.Networking.UnityWebRequest.Get(path))
            {
                request.SendWebRequest();
                while (!request.isDone) { /* 阻塞等待 */ }

                if (request.result == UnityEngine.Networking.UnityWebRequest.Result.Success)
                {
                    return request.downloadHandler.text;
                }
                else
                {
                    Debug.LogError($"[CardConfigManager] 无法读取配置文件: {path}, 错误: {request.error}");
                    return null;
                }
            }
#else
            // 其他平台直接读取文件
            if (File.Exists(path))
            {
                return File.ReadAllText(path);
            }
            else
            {
                Debug.LogWarning($"[CardConfigManager] 配置文件不存在: {path}");
                return null;
            }
#endif
        }

        // ══════════════════════════════════════════════════════════════
        // 私有方法 - 数据转换
        // ══════════════════════════════════════════════════════════════

        /// <summary>
        /// 将 JSON DTO 转换为 CardConfig 业务对象。
        /// </summary>
        private CardConfig ConvertToCardConfig(CardJsonData data)
        {
            return new CardConfig
            {
                CardId = data.cardId,
                CardName = data.cardName ?? string.Empty,
                Description = data.description ?? string.Empty,
                TrackType = ParseEnum(data.trackType, CardTrackType.Instant),
                TargetType = ParseEnum(data.targetType, CardTargetType.Self),
                Tags = ParseTags(data.tags),
                EnergyCost = data.energyCost,
                Duration = data.duration,
                Rarity = data.rarity > 0 ? data.rarity : 1
            };
        }

        /// <summary>
        /// 将 JSON DTO 转换为 CardEffect 业务对象。
        /// </summary>
        private CardEffect ConvertToCardEffect(EffectJsonData data)
        {
            var effect = new CardEffect
            {
                EffectType = (EffectType)data.effectType,
                Value = data.value,
                Duration = data.duration,
                TriggerCondition = data.triggerCondition ?? string.Empty,
                IsDelayed = data.isDelayed
            };

            // 处理目标覆盖
            if (!string.IsNullOrEmpty(data.targetOverride))
            {
                effect.TargetOverride = ParseEnum(data.targetOverride, CardTargetType.Self);
            }

            return effect;
        }

        /// <summary>
        /// 解析标签列表为 CardTag 标志位。
        /// </summary>
        private CardTag ParseTags(List<string> tagNames)
        {
            if (tagNames == null || tagNames.Count == 0)
                return CardTag.None;

            CardTag result = CardTag.None;
            foreach (var name in tagNames)
            {
                if (Enum.TryParse<CardTag>(name, ignoreCase: true, out var tag))
                {
                    result |= tag;
                }
                else
                {
                    Debug.LogWarning($"[CardConfigManager] 未知的标签名称: {name}");
                }
            }
            return result;
        }

        /// <summary>
        /// 安全解析枚举，失败时返回默认值。
        /// </summary>
        private T ParseEnum<T>(string value, T defaultValue) where T : struct
        {
            if (string.IsNullOrEmpty(value))
                return defaultValue;

            if (Enum.TryParse<T>(value, ignoreCase: true, out var result))
                return result;

            Debug.LogWarning($"[CardConfigManager] 无法解析枚举 {typeof(T).Name}: {value}，使用默认值 {defaultValue}");
            return defaultValue;
        }

        /// <summary>
        /// 确保配置已加载，未加载时输出警告。
        /// </summary>
        private bool EnsureLoaded()
        {
            if (!_isLoaded)
            {
                Debug.LogError("[CardConfigManager] 配置未加载！请先调用 LoadAll()");
                return false;
            }
            return true;
        }
    }
}
