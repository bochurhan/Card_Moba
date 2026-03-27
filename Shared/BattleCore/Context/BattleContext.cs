#pragma warning disable CS8632

using System.Collections.Generic;
using CardMoba.BattleCore.EventBus;
using CardMoba.BattleCore.Definitions;
using CardMoba.BattleCore.Foundation;
using CardMoba.BattleCore.Modifiers;
using CardMoba.BattleCore.Rules.Draw;
using CardMoba.BattleCore.Rules.Play;

namespace CardMoba.BattleCore.Context
{
    /// <summary>
    /// 一局战斗的唯一运行时状态容器。
    /// </summary>
    public class BattleContext
    {
        public string BattleId { get; }
        public int CurrentRound { get; set; }
        public BattlePhase CurrentPhase { get; set; }

        private readonly Dictionary<string, PlayerData> _players = new Dictionary<string, PlayerData>();

        public void RegisterPlayer(PlayerData player)
        {
            _players[player.PlayerId] = player;
        }

        public PlayerData? GetPlayer(string playerId)
        {
            _players.TryGetValue(playerId, out var player);
            return player;
        }

        public PlayerData? GetPlayerByEntityId(string entityId)
        {
            foreach (var player in _players.Values)
            {
                if (player.HeroEntity?.EntityId == entityId)
                    return player;
            }

            return null;
        }

        public IReadOnlyDictionary<string, PlayerData> AllPlayers => _players;

        public Entity? GetEntity(string entityId)
        {
            foreach (var player in _players.Values)
            {
                if (player.HeroEntity?.EntityId == entityId)
                    return player.HeroEntity;
            }

            return null;
        }

        public PendingEffectQueue PendingQueue { get; } = new PendingEffectQueue();
        public List<PendingPlanSnapshot> PendingPlanSnapshots { get; } = new List<PendingPlanSnapshot>();
        public Random.SeededRandom Random { get; }

        public IEventBus EventBus { get; }
        public Managers.ITriggerManager TriggerManager { get; }
        public Managers.ICardManager CardManager { get; }
        public Managers.IBuffManager BuffManager { get; }
        public IValueModifierManager ValueModifierManager { get; }
        public DrawRuleResolver DrawRules { get; }
        public PlayRuleResolver PlayRules { get; }

        public System.Func<string, BattleCardDefinition?>? CardDefinitionProvider { get; }

        public List<string> RoundLog { get; } = new List<string>();

        public BattleCardDefinition? GetCardDefinition(string configId)
        {
            return CardDefinitionProvider?.Invoke(configId);
        }

        public BattleCardDefinition? GetEffectiveCardDefinition(BattleCard card)
        {
            return GetCardDefinition(card.GetEffectiveConfigId());
        }

        public List<EffectUnit>? BuildCardEffects(string configId)
        {
            var definition = GetCardDefinition(configId);
            if (definition == null)
                return null;

            return EffectUnitCloner.CloneMany(definition.Effects);
        }

        public List<EffectUnit>? BuildCardEffects(BattleCard card)
        {
            return BuildCardEffects(card.GetEffectiveConfigId());
        }

        public enum BattlePhase
        {
            NotStarted = 0,
            RoundStart = 1,
            PlayerAction = 2,
            Settlement = 3,
            RoundEnd = 4,
            BattleEnd = 5,
        }

        public BattleContext(
            string battleId,
            int randomSeed,
            IEventBus eventBus,
            Managers.ITriggerManager triggerManager,
            Managers.ICardManager cardManager,
            Managers.IBuffManager buffManager,
            IValueModifierManager valueModifierManager,
            DrawRuleResolver drawRules,
            PlayRuleResolver playRules,
            System.Func<string, BattleCardDefinition?>? cardDefinitionProvider = null)
        {
            BattleId = battleId;
            Random = new Random.SeededRandom(randomSeed);
            EventBus = eventBus;
            TriggerManager = triggerManager;
            CardManager = cardManager;
            BuffManager = buffManager;
            ValueModifierManager = valueModifierManager;
            DrawRules = drawRules;
            PlayRules = playRules;
            CardDefinitionProvider = cardDefinitionProvider;
        }
    }
}
