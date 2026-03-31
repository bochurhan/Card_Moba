#pragma warning disable CS8632

using System;
using System.Collections.Generic;
using System.Linq;
using CardMoba.BattleCore.Context;
using CardMoba.BattleCore.Core;
using CardMoba.BattleCore.Definitions;
using CardMoba.BattleCore.EventBus;
using CardMoba.BattleCore.Foundation;
using CardMoba.Client.GameLogic.Abstractions;
using CardMoba.ConfigModels.Card;
using CardMoba.MatchFlow.Catalog;
using CardMoba.MatchFlow.Context;
using CardMoba.MatchFlow.Core;
using CardMoba.MatchFlow.Deck;
using CardMoba.MatchFlow.Definitions;
using CardMoba.MatchFlow.Rules;
using CardMoba.Protocol.Enums;
using CardMoba.BattleCore.Rules.Play;
using CardMoba.Client.Data.ConfigData;

namespace CardMoba.Client.GameLogic
{
    /// <summary>
    /// 本地对局流程管理器，负责桥接 UI、MatchFlow 与 BattleCore。
    /// 当前用于客户端本地验证 1v1 的 4+1 整局流程，并复用现有 Battle UI。
    /// </summary>
    public class BattleGameManager : IBattleClientRuntime
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
        public event Action<BattlePhaseViewState> OnPhaseChanged;

        /// <summary>构筑窗口首次打开时触发。</summary>
        public event Action<BuildWindowViewState> OnBuildWindowOpened;

        /// <summary>构筑窗口内容更新时触发。</summary>
        public event Action<BuildWindowViewState> OnBuildWindowUpdated;

        /// <summary>构筑窗口关闭时触发。</summary>
        public event Action OnBuildWindowClosed;

        public const string HumanPlayerId = "player1";
        public const string AiPlayerId    = "player2";
        public const string HumanTeamId   = "team_player";
        public const string AiTeamId      = "team_ai";

        private BattleContext _ctx;
        private RoundManager  _roundManager;
        private MatchContext _matchContext;
        private MatchManager _matchManager;
        private InternalEventBus _eventBus;
        private int _totalBattleCount;
        private BuildWindowViewState _currentBuildWindow;

        // configId -> CardConfig 映射，供运行时用 BattleCard.ConfigId 反查配置。
        private readonly Dictionary<string, CardConfig> _cardConfigMap
            = new Dictionary<string, CardConfig>();

        /// <summary>当前 BattleContext。</summary>
        public BattleContext Context => _ctx;

        /// <summary>是否处于玩家操作阶段。</summary>
        public bool IsPlayerTurn { get; private set; }

        /// <summary>对局是否已结束。</summary>
        public bool IsGameOver => _matchContext?.IsMatchOver ?? (_roundManager?.IsBattleOver ?? false);

        /// <summary>本地验证模式不支持回合锁定/取消锁定。</summary>
        public bool SupportsTurnLockToggle => false;

        /// <summary>本地验证模式不存在回合锁定状态。</summary>
        public bool IsTurnLocked => false;

        /// <summary>本地验证模式不支持回合锁定冷却。</summary>
        public bool CanToggleTurnLock => false;

        /// <summary>当前回合数。</summary>
        public int CurrentRound => _roundManager?.CurrentRound ?? 0;

        /// <summary>当前战斗快照视图状态。</summary>
        public BattleSnapshotViewState CurrentBattleView => CreateBattleSnapshotViewState();

        /// <summary>当前构筑窗口视图状态。</summary>
        public BuildWindowViewState CurrentBuildWindow => _currentBuildWindow;

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
            _currentBuildWindow = null;
            IsPlayerTurn = false;

            BuildCardConfigMap();
            _eventBus = new InternalEventBus(this);

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
            var buildCatalog = CardConfigManager.Instance.CreateBuildCatalog(CreateDefaultEquipmentDefinitions());
            var matchFactory = new MatchFactory();

            _matchManager = new MatchManager(factory, buildCatalog: buildCatalog);
            _matchContext = matchFactory.CreateMatch(
                "local-matchflow",
                CreateLocalMatchRuleset(),
                CreateMatchPlayers(playerDeckIds, aiDeckIds),
                baseRandomSeed: 42);
            _totalBattleCount = _matchContext.Ruleset.Steps.Count;

            _matchManager.StartMatch(_matchContext, _eventBus);
            SyncActiveBattle();
            FlushMatchLogs();
            StartActiveBattle();
        }

        /// <summary>
        /// 玩家打出一张瞬策牌，并立即结算。
        /// </summary>
        public string PlayerPlayInstantCard(string cardInstanceId)
        {
            if (!IsPlayerTurn || IsGameOver) return "当前无法操作";
            return PlayCardInternal(HumanPlayerId, cardInstanceId, instant: true, runtimeParams: null);
        }

        public string PlayerPlayInstantCard(string cardInstanceId, Dictionary<string, string> runtimeParams)
        {
            if (!IsPlayerTurn || IsGameOver) return "当前无法操作";
            return PlayCardInternal(HumanPlayerId, cardInstanceId, instant: true, runtimeParams);
        }

        /// <summary>
        /// 玩家提交一张定策牌，等待回合结束统一结算。
        /// </summary>
        public string PlayerCommitPlanCard(string cardInstanceId)
        {
            if (!IsPlayerTurn || IsGameOver) return "当前无法操作";
            return PlayCardInternal(HumanPlayerId, cardInstanceId, instant: false, runtimeParams: null);
        }

        public string PlayerCommitPlanCard(string cardInstanceId, Dictionary<string, string> runtimeParams)
        {
            if (!IsPlayerTurn || IsGameOver) return "当前无法操作";
            return PlayCardInternal(HumanPlayerId, cardInstanceId, instant: false, runtimeParams);
        }

        /// <summary>
        /// 玩家结束回合：AI 行动 -> 回合结算 -> 下一回合开始。
        /// </summary>
        public void PlayerEndTurn()
        {
            if (!IsPlayerTurn || IsGameOver || _ctx == null || _roundManager == null) return;

            IsPlayerTurn = false;
            var battleContext = _ctx;
            var roundManager = _roundManager;

            PublishBattlePhase(BattleClientPhaseKind.OpponentAction, "对手行动", "对手行动");
            OnStateChanged?.Invoke();
            ExecuteAiTurn();
            FlushLogs();

            if (!ReferenceEquals(_ctx, battleContext) || !ReferenceEquals(_roundManager, roundManager))
                return;

            if (HandleBattleCompletion())
                return;

            PublishBattlePhase(BattleClientPhaseKind.Settlement, "回合结算", "结算中...");
            roundManager.EndRound(battleContext);
            FlushLogs();
            OnStateChanged?.Invoke();

            if (HandleBattleCompletion())
                return;

            roundManager.BeginRound(battleContext);
            FlushLogs();

            IsPlayerTurn = true;
            PublishBattlePhase(BattleClientPhaseKind.Operation, "操作期（无时限）", "操作中（无时限）");
            OnStateChanged?.Invoke();
        }

        public void SetTurnLock(bool isLocked)
        {
            if (isLocked)
                PlayerEndTurn();
        }

        private int GetDisplayedCost(BattleCard battleCard)
        {
            if (_ctx == null || _roundManager == null || battleCard == null)
                return 0;

            return _roundManager.ResolvePlayCost(_ctx, battleCard.OwnerId, battleCard).FinalCost;
        }

        private string GetHumanBuffSummary() => GetPlayerBuffSummary(HumanPlayerId);

        private string GetAiBuffSummary() => GetPlayerBuffSummary(AiPlayerId);

        private string GetPlayerBuffSummary(string playerId)
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

        /// <summary>
        /// 将本地 BattleCore 运行时映射为客户端战斗视图状态。
        /// </summary>
        private BattleSnapshotViewState CreateBattleSnapshotViewState()
        {
            var viewState = new BattleSnapshotViewState
            {
                MatchId = _matchContext?.MatchId ?? string.Empty,
                BattleId = _ctx?.BattleId ?? string.Empty,
                BattleIndex = GetCurrentBattleNumber(),
                TotalBattleCount = _totalBattleCount,
                CurrentRound = CurrentRound,
                IsBattleOver = _roundManager?.IsBattleOver ?? false,
                PhaseKind = ResolveCurrentBattlePhaseKind(),
            };

            if (_ctx == null)
                return viewState;

            var localPlayer = _ctx.GetPlayer(HumanPlayerId);
            if (localPlayer != null)
            {
                viewState.LocalPlayer = CreateBattlePlayerViewState(localPlayer, true);
                foreach (var card in localPlayer.GetCardsInZone(CardZone.Hand))
                {
                    var config = GetEffectiveCardConfig(card);
                    viewState.LocalHandCards.Add(CreateBattleCardViewState(card, config, true));
                }

                foreach (var card in localPlayer.GetCardsInZone(CardZone.Discard))
                {
                    var config = GetEffectiveCardConfig(card);
                    viewState.LocalDiscardCards.Add(CreateBattleCardViewState(card, config, false));
                }
            }

            var opponentPlayer = _ctx.GetPlayer(AiPlayerId);
            if (opponentPlayer != null)
                viewState.OpponentPlayer = CreateBattlePlayerViewState(opponentPlayer, false);

            return viewState;
        }

        private BattlePlayerViewState CreateBattlePlayerViewState(PlayerData player, bool isHuman)
        {
            var viewState = new BattlePlayerViewState
            {
                PlayerId = player.PlayerId,
                TeamId = player.TeamId,
                DisplayName = isHuman ? "你" : "对手",
                IsAlive = player.HeroEntity.IsAlive,
                Hp = player.HeroEntity.Hp,
                MaxHp = player.HeroEntity.MaxHp,
                Shield = player.HeroEntity.Shield,
                Armor = player.HeroEntity.Armor,
                Energy = player.Energy,
                MaxEnergy = player.MaxEnergy,
                HandCount = player.GetCardsInZone(CardZone.Hand).Count,
                DeckCount = player.GetCardsInZone(CardZone.Deck).Count,
                DiscardCount = player.GetCardsInZone(CardZone.Discard).Count,
                PendingPlanCount = _ctx?.PendingPlanSnapshots.Count(item => string.Equals(item.PlayerId, player.PlayerId, StringComparison.Ordinal)) ?? 0,
                IsTurnLocked = false,
                LockCooldownUntilUnixMs = 0,
                BuffSummaryText = isHuman ? GetHumanBuffSummary() : GetAiBuffSummary(),
            };

            if (_ctx != null)
            {
                foreach (var buff in _ctx.BuffManager.GetBuffs(player.HeroEntity.EntityId))
                {
                    viewState.Buffs.Add(new BattleBuffViewState
                    {
                        ConfigId = buff.ConfigId,
                        DisplayName = string.IsNullOrWhiteSpace(buff.DisplayName) ? GetBuffDisplayName(buff.ConfigId) : buff.DisplayName,
                        Value = buff.Value,
                        RemainingRounds = buff.RemainingRounds,
                    });
                }
            }

            return viewState;
        }

        private BattleCardViewState CreateBattleCardViewState(BattleCard battleCard, CardConfig config, bool resolveDisplayedCost)
        {
            return new BattleCardViewState
            {
                InstanceId = battleCard.InstanceId,
                BaseConfigId = battleCard.ConfigId,
                EffectiveConfigId = battleCard.GetEffectiveConfigId(),
                DisplayName = config?.CardName ?? battleCard.GetEffectiveConfigId(),
                Description = config?.Description ?? "（无描述）",
                DisplayedCost = resolveDisplayedCost ? GetDisplayedCost(battleCard) : (config?.EnergyCost ?? 0),
                TrackType = config?.TrackType ?? CardTrackType.Instant,
                RequiresDiscardSelection = RequiresDiscardSelection(config),
            };
        }

        private BattleClientPhaseKind ResolveCurrentBattlePhaseKind()
        {
            if (_currentBuildWindow != null)
                return BattleClientPhaseKind.BuildWindow;
            if (_matchContext?.IsMatchOver == true)
                return BattleClientPhaseKind.MatchEnded;
            if (_ctx?.CurrentPhase == BattleContext.BattlePhase.Settlement)
                return BattleClientPhaseKind.Settlement;
            if (!IsPlayerTurn)
                return BattleClientPhaseKind.OpponentAction;

            return BattleClientPhaseKind.Operation;
        }

        /// <summary>
        /// 提交当前本地玩家的一次构筑选择。
        /// </summary>
        public void SubmitBuildChoice(BuildChoiceViewState choice)
        {
            if (choice == null || _matchContext?.ActiveBuildWindow == null)
                return;

            try
            {
                _matchManager.ApplyBuildAction(_matchContext, HumanPlayerId, ToRuntimeBuildChoice(choice));
                FlushMatchLogs();
                PublishBuildWindowState(opened: false);
                OnStateChanged?.Invoke();
            }
            catch (Exception ex)
            {
                OnLogMessage?.Invoke($"<color=#ff8866>[构筑] {ex.Message}</color>");
            }
        }

        /// <summary>
        /// 锁定本地玩家的构筑选择，并在满足条件时推进到下一场战斗。
        /// </summary>
        public void LockBuildWindow()
        {
            if (_matchContext?.ActiveBuildWindow == null)
                return;

            try
            {
                _matchManager.LockBuildChoice(_matchContext, HumanPlayerId);
                FlushMatchLogs();

                if (_matchManager.AdvanceIfReady(_matchContext, _eventBus))
                {
                    CloseBuildWindow();
                    FlushMatchLogs();

                    if (_matchContext.IsMatchOver)
                    {
                        PublishDirectPhase(
                            BattleClientPhaseKind.MatchEnded,
                            "整局结束",
                            "整局结束");
                        OnStateChanged?.Invoke();
                        NotifyGameOver();
                        return;
                    }

                    StartActiveBattle();
                    return;
                }

                PublishBuildWindowState(opened: false);
                OnStateChanged?.Invoke();
            }
            catch (Exception ex)
            {
                OnLogMessage?.Invoke($"<color=#ff8866>[构筑] {ex.Message}</color>");
            }
        }

        private string PlayCardInternal(
            string playerId,
            string cardInstanceId,
            bool instant,
            Dictionary<string, string>? runtimeParams)
        {
            var player = _ctx.GetPlayer(playerId);
            if (player == null) return "玩家不存在";

            var hand = player.GetCardsInZone(CardZone.Hand);
            var battleCard = hand.FirstOrDefault(card => string.Equals(card.InstanceId, cardInstanceId, StringComparison.Ordinal));
            if (battleCard == null)
                return $"手牌中不存在卡牌实例：{cardInstanceId}";

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
            HandleBattleCompletion();
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

            var battleContextAtStart = _ctx;
            var hand = player.GetCardsInZone(CardZone.Hand);
            var snapshot = new List<BattleCard>(hand);

            foreach (var battleCard in snapshot)
            {
                if (!ReferenceEquals(_ctx, battleContextAtStart) || !player.HeroEntity.IsAlive || IsGameOver) break;
                var cfg = GetEffectiveCardConfig(battleCard);
                if (cfg == null) continue;

                bool isInstant = cfg.TrackType == CardTrackType.Instant;
                PlayCardInternal(AiPlayerId, battleCard.InstanceId, isInstant, runtimeParams: null);
            }
        }

        private MatchRuleset CreateLocalMatchRuleset()
        {
            string defaultPoolId = BuildCatalogAssembler.GetDefaultPoolId(HeroClass.Warrior);
            var ruleset = new MatchRuleset
            {
                BuildWindowTimeoutMs = 30000,
                DefaultTimeoutAction = BuildActionType.Heal,
            };

            for (int i = 0; i < 4; i++)
            {
                var step = new MatchBattleStep
                {
                    StepId = $"battle_{i + 1:D2}",
                    Mode = BattleStepMode.Duel1v1,
                    BattleRuleset = new BattleRuleset
                    {
                        Mode = BattleMode.Duel1v1,
                        LocalEndPolicy = BattleLocalEndPolicy.RoundLimit,
                        MaxRounds = 5,
                    },
                    OpensBuildWindowAfter = true,
                    BuildPoolId = defaultPoolId,
                };
                step.ParticipantPlayerIds.Add(HumanPlayerId);
                step.ParticipantPlayerIds.Add(AiPlayerId);
                ruleset.Steps.Add(step);
            }

            var finalStep = new MatchBattleStep
            {
                StepId = "battle_05_final",
                Mode = BattleStepMode.Duel1v1,
                BattleRuleset = new BattleRuleset
                {
                    Mode = BattleMode.Duel1v1,
                    LocalEndPolicy = BattleLocalEndPolicy.TeamElimination,
                    MaxRounds = 10,
                },
                OpensBuildWindowAfter = false,
                BuildPoolId = defaultPoolId,
            };
            finalStep.ParticipantPlayerIds.Add(HumanPlayerId);
            finalStep.ParticipantPlayerIds.Add(AiPlayerId);
            ruleset.Steps.Add(finalStep);

            return ruleset;
        }

        private IEnumerable<PlayerMatchState> CreateMatchPlayers(int[] playerDeckIds, int[] aiDeckIds)
        {
            yield return new PlayerMatchState
            {
                PlayerId = HumanPlayerId,
                TeamId = HumanTeamId,
                MaxHp = 200,
                PersistentHp = 200,
                Deck = BuildPersistentDeck(playerDeckIds, HumanPlayerId),
                Loadout = CreateDefaultLoadout(),
            };

            yield return new PlayerMatchState
            {
                PlayerId = AiPlayerId,
                TeamId = AiTeamId,
                MaxHp = 200,
                PersistentHp = 200,
                Deck = BuildPersistentDeck(aiDeckIds, AiPlayerId),
                Loadout = CreateDefaultLoadout(),
            };
        }

        private PlayerLoadout CreateDefaultLoadout()
        {
            var loadout = new PlayerLoadout
            {
                ClassId = HeroClass.Warrior,
                DefaultBuildPoolId = BuildCatalogAssembler.GetDefaultPoolId(HeroClass.Warrior),
            };
            loadout.EquipmentIds.Add("burning_blood");
            return loadout;
        }

        private IEnumerable<EquipmentDefinition> CreateDefaultEquipmentDefinitions()
        {
            yield return new EquipmentDefinition
            {
                EquipmentId = "burning_blood",
                ClassId = HeroClass.Warrior,
                EffectType = EquipmentEffectType.HealAfterBattleFlat,
                EffectValue = 6,
            };
        }

        private PersistentDeckState BuildPersistentDeck(int[] cardIds, string ownerId)
        {
            var deck = new PersistentDeckState();
            var instanceIndex = 0;

            foreach (var id in cardIds)
            {
                if (CardConfigManager.Instance.GetCard(id) == null)
                {
                    OnLogMessage?.Invoke($"<color=#ffaa00>[警告] 找不到卡牌配置 {id}，已忽略。</color>");
                    continue;
                }

                string configId = id.ToString();
                deck.AddCard(new PersistentDeckCard
                {
                    PersistentCardId = $"{ownerId}_deck_{instanceIndex:D2}_{configId}",
                    BaseConfigId = configId,
                    CurrentConfigId = configId,
                    UpgradeLevel = 0,
                });
                instanceIndex++;
            }

            return deck;
        }

        private void SyncActiveBattle()
        {
            _ctx = _matchContext?.ActiveBattleContext;
            _roundManager = _matchContext?.ActiveRoundManager;
        }

        private void StartActiveBattle()
        {
            SyncActiveBattle();
            if (_ctx == null || _roundManager == null || _roundManager.IsBattleOver)
                return;

            if (_roundManager.CurrentRound == 0)
            {
                OnLogMessage?.Invoke(
                    $"<color=#99ddff>=== 进入第 {GetCurrentBattleNumber()}/{_totalBattleCount} 场：{GetCurrentBattleTitle()} ===</color>");
                _roundManager.BeginRound(_ctx);
                FlushLogs();
            }

            IsPlayerTurn = true;
            PublishBattlePhase(BattleClientPhaseKind.Operation, "操作期（无时限）", "操作中（无时限）");
            OnStateChanged?.Invoke();
        }

        private bool HandleBattleCompletion()
        {
            if (_matchContext == null || _roundManager == null || !_roundManager.IsBattleOver)
                return false;

            IsPlayerTurn = false;
            _matchManager.CompleteCurrentBattle(_matchContext, _eventBus);
            FlushMatchLogs();

            if (_matchContext.ActiveBuildWindow != null)
            {
                OpenLocalBuildWindow();
                return true;
            }

            SyncActiveBattle();

            if (_matchContext.IsMatchOver)
            {
                PublishDirectPhase(
                    BattleClientPhaseKind.MatchEnded,
                    "整局结束",
                    "整局结束");
                OnStateChanged?.Invoke();
                NotifyGameOver();
                return true;
            }

            StartActiveBattle();
            return true;
        }

        /// <summary>
        /// 打开构筑窗口，并将非本地玩家的选择按默认规则自动锁定。
        /// </summary>
        private void OpenLocalBuildWindow()
        {
            if (_matchContext?.ActiveBuildWindow == null)
                return;

            IsPlayerTurn = false;
            PublishDirectPhase(
                BattleClientPhaseKind.BuildWindow,
                $"第 {GetCurrentBattleNumber()}/{_totalBattleCount} 场结束 · 构筑阶段",
                "构筑阶段");

            AutoResolveRemoteBuildWindow();
            FlushMatchLogs();
            PublishBuildWindowState(opened: true);
            OnStateChanged?.Invoke();
        }

        private void FlushMatchLogs()
        {
            if (_matchContext == null || _matchContext.MatchLog.Count == 0)
                return;

            foreach (var raw in _matchContext.MatchLog)
                OnLogMessage?.Invoke(ColorizeMatchLog(raw));
            _matchContext.MatchLog.Clear();
        }

        private int GetCurrentBattleNumber()
        {
            if (_matchContext == null)
                return 0;

            return Math.Min(_matchContext.CurrentStepIndex + 1, _totalBattleCount);
        }

        private string GetCurrentBattleTitle()
        {
            if (_matchContext == null || _matchContext.Ruleset.Steps.Count == 0)
                return "战斗";

            return _matchContext.CurrentStepIndex == _matchContext.Ruleset.Steps.Count - 1
                ? "终局死斗"
                : "常规战";
        }

        private string BuildPhaseText(string phaseLabel)
        {
            return $"第 {GetCurrentBattleNumber()}/{_totalBattleCount} 场 · {GetCurrentBattleTitle()} · 第 {CurrentRound} 回合 · {phaseLabel}";
        }

        /// <summary>
        /// 将 Shared 的构筑窗口状态映射为客户端 UI 可消费的视图模型。
        /// </summary>
        private BuildWindowViewState CreateBuildWindowViewState()
        {
            if (_matchContext?.ActiveBuildWindow == null)
                return null;

            var viewState = new BuildWindowViewState
            {
                BattleIndex = GetCurrentBattleNumber(),
                TotalBattleCount = _totalBattleCount,
                BattleTitle = GetCurrentBattleTitle(),
                DisplayText = $"第 {GetCurrentBattleNumber()}/{_totalBattleCount} 场结束 · 构筑阶段",
                DeadlineUnixMs = _matchContext.ActiveBuildWindow.DeadlineUnixMs,
                LocalPlayerId = HumanPlayerId,
            };

            foreach (var playerId in BuildBuildWindowPlayerOrder())
            {
                if (!_matchContext.ActiveBuildWindow.PlayerWindows.TryGetValue(playerId, out var playerWindow))
                    continue;

                var playerView = CreatePlayerBuildWindowViewState(playerWindow);
                viewState.Players.Add(playerView);
                if (string.Equals(playerId, HumanPlayerId, StringComparison.Ordinal))
                    viewState.LocalPlayer = playerView;
            }

            if (viewState.LocalPlayer == null && viewState.Players.Count > 0)
                viewState.LocalPlayer = viewState.Players[0];

            return viewState;
        }

        private PlayerBuildWindowViewState CreatePlayerBuildWindowViewState(PlayerBuildWindowState playerWindow)
        {
            var playerView = new PlayerBuildWindowViewState
            {
                PlayerId = playerWindow.PlayerId,
                DisplayName = GetPlayerLabel(playerWindow.PlayerId),
                PreviewHp = playerWindow.PreviewHp,
                MaxHp = playerWindow.MaxHp,
                OpportunityCount = playerWindow.OpportunityCount,
                NextOpportunityIndex = playerWindow.NextOpportunityIndex,
                ResolvedOpportunityCount = playerWindow.Opportunities.Count(opportunity => opportunity.IsResolved),
                IsLocked = playerWindow.IsLocked,
                CanLock = !playerWindow.IsLocked,
                RestrictionMode = playerWindow.RestrictionMode == BuildWindowRestrictionMode.ForcedRecovery
                    ? BuildWindowRestrictionViewMode.ForcedRecovery
                    : BuildWindowRestrictionViewMode.None,
                RestrictionText = playerWindow.RestrictionMode == BuildWindowRestrictionMode.ForcedRecovery
                    ? $"本场战败，只能休息，回复 {Math.Round(playerWindow.HealPercent * 100)}% 生命"
                    : string.Empty,
            };

            foreach (var opportunity in playerWindow.Opportunities.Where(item => item.IsResolved && item.Choice != null))
                playerView.ResolvedChoiceSummaries.Add(DescribeResolvedBuildChoice(opportunity));

            if (!playerWindow.IsLocked && playerWindow.NextOpportunityIndex < playerWindow.Opportunities.Count)
                playerView.CurrentOpportunity = CreateOpportunityViewState(playerWindow.Opportunities[playerWindow.NextOpportunityIndex]);

            return playerView;
        }

        private BuildOpportunityViewState CreateOpportunityViewState(BuildOpportunityState opportunity)
        {
            var viewState = new BuildOpportunityViewState
            {
                OpportunityIndex = opportunity.OpportunityIndex,
                HealAmount = opportunity.Offers.HealAmount,
                CommittedActionType = ToViewActionType(opportunity.CommittedActionType),
                DraftGroupsRevealed = opportunity.Offers.DraftGroupsRevealed,
            };

            foreach (var actionType in opportunity.AvailableActions)
                viewState.AvailableActions.Add(ToViewActionType(actionType));

            foreach (var candidate in opportunity.Offers.UpgradableCards)
                viewState.UpgradableCards.Add(CreateBuildCardViewState(candidate.PersistentCardId, candidate.BaseConfigId, candidate.EffectiveConfigId, candidate.UpgradeLevel));

            foreach (var candidate in opportunity.Offers.RemovableCards)
                viewState.RemovableCards.Add(CreateBuildCardViewState(candidate.PersistentCardId, candidate.BaseConfigId, candidate.EffectiveConfigId, candidate.UpgradeLevel));

            foreach (var draftGroup in opportunity.Offers.DraftGroups)
            {
                var groupView = new BuildDraftGroupViewState
                {
                    GroupIndex = draftGroup.GroupIndex,
                };

                foreach (var offer in draftGroup.Offers)
                {
                    var config = GetConfigForBuildCard(offer.EffectiveConfigId, offer.BaseConfigId);
                    groupView.Offers.Add(new BuildDraftOfferViewState
                    {
                        OfferId = offer.OfferId,
                        PersistentCardId = offer.PersistentCardId,
                        ConfigId = offer.BaseConfigId,
                        EffectiveConfigId = offer.EffectiveConfigId,
                        DisplayName = config?.CardName ?? offer.EffectiveConfigId,
                        Description = config?.Description ?? "（无描述）",
                        RarityText = FormatBuildCardRarity(offer.Rarity),
                        Cost = config?.EnergyCost ?? 0,
                        UpgradeLevel = offer.UpgradeLevel,
                        IsUpgraded = offer.IsUpgraded,
                    });
                }

                viewState.DraftGroups.Add(groupView);
            }

            return viewState;
        }

        private BuildCardViewState CreateBuildCardViewState(string persistentCardId, string baseConfigId, string effectiveConfigId, int upgradeLevel)
        {
            var config = GetConfigForBuildCard(effectiveConfigId, baseConfigId);
            return new BuildCardViewState
            {
                PersistentCardId = persistentCardId,
                ConfigId = baseConfigId,
                EffectiveConfigId = effectiveConfigId,
                DisplayName = config?.CardName ?? effectiveConfigId,
                Description = config?.Description ?? "（无描述）",
                Cost = config?.EnergyCost ?? 0,
                UpgradeLevel = upgradeLevel,
            };
        }

        private CardConfig? GetConfigForBuildCard(string effectiveConfigId, string baseConfigId)
        {
            if (_cardConfigMap.TryGetValue(effectiveConfigId, out var effectiveConfig))
                return effectiveConfig;

            return _cardConfigMap.TryGetValue(baseConfigId, out var baseConfig) ? baseConfig : null;
        }

        private IEnumerable<string> BuildBuildWindowPlayerOrder()
        {
            yield return HumanPlayerId;
            yield return AiPlayerId;

            if (_matchContext?.ActiveBuildWindow == null)
                yield break;

            foreach (var playerId in _matchContext.ActiveBuildWindow.PlayerWindows.Keys)
            {
                if (playerId == HumanPlayerId || playerId == AiPlayerId)
                    continue;

                yield return playerId;
            }
        }

        private string DescribeResolvedBuildChoice(BuildOpportunityState opportunity)
        {
            if (opportunity.Choice == null)
                return $"第 {opportunity.OpportunityIndex + 1} 次：未选择";

            switch (opportunity.Choice.ActionType)
            {
                case BuildActionType.Heal:
                    return $"第 {opportunity.OpportunityIndex + 1} 次：休息（+{opportunity.Offers.HealAmount}）";

                case BuildActionType.UpgradeCard:
                    {
                        var target = opportunity.Offers.UpgradableCards.FirstOrDefault(card =>
                            string.Equals(card.PersistentCardId, opportunity.Choice.TargetPersistentCardId, StringComparison.Ordinal));
                        string name = target != null ? ResolveCardName(target.EffectiveConfigId) : "未知卡牌";
                        return $"第 {opportunity.OpportunityIndex + 1} 次：升级【{name}】";
                    }

                case BuildActionType.RemoveCard:
                    {
                        var target = opportunity.Offers.RemovableCards.FirstOrDefault(card =>
                            string.Equals(card.PersistentCardId, opportunity.Choice.TargetPersistentCardId, StringComparison.Ordinal));
                        string name = target != null ? ResolveCardName(target.EffectiveConfigId) : "未知卡牌";
                        return $"第 {opportunity.OpportunityIndex + 1} 次：删去【{name}】";
                    }

                case BuildActionType.AddCard:
                    {
                        var names = new List<string>();
                        foreach (var group in opportunity.Offers.DraftGroups)
                        {
                            if (!opportunity.Choice.SelectedDraftOfferIdsByGroup.TryGetValue(group.GroupIndex, out var offerId))
                                continue;

                            var offer = group.Offers.FirstOrDefault(item => string.Equals(item.OfferId, offerId, StringComparison.Ordinal));
                            if (offer != null)
                                names.Add(ResolveCardName(offer.EffectiveConfigId));
                        }

                        return names.Count == 0
                            ? $"第 {opportunity.OpportunityIndex + 1} 次：拿牌（全部跳过）"
                            : $"第 {opportunity.OpportunityIndex + 1} 次：拿牌（{string.Join("、", names)}）";
                    }
            }

            return $"第 {opportunity.OpportunityIndex + 1} 次：{opportunity.Choice.ActionType}";
        }

        private void PublishBuildWindowState(bool opened)
        {
            _currentBuildWindow = CreateBuildWindowViewState();
            if (_currentBuildWindow == null)
                return;

            LogBuildWindowDebugSnapshot(_currentBuildWindow, opened);

            if (opened)
                OnBuildWindowOpened?.Invoke(_currentBuildWindow);
            else
                OnBuildWindowUpdated?.Invoke(_currentBuildWindow);
        }

        private void CloseBuildWindow()
        {
            if (_currentBuildWindow == null && _matchContext?.ActiveBuildWindow == null)
                return;

            _currentBuildWindow = null;
            OnBuildWindowClosed?.Invoke();
        }

        private void AutoResolveRemoteBuildWindow()
        {
            if (_matchContext?.ActiveBuildWindow == null)
                return;

            foreach (var playerWindow in _matchContext.ActiveBuildWindow.PlayerWindows.Values)
            {
                if (string.Equals(playerWindow.PlayerId, HumanPlayerId, StringComparison.Ordinal) || playerWindow.IsLocked)
                    continue;

                _matchManager.LockBuildChoice(_matchContext, playerWindow.PlayerId);
            }
        }

        private void PublishBattlePhase(BattleClientPhaseKind phaseKind, string phaseLabel, string timerText)
        {
            PublishDirectPhase(phaseKind, BuildPhaseText(phaseLabel), timerText);
        }

        private void PublishDirectPhase(BattleClientPhaseKind phaseKind, string displayText, string timerText)
        {
            OnPhaseChanged?.Invoke(new BattlePhaseViewState
            {
                PhaseKind = phaseKind,
                DisplayText = displayText,
                TimerText = timerText,
                UseOperationTimer = false,
            });
        }

        private static BuildActionViewType ToViewActionType(BuildActionType actionType)
        {
            return actionType switch
            {
                BuildActionType.Heal => BuildActionViewType.Heal,
                BuildActionType.AddCard => BuildActionViewType.AddCard,
                BuildActionType.RemoveCard => BuildActionViewType.RemoveCard,
                BuildActionType.UpgradeCard => BuildActionViewType.UpgradeCard,
                _ => BuildActionViewType.None,
            };
        }

        private static BuildActionType ToRuntimeActionType(BuildActionViewType actionType)
        {
            return actionType switch
            {
                BuildActionViewType.Heal => BuildActionType.Heal,
                BuildActionViewType.AddCard => BuildActionType.AddCard,
                BuildActionViewType.RemoveCard => BuildActionType.RemoveCard,
                BuildActionViewType.UpgradeCard => BuildActionType.UpgradeCard,
                _ => BuildActionType.None,
            };
        }

        private static BuildChoice ToRuntimeBuildChoice(BuildChoiceViewState choice)
        {
            var runtimeChoice = new BuildChoice
            {
                ActionType = ToRuntimeActionType(choice.ActionType),
                TargetPersistentCardId = choice.TargetPersistentCardId,
            };

            foreach (var pair in choice.SelectedDraftOfferIdsByGroup)
                runtimeChoice.SelectedDraftOfferIdsByGroup[pair.Key] = pair.Value;

            return runtimeChoice;
        }

        private static string FormatBuildCardRarity(BuildCardRarity rarity)
        {
            return rarity switch
            {
                BuildCardRarity.Common => "普通",
                BuildCardRarity.Uncommon => "罕见",
                BuildCardRarity.Rare => "稀有",
                BuildCardRarity.Legendary => "传说",
                _ => rarity.ToString(),
            };
        }

        private void LogBuildWindowDebugSnapshot(BuildWindowViewState viewState, bool opened)
        {
            if (viewState == null)
                return;

            foreach (var player in viewState.Players)
            {
                string header = opened ? "打开构筑窗口" : "更新构筑窗口";
                OnLogMessage?.Invoke(
                    $"<color=#aaaaff>[构筑调试] {header}：{player.DisplayName} HP={player.PreviewHp}/{player.MaxHp} 已完成={player.ResolvedOpportunityCount}/{player.OpportunityCount} 锁定={player.IsLocked}</color>");

                if (player.CurrentOpportunity == null)
                {
                    OnLogMessage?.Invoke($"<color=#aaaaff>[构筑调试] {player.DisplayName} 当前无可编辑机会。</color>");
                    continue;
                }

                var opportunity = player.CurrentOpportunity;
                string actions = opportunity.AvailableActions.Count == 0
                    ? "无"
                    : string.Join("、", opportunity.AvailableActions.Select(action => action.ToString()));
                string upgradeNames = opportunity.UpgradableCards.Count == 0
                    ? "无"
                    : string.Join("、", opportunity.UpgradableCards.Select(card => card.DisplayName));
                string removeNames = opportunity.RemovableCards.Count == 0
                    ? "无"
                    : string.Join("、", opportunity.RemovableCards.Select(card => card.DisplayName));
                string draftGroups = !opportunity.DraftGroupsRevealed
                    ? "未揭示"
                    : opportunity.DraftGroups.Count == 0
                        ? "无"
                        : string.Join(" | ", opportunity.DraftGroups.Select(group =>
                            $"组{group.GroupIndex + 1}:{string.Join("、", group.Offers.Select(offer => offer.DisplayName))}"));
                string committedAction = opportunity.CommittedActionType == BuildActionViewType.None
                    ? "无"
                    : opportunity.CommittedActionType.ToString();

                OnLogMessage?.Invoke(
                    $"<color=#aaaaff>[构筑调试] {player.DisplayName} 第 {opportunity.OpportunityIndex + 1} 次机会：动作={actions}，已承诺={committedAction}，回血={opportunity.HealAmount}，升级候选={opportunity.UpgradableCards.Count}（{upgradeNames}），删牌候选={opportunity.RemovableCards.Count}（{removeNames}），拿牌组={opportunity.DraftGroups.Count}（{draftGroups}）</color>");
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

        private static bool RequiresDiscardSelection(CardConfig? config)
        {
            if (config?.Effects == null)
                return false;

            foreach (var effect in config.Effects)
            {
                if (effect.EffectType == EffectType.MoveSelectedCardToDeckTop)
                    return true;
            }

            return false;
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
            string? winner = _matchContext?.WinnerTeamId ?? _roundManager?.WinnerId;
            int code = winner == null ? -1
                     : string.Equals(winner, HumanTeamId, StringComparison.Ordinal) || string.Equals(winner, HumanPlayerId, StringComparison.Ordinal) ? 1
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

        private static string ColorizeMatchLog(string log)
        {
            if (log.Contains("<color="))
                return log;

            if (log.Contains("[Equipment]"))
                return $"<color=#66ee88>{log}</color>";
            if (log.Contains("[MatchManager]") || log.Contains("[MatchFactory]"))
                return $"<color=#99ddff>{log}</color>";

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
            return playerId == HumanPlayerId || playerId == HumanTeamId ? "你"
                : playerId == AiPlayerId || playerId == AiTeamId ? "对手"
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


