#pragma warning disable CS8632

using System;
using System.Collections.Generic;
using CardMoba.BattleCore.Context;
using CardMoba.BattleCore.Core;
using CardMoba.BattleCore.Definitions;
using CardMoba.BattleCore.EventBus;
using CardMoba.BattleCore.Foundation;
using CardMoba.ConfigModels.Card;
using CardMoba.Protocol.Enums;
using CardMoba.BattleCore.Rules.Play;
using CardMoba.Client.Data.ConfigData;

namespace CardMoba.Client.GameLogic
{
    /// <summary>
    /// 战斗流程管理器，负责桥接 UI 与 BattleCore。
    /// 它负责创建对局、暴露 UI 可调用接口、驱动 AI 回合，并把 BattleCore 事件转成日志和界面刷新。
    /// </summary>
    public class BattleGameManager
    {
        /// <summary>对局状态变化时触发，用于刷新 HP、能量、手牌和 Buff 等表现。</summary>
        public event Action OnStateChanged;

        /// <summary>战斗日志输出，允许使用 TMP RichText。</summary>
        public event Action<string> OnLogMessage;

        /// <summary>
        /// 对局结束时触发。
        /// winnerCode：1 = 玩家胜利，2 = AI 胜利，-1 = 平局。
        /// </summary>
        public event Action<int> OnGameOver;

        /// <summary>回合阶段切换时触发，用于更新阶段提示。</summary>
        public event Action<string> OnPhaseChanged;

        public const string HumanPlayerId = "player1";
        public const string AiPlayerId    = "player2";

        private BattleContext _ctx;
        private RoundManager  _roundManager;

        // configId -> CardConfig 映射，供运行时用 BattleCard.ConfigId 反查配置。
        private readonly Dictionary<string, CardConfig> _cardConfigMap
            = new Dictionary<string, CardConfig>();

        /// <summary>当前 BattleContext。</summary>
        public BattleContext Context => _ctx;

        /// <summary>是否处于玩家操作阶段。</summary>
        public bool IsPlayerTurn { get; private set; }

        /// <summary>对局是否已结束。</summary>
        public bool IsGameOver => _roundManager?.IsBattleOver ?? false;

        /// <summary>当前回合数。</summary>
        public int CurrentRound => _roundManager?.CurrentRound ?? 0;

        /// <summary>
        /// 启动一场新的 1v1 对战，使用默认战士套牌。
        /// </summary>
        public void StartBattle()
        {
            StartBattleWithDeck(DefaultWarriorDeckIds, DefaultWarriorDeckIds);
        }

        /// <summary>
        /// 使用指定卡牌 ID 列表初始化对战。
        /// </summary>
        public void StartBattleWithDeck(int[] playerDeckIds, int[] aiDeckIds)
        {
            EnsureConfigLoaded();
            _cardConfigMap.Clear();

            BuildCardConfigMap();

            var humanDeck = BuildDeckConfig(playerDeckIds);
            var aiDeck    = BuildDeckConfig(aiDeckIds);

            var eventBus = new InternalEventBus(this);

            var factory = new BattleFactory
            {
                BuffConfigProvider = ResolveRuntimeBuffConfig,
                CardDefinitionProvider = configId =>
                {
                    if (!_cardConfigMap.TryGetValue(configId, out var cardConfig))
                        return null;

                    string defaultTarget = CardConfigToEffectAdapter.CardTargetTypeToString(cardConfig.TargetType);
                    return new BattleCardDefinition
                    {
                        ConfigId = configId,
                        IsExhaust = cardConfig.Tags.HasFlag(CardTag.Exhaust),
                        IsStatCard = cardConfig.Tags.HasFlag(CardTag.Status),
                        EnergyCost = cardConfig.EnergyCost,
                        UpgradedConfigId = cardConfig.UpgradedCardConfigId,
                        Effects = CardConfigToEffectAdapter.ConvertEffects(cardConfig, defaultTarget),
                    };
                },
            };
            var result  = factory.CreateBattle(
                battleId:   "local-battle",
                randomSeed: 42,
                players: new List<PlayerSetupData>
                {
                    new PlayerSetupData
                    {
                        PlayerId     = HumanPlayerId,
                        MaxHp        = 200,
                        InitialHp    = 200,
                        InitialArmor = 0,
                        DeckConfig   = humanDeck,
                    },
                    new PlayerSetupData
                    {
                        PlayerId     = AiPlayerId,
                        MaxHp        = 200,
                        InitialHp    = 200,
                        InitialArmor = 0,
                        DeckConfig   = aiDeck,
                    },
                },
                eventBus: eventBus);

            _ctx          = result.Context;
            _roundManager = result.RoundManager;

            foreach (var log in result.SetupLog)
                OnLogMessage?.Invoke(ColorizeLog(log));

            _roundManager.BeginRound(_ctx);
            FlushLogs();

            IsPlayerTurn = true;
            OnPhaseChanged?.Invoke($"第 {_roundManager.CurrentRound} 回合 · 玩家行动");
            OnStateChanged?.Invoke();
        }

        /// <summary>
        /// 玩家打出一张瞬策牌，并立即结算。
        /// </summary>
        /// <param name="handIndex">该牌在手牌列表中的索引。</param>
        /// <returns>成功时返回卡名，失败时返回原因。</returns>
        public string PlayerPlayInstantCard(int handIndex)
        {
            if (!IsPlayerTurn || IsGameOver) return "当前无法操作";
            return PlayCardInternal(HumanPlayerId, handIndex, instant: true, runtimeParams: null);
        }

        public string PlayerPlayInstantCard(int handIndex, Dictionary<string, string> runtimeParams)
        {
            if (!IsPlayerTurn || IsGameOver) return "当前无法操作";
            return PlayCardInternal(HumanPlayerId, handIndex, instant: true, runtimeParams);
        }

        /// <summary>
        /// 玩家提交一张定策牌，等待回合结束统一结算。
        /// </summary>
        public string PlayerCommitPlanCard(int handIndex)
        {
            if (!IsPlayerTurn || IsGameOver) return "当前无法操作";
            return PlayCardInternal(HumanPlayerId, handIndex, instant: false, runtimeParams: null);
        }

        public string PlayerCommitPlanCard(int handIndex, Dictionary<string, string> runtimeParams)
        {
            if (!IsPlayerTurn || IsGameOver) return "当前无法操作";
            return PlayCardInternal(HumanPlayerId, handIndex, instant: false, runtimeParams);
        }

        /// <summary>
        /// 玩家结束回合：AI 行动 -> 回合结算 -> 下一回合开始。
        /// </summary>
        public void PlayerEndTurn()
        {
            if (!IsPlayerTurn || IsGameOver) return;

            IsPlayerTurn = false;

            OnPhaseChanged?.Invoke($"第 {_roundManager.CurrentRound} 回合 · 对手行动...");
            OnStateChanged?.Invoke();
            ExecuteAiTurn();
            FlushLogs();

            if (IsGameOver) { NotifyGameOver(); return; }

            OnPhaseChanged?.Invoke($"第 {_roundManager.CurrentRound} 回合 · 回合结算...");
            _roundManager.EndRound(_ctx);
            FlushLogs();
            OnStateChanged?.Invoke();

            if (IsGameOver) { NotifyGameOver(); return; }

            _roundManager.BeginRound(_ctx);
            FlushLogs();

            IsPlayerTurn = true;
            OnPhaseChanged?.Invoke($"第 {_roundManager.CurrentRound} 回合 · 玩家行动");
            OnStateChanged?.Invoke();
        }

        /// <summary>获取玩家运行时数据。</summary>
        public PlayerData GetHumanPlayer() => _ctx?.GetPlayer(HumanPlayerId);

        /// <summary>获取 AI 运行时数据。</summary>
        public PlayerData GetAiPlayer() => _ctx?.GetPlayer(AiPlayerId);

        /// <summary>
        /// 获取玩家手牌，并附带对应的 CardConfig。
        /// </summary>
        public List<(BattleCard Card, CardConfig Config)> GetHumanHandCards()
        {
            var list   = new List<(BattleCard, CardConfig)>();
            var player = _ctx?.GetPlayer(HumanPlayerId);
            if (player == null) return list;

            foreach (var bc in player.GetCardsInZone(CardZone.Hand))
            {
                var cfg = GetEffectiveCardConfig(bc);
                list.Add((bc, cfg));
            }
            return list;
        }

        public List<(BattleCard Card, CardConfig Config)> GetHumanDiscardCards()
        {
            var list = new List<(BattleCard, CardConfig)>();
            var player = _ctx?.GetPlayer(HumanPlayerId);
            if (player == null) return list;

            foreach (var bc in player.GetCardsInZone(CardZone.Discard))
            {
                var cfg = GetEffectiveCardConfig(bc);
                list.Add((bc, cfg));
            }

            return list;
        }

        public int GetDisplayedCost(BattleCard battleCard)
        {
            if (_ctx == null || _roundManager == null || battleCard == null)
                return 0;

            return _roundManager.ResolvePlayCost(_ctx, battleCard.OwnerId, battleCard).FinalCost;
        }

        public string GetHumanBuffSummary() => GetPlayerBuffSummary(HumanPlayerId);

        public string GetAiBuffSummary() => GetPlayerBuffSummary(AiPlayerId);

        public string GetPlayerBuffSummary(string playerId)
        {
            var player = _ctx?.GetPlayer(playerId);
            if (player == null || _ctx == null)
                return "无";

            var buffs = _ctx.BuffManager.GetBuffs(player.HeroEntity.EntityId);
            if (buffs.Count == 0)
                return "无";

            var parts = new List<string>(buffs.Count);
            foreach (var buff in buffs)
                parts.Add(FormatBuff(buff));

            return string.Join(" / ", parts);
        }

        /// <summary>打印当前战斗状态快照。</summary>
        public void PrintBattleStatus()
        {
            if (_ctx == null)
            {
                OnLogMessage?.Invoke("<color=#ff4444>[状态面板] BattleContext 为空，对局尚未开始。</color>");
                return;
            }

            var sb = new System.Text.StringBuilder();
            sb.AppendLine("<color=#ffffff>========== [战斗状态快照] ==========</color>");
            sb.AppendLine($"<color=#aaaaaa>第 {_roundManager.CurrentRound} 回合</color>");
            sb.AppendLine();
            AppendPlayerSnapshot(sb, _ctx.GetPlayer(HumanPlayerId), "我方");
            sb.AppendLine();
            AppendPlayerSnapshot(sb, _ctx.GetPlayer(AiPlayerId),    "敌方");
            sb.AppendLine("<color=#ffffff>===================================</color>");

            foreach (var line in sb.ToString().Split('\n'))
                if (!string.IsNullOrWhiteSpace(line))
                    OnLogMessage?.Invoke(line.TrimEnd('\r'));
        }

        private string PlayCardInternal(
            string playerId,
            int handIndex,
            bool instant,
            Dictionary<string, string>? runtimeParams)
        {
            var player = _ctx.GetPlayer(playerId);
            if (player == null) return "玩家不存在";

            var hand = player.GetCardsInZone(CardZone.Hand);
            if (handIndex < 0 || handIndex >= hand.Count)
                return $"手牌索引越界（{handIndex}/{hand.Count}）";

            var battleCard = hand[handIndex];
            var cardConfig = GetEffectiveCardConfig(battleCard);
            if (cardConfig == null)
                return $"找不到卡牌配置 configId={battleCard.GetEffectiveConfigId()}";

            var playRules = _roundManager.ResolvePlayRules(_ctx, playerId, battleCard, PlayOrigin.PlayerHandPlay);
            if (!playRules.Allowed)
            {
                OnLogMessage?.Invoke($"<color=#ff8866>[!] {playRules.BlockReason}</color>");
                return playRules.BlockReason;
            }

            var playCost = _roundManager.ResolvePlayCost(_ctx, playerId, battleCard, playRules);
            int cost = playCost.FinalCost;
            if (player.Energy < cost)
            {
                string reason = $"能量不足（当前 {player.Energy}，需要 {cost}）";
                OnLogMessage?.Invoke($"<color=#ff8866>[!] {reason}</color>");
                return reason;
            }

            bool hadForceConsumeFlag = battleCard.ExtraData.TryGetValue("forceConsumeAfterResolve", out var previousForceConsumeFlag);
            if (playRules.ForceConsumeAfterResolve)
                battleCard.ExtraData["forceConsumeAfterResolve"] = true;

            player.Energy -= cost;

            string cardName = cardConfig.CardName;
            bool success;
            List<EffectResult>? instantResults = null;

            if (instant)
            {
                instantResults = _roundManager.PlayInstantCard(_ctx, playerId, battleCard.InstanceId, runtimeParams);
                success = instantResults.Count > 0 || battleCard.Zone != CardZone.Hand;
                FlushLogs();
            }
            else
            {
                success = _roundManager.CommitPlanCard(_ctx, new CommittedPlanCard
                {
                    PlayerId       = playerId,
                    CardInstanceId = battleCard.InstanceId,
                    CommittedCost  = cost,
                    RuntimeParams  = runtimeParams ?? new Dictionary<string, string>(),
                });
                FlushLogs();
            }

            if (!success)
            {
                player.Energy += cost;
                if (hadForceConsumeFlag)
                    battleCard.ExtraData["forceConsumeAfterResolve"] = previousForceConsumeFlag!;
                else
                    battleCard.ExtraData.Remove("forceConsumeAfterResolve");

                string reason = "出牌失败";
                OnLogMessage?.Invoke($"<color=#ff8866>[!] {reason}</color>");
                return reason;
            }

            _roundManager.CommitSuccessfulPlayRules(_ctx, playerId, playRules);

            if (instant)
            {
                OnLogMessage?.Invoke($"<color=#aaffaa>{(playerId == HumanPlayerId ? "你" : "对手")}打出瞬策牌【{cardName}】，消耗 {cost} 点能量</color>");
                LogInstantEffectResults(playerId, cardName, instantResults);
            }
            else
            {
                OnLogMessage?.Invoke($"<color=#aaddff>{(playerId == HumanPlayerId ? "你" : "对手")}提交定策牌【{cardName}】，消耗 {cost} 点能量</color>");
            }

            OnStateChanged?.Invoke();
            if (IsGameOver) NotifyGameOver();
            return cardName;
        }

        /// <summary>
        /// 旧的手动移牌辅助逻辑。
        /// 当前主流程大多由 BattleCore 负责区位流转，这里仅保留兼容实现。
        /// </summary>
        private void MoveCardAfterPlay(BattleCard battleCard, CardConfig cardConfig)
        {
            bool isExhaust = cardConfig.Tags.HasFlag(CardTag.Exhaust);
            if (isExhaust)
            {
                var owner = _ctx.GetPlayer(battleCard.OwnerId);
                owner?.AllCards.Remove(battleCard);
                _ctx.RoundLog.Add($"[BattleGameManager] 卡牌【{cardConfig.CardName}】已消耗（Exhaust）。");
            }
            else
            {
                _ctx.CardManager.MoveCard(_ctx, battleCard, CardZone.Discard);
            }
        }

        private void ExecuteAiTurn()
        {
            var player = _ctx.GetPlayer(AiPlayerId);
            if (player == null || !player.HeroEntity.IsAlive) return;

            var hand = player.GetCardsInZone(CardZone.Hand);
            var snapshot = new List<BattleCard>(hand);

            foreach (var battleCard in snapshot)
            {
                if (!player.HeroEntity.IsAlive || IsGameOver) break;
                var cfg = GetEffectiveCardConfig(battleCard);
                if (cfg == null) continue;

                bool isInstant = cfg.TrackType == CardTrackType.Instant;
                PlayCardInternal(AiPlayerId, 0, isInstant, runtimeParams: null);
            }
        }

        /// <summary>
        /// 默认战士测试牌组。
        /// </summary>
        /* Legacy V1 warrior demo deck removed from active use.
        {
            2001, 2001, 2001, 2001,
            2002, 2002, 2002,
            2003, 2003,
            2005, 2005,
            1001,
            1002,
            2008, 2008,
            1001,
            1002,
        };

        */
        private static readonly int[] DefaultWarriorDeckIds = new int[]
        {
            2001, 2001, 2001,
            1001, 1001, 1001,
            1002, 1002,
            1003, 1003,
            1004, 1004,
            1005,
            1008,
            2002, 2002,
            2003, 2003,
            2004,
            2005, 2005,
            2006,
            2007, 2007,
            2008, 2008,
            2009, 2009,
            2010, 2010,
            2011, 2011,
            2013,
            1006,
            2015,
        };

        private static BuffConfig? ResolveRuntimeBuffConfig(string buffId)
        {
            return buffId switch
            {
                "strength" => new BuffConfig
                {
                    BuffId = "strength",
                    BuffName = "力量",
                    Description = "提高造成的伤害。",
                    BuffType = BuffType.Strength,
                    IsBuff = true,
                    StackRule = BuffStackRule.RefreshDuration,
                    MaxStacks = 99,
                    DefaultDuration = 0,
                    DefaultValue = 0,
                    IsDispellable = true,
                    IsPurgeable = true,
                },
                "weak" => new BuffConfig
                {
                    BuffId = "weak",
                    BuffName = "虚弱",
                    Description = "造成的伤害降低 25%。",
                    BuffType = BuffType.Weak,
                    IsBuff = false,
                    StackRule = BuffStackRule.RefreshDuration,
                    MaxStacks = 99,
                    DefaultDuration = 1,
                    DefaultValue = 25,
                    IsDispellable = true,
                    IsPurgeable = true,
                },
                "vulnerable" => new BuffConfig
                {
                    BuffId = "vulnerable",
                    BuffName = "易伤",
                    Description = "受到的伤害增加 50%。",
                    BuffType = BuffType.Vulnerable,
                    IsBuff = false,
                    StackRule = BuffStackRule.StackValue,
                    MaxStacks = 99,
                    DefaultDuration = 1,
                    DefaultValue = 50,
                    IsDispellable = true,
                    IsPurgeable = true,
                },
                "no_draw_this_turn" => new BuffConfig
                {
                    BuffId = "no_draw_this_turn",
                    BuffName = "本回合禁止抽牌",
                    Description = "本回合剩余时间内无法再抽牌。",
                    BuffType = BuffType.NoDrawThisTurn,
                    IsBuff = false,
                    StackRule = BuffStackRule.RefreshDuration,
                    MaxStacks = 1,
                    DefaultDuration = 1,
                    DefaultValue = 0,
                    IsDispellable = true,
                    IsPurgeable = true,
                },
                "no_damage_card_this_turn" => new BuffConfig
                {
                    BuffId = "no_damage_card_this_turn",
                    BuffName = "本回合禁止伤害牌",
                    Description = "本回合剩余时间内无法再打出伤害牌。",
                    BuffType = BuffType.NoDamageCardThisTurn,
                    IsBuff = false,
                    StackRule = BuffStackRule.RefreshDuration,
                    MaxStacks = 1,
                    DefaultDuration = 1,
                    DefaultValue = 0,
                    IsDispellable = true,
                    IsPurgeable = true,
                },
                "delayed_vulnerable_next_round" => new BuffConfig
                {
                    BuffId = "delayed_vulnerable_next_round",
                    BuffName = "下回合易伤",
                    Description = "下回合开始时获得对应数值的易伤。",
                    BuffType = BuffType.DelayedVulnerableNextRound,
                    IsBuff = false,
                    StackRule = BuffStackRule.StackValue,
                    MaxStacks = 99,
                    DefaultDuration = 2,
                    DefaultValue = 50,
                    IsDispellable = true,
                    IsPurgeable = true,
                    IsHidden = true,
                },
                "blood_ritual" => new BuffConfig
                {
                    BuffId = "blood_ritual",
                    BuffName = "血祭",
                    Description = "每次失去生命时获得力量。",
                    BuffType = BuffType.BloodRitual,
                    IsBuff = true,
                    StackRule = BuffStackRule.RefreshDuration,
                    MaxStacks = 1,
                    DefaultDuration = 0,
                    DefaultValue = 1,
                    IsDispellable = true,
                    IsPurgeable = true,
                },
                "corruption" => new BuffConfig
                {
                    BuffId = "corruption",
                    BuffName = "腐化",
                    Description = "每回合前 X 张牌费用变为 0，且结算后消耗。",
                    BuffType = BuffType.Corruption,
                    IsBuff = true,
                    StackRule = BuffStackRule.StackValue,
                    MaxStacks = 99,
                    DefaultDuration = 0,
                    DefaultValue = 2,
                    IsDispellable = true,
                    IsPurgeable = true,
                },
                _ => null,
            };
        }

        private List<(string configId, int count)> BuildDeckConfig(int[] cardIds)
        {
            var countMap = new Dictionary<int, int>();
            foreach (var id in cardIds)
            {
                if (!countMap.ContainsKey(id)) countMap[id] = 0;
                countMap[id]++;
            }

            var deck = new List<(string, int)>();
            foreach (var kv in countMap)
            {
                if (CardConfigManager.Instance.GetCard(kv.Key) != null)
                    deck.Add((kv.Key.ToString(), kv.Value));
                else
                    OnLogMessage?.Invoke($"<color=#ffaa00>[警告] 找不到卡牌配置 {kv.Key}，已忽略。</color>");
            }
            return deck;
        }

        /// <summary>
        /// 建立 configId 到 CardConfig 的映射。
        /// </summary>
        private void BuildCardConfigMap()
        {
            var all = CardConfigManager.Instance.AllCards;
            if (all == null) return;
            foreach (var kv in all)
                _cardConfigMap[kv.Key.ToString()] = kv.Value;
        }

        private CardConfig? GetEffectiveCardConfig(BattleCard battleCard)
        {
            if (battleCard == null)
                return null;

            if (_cardConfigMap.TryGetValue(battleCard.GetEffectiveConfigId(), out var effectiveConfig))
                return effectiveConfig;

            return _cardConfigMap.TryGetValue(battleCard.ConfigId, out var baseConfig) ? baseConfig : null;
        }

        private void EnsureConfigLoaded()
        {
            if (!CardConfigManager.Instance.IsLoaded)
                CardConfigManager.Instance.LoadAll();
        }

        private void FlushLogs()
        {
            if (_ctx == null) return;
            foreach (var raw in _ctx.RoundLog)
                OnLogMessage?.Invoke(ColorizeLog(raw));
            _ctx.RoundLog.Clear();
        }

        private void NotifyGameOver()
        {
            string? winner = _roundManager?.WinnerId;
            int code = winner == null       ? -1
                     : winner == HumanPlayerId ? 1
                     : 2;
            OnGameOver?.Invoke(code);
        }

        private static string ColorizeLog(string log)
        {
            if (log.Contains("<color=")) return log;
            string lower = log.ToLower();

            if (lower.Contains("伤害") || lower.Contains("击碎") || lower.Contains("扣除"))
                return $"<color=#ff8866>{log}</color>";
            if (lower.Contains("护盾") || lower.Contains("shield"))
                return $"<color=#66aaff>{log}</color>";
            if (lower.Contains("治疗") || lower.Contains("吸血") || lower.Contains("恢复"))
                return $"<color=#66ee88>{log}</color>";
            if (lower.Contains("buff") || lower.Contains("获得") || lower.Contains("失去"))
                return $"<color=#ffdd55>{log}</color>";
            if (lower.Contains("回合") && (log.Contains("---") || log.Contains("结算")))
                return $"<color=#888888><size=85%>{log}</size></color>";

            return log;
        }

        private void LogInstantEffectResults(string playerId, string cardName, List<EffectResult>? results)
        {
            if (results == null || results.Count == 0)
                return;

            var parts = new List<string>();
            foreach (var result in results)
            {
                var summary = BuildEffectSummary(result);
                if (!string.IsNullOrWhiteSpace(summary))
                    parts.Add(summary);
            }

            if (parts.Count == 0)
                return;

            OnLogMessage?.Invoke(
                $"<color=#cceeff>[效果] {GetPlayerLabel(playerId)}的【{cardName}】：{string.Join("；", parts)}</color>");
        }

        private string? BuildEffectSummary(EffectResult result)
        {
            if (result == null || !result.Success)
                return null;

            switch (result.Type)
            {
                case EffectType.Damage:
                case EffectType.Pierce:
                    return result.TotalRealHpDamage > 0 ? $"造成 {result.TotalRealHpDamage} 点生命伤害" : null;

                case EffectType.Heal:
                case EffectType.Lifesteal:
                    return result.TotalRealHeal > 0 ? $"恢复 {result.TotalRealHeal} 点生命" : null;

                case EffectType.Shield:
                    return result.TotalRealShield > 0 ? $"获得 {result.TotalRealShield} 点护盾" : null;

                case EffectType.Draw:
                    return TryGetExtraInt(result, "drawnCount", out var drawnCount) && drawnCount > 0
                        ? $"抽 {drawnCount} 张牌"
                        : null;

                case EffectType.AddBuff:
                    if (!TryGetExtraInt(result, "appliedCount", out var appliedCount) || appliedCount <= 0)
                        return null;

                    string buffConfigId = TryGetExtraString(result, "buffConfigId") ?? string.Empty;
                    string buffName = GetBuffDisplayName(buffConfigId);
                    string valueText = TryGetExtraInt(result, "buffValue", out var buffValue) && buffValue > 0
                        ? FormatBuffValue(buffConfigId, buffValue)
                        : string.Empty;
                    string durationText = TryGetExtraInt(result, "buffDuration", out var buffDuration)
                        ? FormatDuration(buffDuration)
                        : string.Empty;

                    var buffParts = new List<string>();
                    if (!string.IsNullOrWhiteSpace(valueText))
                        buffParts.Add(valueText);
                    if (!string.IsNullOrWhiteSpace(durationText))
                        buffParts.Add(durationText);

                    return buffParts.Count > 0
                        ? $"附加 {buffName}（{string.Join("，", buffParts)}）"
                        : $"附加 {buffName}";

                case EffectType.GainEnergy:
                    return TryGetExtraInt(result, "gainedEnergy", out var gainedEnergy) && gainedEnergy > 0
                        ? $"获得 {gainedEnergy} 点能量"
                        : null;

                case EffectType.GenerateCard:
                    if (!TryGetExtraInt(result, "generatedCount", out var generatedCount) || generatedCount <= 0)
                        return null;

                    string generatedConfigId = TryGetExtraString(result, "generatedConfigId") ?? string.Empty;
                    string generatedName = ResolveCardName(generatedConfigId);
                    string generatedZone = TryGetExtraString(result, "generatedZone") ?? "Hand";
                    return $"生成 {generatedCount} 张【{generatedName}】到{FormatZoneName(generatedZone)}";

                case EffectType.MoveSelectedCardToDeckTop:
                    string selectedConfigId = TryGetExtraString(result, "selectedCardConfigId") ?? string.Empty;
                    return !string.IsNullOrWhiteSpace(selectedConfigId)
                        ? $"将【{ResolveCardName(selectedConfigId)}】置于牌堆顶"
                        : "将所选卡牌置于牌堆顶";

                case EffectType.UpgradeCardsInHand:
                    return TryGetExtraInt(result, "upgradedCount", out var upgradedCount) && upgradedCount > 0
                        ? $"升级 {upgradedCount} 张手牌"
                        : null;

                case EffectType.ReturnSourceCardToHandAtRoundEnd:
                    return "回合结束时返回手牌";
            }

            return null;
        }

        private static bool TryGetExtraInt(EffectResult result, string key, out int value)
        {
            value = 0;
            if (!result.Extra.TryGetValue(key, out var raw) || raw == null)
                return false;

            if (raw is int intValue)
            {
                value = intValue;
                return true;
            }

            return int.TryParse(raw.ToString(), out value);
        }

        private static string? TryGetExtraString(EffectResult result, string key)
        {
            if (!result.Extra.TryGetValue(key, out var raw) || raw == null)
                return null;

            return raw.ToString();
        }

        private string FormatBuff(BuffUnit buff)
        {
            string name = !string.IsNullOrWhiteSpace(buff.DisplayName)
                ? buff.DisplayName
                : GetBuffDisplayName(buff.ConfigId);

            var parts = new List<string>();
            if (buff.Value > 0)
                parts.Add(FormatBuffValue(buff.ConfigId, buff.Value));

            string durationText = FormatDuration(buff.RemainingRounds);
            if (!string.IsNullOrWhiteSpace(durationText))
                parts.Add(durationText);

            return parts.Count > 0
                ? $"{name}({string.Join("，", parts)})"
                : name;
        }

        private string FormatBuffValue(string buffConfigId, int value)
        {
            string lower = buffConfigId?.ToLowerInvariant() ?? string.Empty;
            return lower switch
            {
                "weak" => $"{value}%",
                "vulnerable" => $"{value}%",
                _ => value.ToString(),
            };
        }

        private static string FormatDuration(int remainingRounds)
        {
            if (remainingRounds < 0)
                return "永久";

            if (remainingRounds == 0)
                return string.Empty;

            return $"{remainingRounds}回合";
        }

        private string GetBuffDisplayName(string buffConfigId)
        {
            if (string.IsNullOrWhiteSpace(buffConfigId))
                return "未知Buff";

            var buffConfig = ResolveRuntimeBuffConfig(buffConfigId);
            if (buffConfig != null && !string.IsNullOrWhiteSpace(buffConfig.BuffName))
                return buffConfig.BuffName;

            return buffConfigId;
        }

        private string ResolveCardName(string configId)
        {
            if (string.IsNullOrWhiteSpace(configId))
                return "未知卡牌";

            return _cardConfigMap.TryGetValue(configId, out var config)
                ? config.CardName
                : configId;
        }

        private string GetPlayerLabel(string playerId)
        {
            return playerId == HumanPlayerId ? "你"
                : playerId == AiPlayerId ? "对手"
                : playerId;
        }

        private string GetEntityLabel(string entityId)
        {
            if (_ctx != null)
            {
                foreach (var player in _ctx.AllPlayers.Values)
                {
                    if (player.HeroEntity.EntityId == entityId)
                        return GetPlayerLabel(player.PlayerId);
                }
            }

            return entityId;
        }

        private static string FormatZoneName(string zone)
        {
            return zone.ToLowerInvariant() switch
            {
                "deck" => "牌库",
                "discard" => "弃牌堆",
                "consume" => "消耗区",
                _ => "手牌",
            };
        }

        private void AppendPlayerSnapshot(System.Text.StringBuilder sb, PlayerData? p, string label)
        {
            if (p == null) { sb.AppendLine($"  [{label}]: 数据不可用"); return; }

            var hero    = p.HeroEntity;
            var hand    = p.GetCardsInZone(CardZone.Hand);
            var deck    = p.GetCardsInZone(CardZone.Deck);
            var discard = p.GetCardsInZone(CardZone.Discard);

            string hpColor = hero.Hp <= hero.MaxHp / 3 ? "#ff4444"
                           : hero.Hp <= hero.MaxHp * 2 / 3 ? "#ffaa33"
                           : "#66ee88";

            sb.AppendLine($"  <color=#ddddff>[{label}]</color>");
            sb.AppendLine($"    HP    : <color={hpColor}>{hero.Hp}/{hero.MaxHp}</color>"
                + (hero.Shield > 0 ? $"   护盾: <color=#66aaff>{hero.Shield}</color>" : "")
                + (hero.Armor  > 0 ? $"   护甲: <color=#88ccff>{hero.Armor}</color>" : ""));
            sb.AppendLine($"    能量  : <color=#ffdd55>{p.Energy}/{p.MaxEnergy}</color>");
            sb.AppendLine($"    手牌  : {hand.Count}  |  牌库: {deck.Count}  弃牌: {discard.Count}");
            sb.AppendLine($"    Buff  : {GetPlayerBuffSummary(p.PlayerId)}");
        }

        /// <summary>
        /// 将 BattleCore 内部事件转成 BattleGameManager 的 UI 日志。
        /// </summary>
        private sealed class InternalEventBus : IEventBus
        {
            private readonly BattleGameManager _mgr;
            public InternalEventBus(BattleGameManager mgr) => _mgr = mgr;

            public void Subscribe<T>(Action<T> handler)   where T : BattleEventBase { }
            public void Unsubscribe<T>(Action<T> handler) where T : BattleEventBase { }

            public void Publish<T>(T evt) where T : BattleEventBase
            {
                switch (evt)
                {
                    case DamageDealtEvent dmg:
                        if (dmg.RealHpDamage > 0)
                            _mgr.OnLogMessage?.Invoke(
                                $"<color=#ff6666>[伤害] {_mgr.GetEntityLabel(dmg.SourceEntityId)} -> {_mgr.GetEntityLabel(dmg.TargetEntityId)} {dmg.RealHpDamage} 点"
                                + (dmg.ShieldAbsorbed > 0 ? $"，同时击碎护盾 {dmg.ShieldAbsorbed} 点" : "")
                                + "</color>");

                        else if (dmg.ShieldAbsorbed > 0)
                            _mgr.OnLogMessage?.Invoke(
                                $"<color=#66aaff>[护盾] {_mgr.GetEntityLabel(dmg.TargetEntityId)} 吸收 {dmg.ShieldAbsorbed} 点伤害</color>");
                        break;

                    case HealEvent heal:
                        _mgr.OnLogMessage?.Invoke(
                            $"<color=#66ee88>[治疗] {_mgr.GetEntityLabel(heal.TargetEntityId)} 恢复 {heal.RealHealAmount} 点生命</color>");
                        break;

                    case ShieldGainedEvent sg:
                        _mgr.OnLogMessage?.Invoke(
                            $"<color=#66aaff>[护盾] {_mgr.GetEntityLabel(sg.TargetEntityId)} 获得 {sg.ShieldAmount} 点护盾</color>");
                        break;

                    case BuffAddedEvent buffAdded:
                        _mgr.OnLogMessage?.Invoke(
                            $"<color=#ffdd55>[Buff] {_mgr.GetEntityLabel(buffAdded.TargetEntityId)} 获得 {_mgr.FormatBuff(buffAdded.Buff)}</color>");
                        break;

                    case BuffRemovedEvent buffRemoved:
                        _mgr.OnLogMessage?.Invoke(
                            $"<color=#cccccc>[Buff] {_mgr.GetEntityLabel(buffRemoved.TargetEntityId)} 失去 {_mgr.GetBuffDisplayName(buffRemoved.BuffConfigId)}</color>");
                        break;

                    case RoundStartEvent rs:
                        _mgr.OnLogMessage?.Invoke(
                            $"<color=#888888><size=85%>--- 第 {rs.Round} 回合开始 ---</size></color>");
                        break;

                    case RoundEndEvent re:
                        _mgr.OnLogMessage?.Invoke(
                            $"<color=#888888><size=85%>--- 第 {re.Round} 回合结束 ---</size></color>");
                        break;

                    case PlayerDeathEvent death:
                        _mgr.OnLogMessage?.Invoke(
                            $"<color=#ff4444>[死亡] {_mgr.GetPlayerLabel(death.PlayerId)}</color>");
                        break;

                    case BattleEndEvent end:
                        _mgr.OnLogMessage?.Invoke(end.IsDraw
                            ? "<color=#ffdd55>[结束] 平局</color>"
                            : $"<color=#ffdd55>[结束] 胜者：{_mgr.GetPlayerLabel(end.WinnerId ?? string.Empty)}</color>");
                        break;
                }
            }
        }
    }
}


