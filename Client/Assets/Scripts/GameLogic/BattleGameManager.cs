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
    /// ս�����̹�������V2�������� UI ��� BattleCore V2��
    ///
    /// ְ��
    ///   - ͨ�� BattleFactory ����������һ��ս����������������
    ///   - �ṩ UI ����õĲ����ӿڣ�������ƺͽ����غ�
    ///   - ���ü� AI ���ƶ�����Ϊ
    ///   - ͨ�� C# �¼�֪ͨ UI ��ˢ����ʾ
    ///
    /// �ܹ���
    ///   BattleUIManager (Presentation)
    ///     -> BattleGameManager (GameLogic)
    ///       -> BattleFactory / RoundManager (BattleCore V2)
    /// </summary>
    public class BattleGameManager
    {
        // �T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T
        // UI �㶩���¼�
        // �T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T

        /// <summary>�Ծ�״̬�����仯ʱ������HP / ���� / ���Ƶȱ仯��</summary>
        public event Action OnStateChanged;

        /// <summary>������־��Ϣʱ����������Ϊ�ɴ� TMP RichText ��ǩ���ַ�����</summary>
        public event Action<string> OnLogMessage;

        /// <summary>
        /// �Ծֽ���ʱ������
        /// ���� winnerCode��1 = ���ʤ��2 = AI ʤ��-1 = ƽ�֡�
        /// </summary>
        public event Action<int> OnGameOver;

        /// <summary>�غϽ׶��л�ʱ���������ڸ��� phaseText ��������ʱ����</summary>
        public event Action<string> OnPhaseChanged;

        // �T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T
        // ��� ID ����
        // �T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T

        public const string HumanPlayerId = "player1";
        public const string AiPlayerId    = "player2";

        // �T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T
        // V2 ���Ķ���
        // �T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T

        private BattleContext _ctx;
        private RoundManager  _roundManager;

        // configId -> CardConfig ӳ�䣬�� BattleFactory ��ʼ������ BuildCardConfigMap ��䡣
        private readonly Dictionary<string, CardConfig> _cardConfigMap
            = new Dictionary<string, CardConfig>();

        // �T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T
        // ����״̬���ԣ�UI ��ֻ����
        // �T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T

        /// <summary>��ǰ BattleContext��</summary>
        public BattleContext Context => _ctx;

        /// <summary>�Ƿ�����Ҳ����׶�</summary>
        public bool IsPlayerTurn { get; private set; }

        /// <summary>�Ծ��Ƿ��ѽ�����</summary>
        public bool IsGameOver => _roundManager?.IsBattleOver ?? false;

        /// <summary>��ǰ�غ�����</summary>
        public int CurrentRound => _roundManager?.CurrentRound ?? 0;

        // �T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T
        // ս�����
        // �T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T

        /// <summary>
        /// ��ʼһ���µ� 1v1 ��ս��ʹ��Ĭ��սʿ���Կ��顣
        /// </summary>
        public void StartBattle()
        {
            StartBattleWithDeck(DefaultWarriorDeckIds, DefaultWarriorDeckIds);
        }

        /// <summary>
        /// ʹ��ָ������ ID �б��ʼ��ս��
        /// </summary>
        public void StartBattleWithDeck(int[] playerDeckIds, int[] aiDeckIds)
        {
            EnsureConfigLoaded();
            _cardConfigMap.Clear();

            // ���� ���� configId ӳ������������ instanceId �������� ����
            BuildCardConfigMap();

            // ���� ���� DeckConfig ������������������������������������������������������������������������
            var humanDeck = BuildDeckConfig(playerDeckIds);
            var aiDeck    = BuildDeckConfig(aiDeckIds);

            // ���� ���� EventBus ������ ������������������������������������������������������������
            var eventBus = new InternalEventBus(this);

            // ���� ͨ�� BattleFactory ����ս�� ��������������������������������������������
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

            // ��� setup ��־
            foreach (var log in result.SetupLog)
                OnLogMessage?.Invoke(ColorizeLog(log));

            // ���� ��ʼ��һ�غ� ����������������������������������������������������������������������������
            _roundManager.BeginRound(_ctx);
            FlushLogs();

            IsPlayerTurn = true;
            OnPhaseChanged?.Invoke($"�� {_roundManager.CurrentRound} �غ� �� ��Ĳ���");
            OnStateChanged?.Invoke();
        }

        // �T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T
        // ��Ҳ����ӿ�
        // �T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T

        /// <summary>
        /// ��Ҵ��һ��˲���ƣ��������㡣
        /// </summary>
        /// <param name="handIndex">��������������б��е�λ�á�</param>
        /// <returns>�����������</returns>
        public string PlayerPlayInstantCard(int handIndex)
        {
            if (!IsPlayerTurn || IsGameOver) return "��ǰ�޷�����";
            return PlayCardInternal(HumanPlayerId, handIndex, instant: true, runtimeParams: null);
        }

        public string PlayerPlayInstantCard(int handIndex, Dictionary<string, string> runtimeParams)
        {
            if (!IsPlayerTurn || IsGameOver) return "��ǰ�޷�����";
            return PlayCardInternal(HumanPlayerId, handIndex, instant: true, runtimeParams);
        }

        /// <summary>
        /// ����ύһ�Ŷ����ƣ��ȴ� EndRound ���㡣
        /// </summary>
        public string PlayerCommitPlanCard(int handIndex)
        {
            if (!IsPlayerTurn || IsGameOver) return "��ǰ�޷�����";
            return PlayCardInternal(HumanPlayerId, handIndex, instant: false, runtimeParams: null);
        }

        public string PlayerCommitPlanCard(int handIndex, Dictionary<string, string> runtimeParams)
        {
            if (!IsPlayerTurn || IsGameOver) return "��ǰ�޷�����";
            return PlayCardInternal(HumanPlayerId, handIndex, instant: false, runtimeParams);
        }

        /// <summary>
        /// ��ҽ����غϣ�AI ���� -> ���߽��� -> ��һ�غϿ�ʼ��
        /// </summary>
        public void PlayerEndTurn()
        {
            if (!IsPlayerTurn || IsGameOver) return;

            IsPlayerTurn = false;

            // ���� AI �����׶� ��������������������������������������������������������������������������������
            OnPhaseChanged?.Invoke($"�� {_roundManager.CurrentRound} �غ� �� ���ֲ���...");
            OnStateChanged?.Invoke();
            ExecuteAiTurn();
            FlushLogs();

            if (IsGameOver) { NotifyGameOver(); return; }

            // ���� ���������� ������������������������������������������������������������������������������
            OnPhaseChanged?.Invoke($"�� {_roundManager.CurrentRound} �غ� �� ������...");
            _roundManager.EndRound(_ctx);
            FlushLogs();
            OnStateChanged?.Invoke();

            if (IsGameOver) { NotifyGameOver(); return; }

            // ���� ��һ�غ� ��������������������������������������������������������������������������������������
            _roundManager.BeginRound(_ctx);
            FlushLogs();

            IsPlayerTurn = true;
            OnPhaseChanged?.Invoke($"�� {_roundManager.CurrentRound} �غ� �� ��Ĳ���");
            OnStateChanged?.Invoke();
        }

        // �T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T
        // ���ݷ��ʣ�UI ����ã�
        // �T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T

        /// <summary>��ȡ����������ݣ�V2 PlayerData����</summary>
        public PlayerData GetHumanPlayer() => _ctx?.GetPlayer(HumanPlayerId);

        /// <summary>��ȡ AI ������ݣ�V2 PlayerData����</summary>
        public PlayerData GetAiPlayer() => _ctx?.GetPlayer(AiPlayerId);

        /// <summary>
        /// ��ȡ����������ƣ�����Ӧ CardConfig ��ʾ��Ϣ����
        /// �����б�˳���� PlayerData.Hand �е� BattleCard ˳��һ�¡�
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
                return "��";

            var buffs = _ctx.BuffManager.GetBuffs(player.HeroEntity.EntityId);
            if (buffs.Count == 0)
                return "��";

            var parts = new List<string>(buffs.Count);
            foreach (var buff in buffs)
                parts.Add(FormatBuff(buff));

            return string.Join(" / ", parts);
        }

        // �T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T
        // ����
        // �T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T

        /// <summary>��ӡս������״̬���գ�ͨ�� OnLogMessage ���͸� UI��</summary>
        public void PrintBattleStatus()
        {
            if (_ctx == null)
            {
                OnLogMessage?.Invoke("<color=#ff4444>[״̬����] BattleContext Ϊ�գ��Ծ���δ��ʼ��</color>");
                return;
            }

            var sb = new System.Text.StringBuilder();
            sb.AppendLine("<color=#ffffff>�X�T�T�T�T�T�T�T�T�T�T [ս��״̬����] �T�T�T�T�T�T�T�T�T�T�[</color>");
            sb.AppendLine($"<color=#aaaaaa>  �� {_roundManager.CurrentRound} �غ�</color>");
            sb.AppendLine();
            AppendPlayerSnapshot(sb, _ctx.GetPlayer(HumanPlayerId), "�ҷ�");
            sb.AppendLine();
            AppendPlayerSnapshot(sb, _ctx.GetPlayer(AiPlayerId),    "����");
            sb.AppendLine("<color=#ffffff>�^�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�a</color>");

            foreach (var line in sb.ToString().Split('\n'))
                if (!string.IsNullOrWhiteSpace(line))
                    OnLogMessage?.Invoke(line.TrimEnd('\r'));
        }

        // �T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T
        // �ڲ������ƺ����߼�
        // �T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T

        private string PlayCardInternal(
            string playerId,
            int handIndex,
            bool instant,
            Dictionary<string, string>? runtimeParams)
        {
            var player = _ctx.GetPlayer(playerId);
            if (player == null) return "��Ҳ�����";

            var hand = player.GetCardsInZone(CardZone.Hand);
            if (handIndex < 0 || handIndex >= hand.Count)
                return $"��������Խ�磨{handIndex}/{hand.Count}��";

            var battleCard = hand[handIndex];
            var cardConfig = GetEffectiveCardConfig(battleCard);
            if (cardConfig == null)
                return $"�Ҳ����������� configId={battleCard.GetEffectiveConfigId()}";

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
                string reason = $"�������㣨��ǰ {player.Energy}����Ҫ {cost}��";
                OnLogMessage?.Invoke($"<color=#ff8866>[!] {reason}</color>");
                return reason;
            }

            bool hadForceConsumeFlag = battleCard.ExtraData.TryGetValue("forceConsumeAfterResolve", out var previousForceConsumeFlag);
            if (playRules.ForceConsumeAfterResolve)
                battleCard.ExtraData["forceConsumeAfterResolve"] = true;

            // ���� Ԥ��������ʧ��ʱ�ع� ������������������������������������������������������������������
            player.Energy -= cost;

            string cardName = cardConfig.CardName;
            bool success;
            List<EffectResult>? instantResults = null;

            // ���� ���� ��������������������������������������������������������������������������������������������������
            if (instant)
            {
                // ˲���ƣ����Ƴ����������ٽ���
                instantResults = _roundManager.PlayInstantCard(_ctx, playerId, battleCard.InstanceId, runtimeParams);
                success = instantResults.Count > 0 || battleCard.Zone != CardZone.Hand;
                FlushLogs();
            }
            else
            {
                // �����ƣ�������������ȴ� EndRound ͳһ����
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

                string reason = "����ʧ��";
                OnLogMessage?.Invoke($"<color=#ff8866>[!] {reason}</color>");
                return reason;
            }

            _roundManager.CommitSuccessfulPlayRules(_ctx, playerId, playRules);

            if (instant)
            {
                OnLogMessage?.Invoke($"<color=#aaffaa>{(playerId == HumanPlayerId ? "��" : "����")} ���˲���ơ�{cardName}�������� {cost} ��������</color>");
                LogInstantEffectResults(playerId, cardName, instantResults);
            }
            else
            {
                OnLogMessage?.Invoke($"<color=#aaddff>{(playerId == HumanPlayerId ? "��" : "����")} �ύ�����ơ�{cardName}�������� {cost} ��������</color>");
            }

            OnStateChanged?.Invoke();
            if (IsGameOver) NotifyGameOver();
            return cardName;
        }

        /// <summary>
        /// ���ƺ���ݿ��Ʊ�ǩ��������ȥ��
        /// Exhaust ��ǩ��ʾ����Ϸ���Ƴ�����ͨ�ƽ������ƶѡ�
        /// </summary>
        private void MoveCardAfterPlay(BattleCard battleCard, CardConfig cardConfig)
        {
            bool isExhaust = cardConfig.Tags.HasFlag(CardTag.Exhaust);
            if (isExhaust)
            {
                // �����ƣ��� AllCards �г����Ƴ�
                var owner = _ctx.GetPlayer(battleCard.OwnerId);
                owner?.AllCards.Remove(battleCard);
                _ctx.RoundLog.Add($"[BattleGameManager] ���ơ�{cardConfig.CardName}�������ģ�Exhaust����");
            }
            else
            {
                // ��ͨ�ƣ��������ƶ�
                _ctx.CardManager.MoveCard(_ctx, battleCard, CardZone.Discard);
            }
        }

        // �T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T
        // �ڲ���AI �߼�
        // �T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T

        private void ExecuteAiTurn()
        {
            var player = _ctx.GetPlayer(AiPlayerId);
            if (player == null || !player.HeroEntity.IsAlive) return;

            // �򵥲��ԣ����������ƶ��ύ�������ã������������Ӿ���
            var hand = player.GetCardsInZone(CardZone.Hand);
            var snapshot = new List<BattleCard>(hand); // ��ֹ����ʱ�б���

            foreach (var battleCard in snapshot)
            {
                if (!player.HeroEntity.IsAlive || IsGameOver) break;
                var cfg = GetEffectiveCardConfig(battleCard);
                if (cfg == null) continue;

                bool isInstant = cfg.TrackType == CardTrackType.Instant;
                PlayCardInternal(AiPlayerId, 0, isInstant, runtimeParams: null);
            }
        }

        // �T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T
        // �ڲ������鹹��
        // �T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T

        /// <summary>
        /// ����ս���Կ��飨13 �ţ���
        ///   ��� ��4 + ���� ��3 + �۲����� ��2 + �ɽ�����ն ��2 + ս��רע ��1 + ͻ�Ƽ��� ��1
        /// </summary>
        /* Legacy V1 warrior demo deck removed from active use.
        {
            2001, 2001, 2001, 2001,   // ��� ��4        (1�ѣ����ߣ����6�˺�)
            2002, 2002, 2002,         // ���� ��3        (1�ѣ����ߣ���û���)
            2003, 2003,               // �۲����� ��2    (1�ѣ����ߣ������������)
            2005, 2005,               // �ɽ�����ն ��2  (1�ѣ����ߣ�2���˺�)
            1001,                     // ս��רע ��1    (0�ѣ�˲�ߣ���3��)
            1002,                     // ͻ�Ƽ��� ��1    (1�ѣ�˲�ߣ��������������)
            2008, 2008,               // ��ŭ x2
            1001,                     // ս��רע ��1    (0�ѣ�˲�ߣ���3��)
            1002,                     // ͻ�Ƽ��� ��1    (1�ѣ�˲�ߣ��������������)
        };

        */
        private static readonly int[] DefaultWarriorDeckIds = new int[]
        {
            2001, 2001, 2001,         // �������� x3
            1001, 1001, 1001,         // �ֶ���ǰ x3
            1002, 1002,               // ���� x2
            1003, 1003,               // ��Ѫ x2
            1004, 1004,               // ƣ���о� x2
            1005,                     // ������� x1
            1008,                     // ��ʰ x1
            2002, 2002,               // ��Ѫ��Ѫ x2
            2003, 2003,               // ��Ѫ���� x2
            2004,                     // �����ո� x1
            2005, 2005,               // �����ͻ� x2
            2006,                     // ȫ��һ�� x1
            2007, 2007,               // ˺�� x2
            2008, 2008,               // ��ŭ x2
            2009, 2009,               // ������ x2
            2010, 2010,               // ʹ�� x2
            2011, 2011,               // ���� x2
            2013,                     // Ѫ�� x1
            1006,                     // ��װ x1
            2015,                     // ���� x1
        };

        private static BuffConfig? ResolveRuntimeBuffConfig(string buffId)
        {
            return buffId switch
            {
                "strength" => new BuffConfig
                {
                    BuffId = "strength",
                    BuffName = "����",
                    Description = "������ɵ��˺�",
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
                    BuffName = "����",
                    Description = "��ɵ��˺����� 25%",
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
                    BuffName = "����",
                    Description = "�ܵ����˺���� 50%",
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
                    BuffName = "���غϽ�ֹ����",
                    Description = "���غ�ʣ��ʱ�����޷��ٳ���",
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
                    BuffName = "���غϽ�ֹ�˺���",
                    Description = "���غ�ʣ��ʱ�����޷��ٴ���˺���",
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
                    BuffName = "�»غ�����",
                    Description = "�»غϿ�ʼʱ����õ�ֵ����",
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
                    BuffName = "Ѫ��",
                    Description = "ÿ����ʧȥ����ʱ���������",
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
                    BuffName = "����",
                    Description = "ÿ�غ�ǰ X ���Ʒ��ñ�Ϊ 0���ҽ��������",
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
                    OnLogMessage?.Invoke($"<color=#ffaa00>[����] �������ò����� {kv.Key}��������</color>");
            }
            return deck;
        }

        /// <summary>
        /// ���� configId���ַ�����ʽ CardId���� CardConfig ��ӳ����
        /// �� PlayCardInternal ���õ� BattleCard.ConfigId ����ٲ������á�
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

        // �T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T
        // �ڲ�������
        // �T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T

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

            if (lower.Contains("�˺�") || lower.Contains("����") || lower.Contains("�۳�"))
                return $"<color=#ff8866>{log}</color>";
            if (lower.Contains("����") || lower.Contains("shield"))
                return $"<color=#66aaff>{log}</color>";
            if (lower.Contains("����") || lower.Contains("��Ѫ") || lower.Contains("�ָ�"))
                return $"<color=#66ee88>{log}</color>";
            if (lower.Contains("����") || lower.Contains("buff") || lower.Contains("����"))
                return $"<color=#ffdd55>{log}</color>";
            if (lower.Contains("�غ�") && (log.Contains("�T�T") || log.Contains("����")))
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
                $"<color=#cceeff>[Ч��] {GetPlayerLabel(playerId)}�ġ�{cardName}����{string.Join("��", parts)}</color>");
        }

        private string? BuildEffectSummary(EffectResult result)
        {
            if (result == null || !result.Success)
                return null;

            switch (result.Type)
            {
                case EffectType.Damage:
                case EffectType.Pierce:
                    return result.TotalRealHpDamage > 0 ? $"��� {result.TotalRealHpDamage} �������˺�" : null;

                case EffectType.Heal:
                case EffectType.Lifesteal:
                    return result.TotalRealHeal > 0 ? $"�ָ� {result.TotalRealHeal} ������" : null;

                case EffectType.Shield:
                    return result.TotalRealShield > 0 ? $"��� {result.TotalRealShield} �㻤��" : null;

                case EffectType.Draw:
                    return TryGetExtraInt(result, "drawnCount", out var drawnCount) && drawnCount > 0
                        ? $"�� {drawnCount} ����"
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
                        ? $"���� {buffName}��{string.Join("��", buffParts)}��"
                        : $"���� {buffName}";

                case EffectType.GainEnergy:
                    return TryGetExtraInt(result, "gainedEnergy", out var gainedEnergy) && gainedEnergy > 0
                        ? $"��� {gainedEnergy} ������"
                        : null;

                case EffectType.GenerateCard:
                    if (!TryGetExtraInt(result, "generatedCount", out var generatedCount) || generatedCount <= 0)
                        return null;

                    string generatedConfigId = TryGetExtraString(result, "generatedConfigId") ?? string.Empty;
                    string generatedName = ResolveCardName(generatedConfigId);
                    string generatedZone = TryGetExtraString(result, "generatedZone") ?? "Hand";
                    return $"���� {generatedCount} �š�{generatedName}����{FormatZoneName(generatedZone)}";

                case EffectType.MoveSelectedCardToDeckTop:
                    string selectedConfigId = TryGetExtraString(result, "selectedCardConfigId") ?? string.Empty;
                    return !string.IsNullOrWhiteSpace(selectedConfigId)
                        ? $"����{ResolveCardName(selectedConfigId)}�������ƶѶ�"
                        : "��ѡ������������ƶѶ�";

                case EffectType.UpgradeCardsInHand:
                    return TryGetExtraInt(result, "upgradedCount", out var upgradedCount) && upgradedCount > 0
                        ? $"���� {upgradedCount} ������"
                        : null;

                case EffectType.ReturnSourceCardToHandAtRoundEnd:
                    return "���غϽ���ʱ��������";
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
                ? $"{name}({string.Join("��", parts)})"
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
                return "����";

            if (remainingRounds == 0)
                return string.Empty;

            return $"{remainingRounds}�غ�";
        }

        private string GetBuffDisplayName(string buffConfigId)
        {
            if (string.IsNullOrWhiteSpace(buffConfigId))
                return "δ֪Buff";

            var buffConfig = ResolveRuntimeBuffConfig(buffConfigId);
            if (buffConfig != null && !string.IsNullOrWhiteSpace(buffConfig.BuffName))
                return buffConfig.BuffName;

            return buffConfigId;
        }

        private string ResolveCardName(string configId)
        {
            if (string.IsNullOrWhiteSpace(configId))
                return "δ֪����";

            return _cardConfigMap.TryGetValue(configId, out var config)
                ? config.CardName
                : configId;
        }

        private string GetPlayerLabel(string playerId)
        {
            return playerId == HumanPlayerId ? "��"
                : playerId == AiPlayerId ? "����"
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
                "deck" => "�ƶ�",
                "discard" => "���ƶ�",
                "consume" => "������",
                _ => "����",
            };
        }

        private void AppendPlayerSnapshot(System.Text.StringBuilder sb, PlayerData? p, string label)
        {
            if (p == null) { sb.AppendLine($"  [{label}]: ���ݲ�����"); return; }

            var hero    = p.HeroEntity;
            var hand    = p.GetCardsInZone(CardZone.Hand);
            var deck    = p.GetCardsInZone(CardZone.Deck);
            var discard = p.GetCardsInZone(CardZone.Discard);

            string hpColor = hero.Hp <= hero.MaxHp / 3 ? "#ff4444"
                           : hero.Hp <= hero.MaxHp * 2 / 3 ? "#ffaa33"
                           : "#66ee88";

            sb.AppendLine($"  <color=#ddddff>[{label}]</color>");
            sb.AppendLine($"    HP    : <color={hpColor}>{hero.Hp}/{hero.MaxHp}</color>"
                + (hero.Shield > 0 ? $"   ����: <color=#66aaff>{hero.Shield}</color>" : "")
                + (hero.Armor  > 0 ? $"   ����: <color=#88ccff>{hero.Armor}</color>" : ""));
            sb.AppendLine($"    ����  : <color=#ffdd55>{p.Energy}/{p.MaxEnergy}</color>");
            sb.AppendLine($"    ����  : {hand.Count}  |  �ƿ�: {deck.Count}   ����: {discard.Count}");
            sb.AppendLine($"    Buff  : {GetPlayerBuffSummary(p.PlayerId)}");
        }

        // �T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T
        // �ڲ���EventBus ����
        // �T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T�T

        /// <summary>
        /// �� V2 BattleCore �ڲ��¼�ת���� BattleGameManager �� C# �¼��� UI ��־��
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
                                $"<color=#ff6666>[�˺�] {_mgr.GetEntityLabel(dmg.SourceEntityId)} -> {_mgr.GetEntityLabel(dmg.TargetEntityId)} {dmg.RealHpDamage} ��"
                                + (dmg.ShieldAbsorbed > 0 ? $"���������� {dmg.ShieldAbsorbed}��" : "")
                                + "</color>");

                        else if (dmg.ShieldAbsorbed > 0)
                            _mgr.OnLogMessage?.Invoke(
                                $"<color=#66aaff>[����] {_mgr.GetEntityLabel(dmg.TargetEntityId)} ���� {dmg.ShieldAbsorbed} ���˺�</color>");
                        break;

                    case HealEvent heal:
                        _mgr.OnLogMessage?.Invoke(
                            $"<color=#66ee88>[����] {_mgr.GetEntityLabel(heal.TargetEntityId)} �ָ� {heal.RealHealAmount} ������</color>");
                        break;

                    case ShieldGainedEvent sg:
                        _mgr.OnLogMessage?.Invoke(
                            $"<color=#66aaff>[����] {_mgr.GetEntityLabel(sg.TargetEntityId)} ��� {sg.ShieldAmount} �㻤��</color>");
                        break;

                    case BuffAddedEvent buffAdded:
                        _mgr.OnLogMessage?.Invoke(
                            $"<color=#ffdd55>[Buff] {_mgr.GetEntityLabel(buffAdded.TargetEntityId)} ��� {_mgr.FormatBuff(buffAdded.Buff)}</color>");
                        break;

                    case BuffRemovedEvent buffRemoved:
                        _mgr.OnLogMessage?.Invoke(
                            $"<color=#cccccc>[Buff] {_mgr.GetEntityLabel(buffRemoved.TargetEntityId)} ʧȥ {_mgr.GetBuffDisplayName(buffRemoved.BuffConfigId)}</color>");
                        break;

                    case RoundStartEvent rs:
                        _mgr.OnLogMessage?.Invoke(
                            $"<color=#888888><size=85%>--- �� {rs.Round} �غϿ�ʼ ---</size></color>");
                        break;

                    case RoundEndEvent re:
                        _mgr.OnLogMessage?.Invoke(
                            $"<color=#888888><size=85%>--- �� {re.Round} �غϽ��� ---</size></color>");
                        break;

                    case PlayerDeathEvent death:
                        _mgr.OnLogMessage?.Invoke(
                            $"<color=#ff4444>[����] {death.PlayerId}</color>");
                        break;

                    case BattleEndEvent end:
                        _mgr.OnLogMessage?.Invoke(end.IsDraw
                            ? "<color=#ffdd55>[����] ƽ��</color>"
                            : $"<color=#ffdd55>[����] ʤ�ߣ�{end.WinnerId}</color>");
                        break;
                }
            }
        }
    }
}


