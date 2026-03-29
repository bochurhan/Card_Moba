#pragma warning disable CS8632

using System;
using System.Collections.Generic;
using CardMoba.BattleCore.Core;
using CardMoba.BattleCore.Definitions;
using CardMoba.BattleCore.EventBus;
using CardMoba.BattleCore.Foundation;
using CardMoba.BattleCore.Modifiers;
using CardMoba.BattleCore.Rules.Draw;
using CardMoba.BattleCore.Rules.Play;

namespace CardMoba.BattleCore.Context
{
    /// <summary>
    /// 单场战斗运行时的唯一真源。
    /// 当前负责维护玩家、队伍以及共享目标等通用实体。
    /// </summary>
    public class BattleContext
    {
        public string BattleId { get; }
        public int CurrentRound { get; set; }
        public BattlePhase CurrentPhase { get; set; }
        public BattleRuleset Ruleset { get; }

        private readonly Dictionary<string, PlayerData> _players = new Dictionary<string, PlayerData>();
        private readonly Dictionary<string, BattleTeamState> _teams = new Dictionary<string, BattleTeamState>();
        private readonly Dictionary<string, Entity> _entities = new Dictionary<string, Entity>();

        public void RegisterPlayer(PlayerData player)
        {
            if (player == null)
                throw new ArgumentNullException(nameof(player));
            if (string.IsNullOrWhiteSpace(player.PlayerId))
                throw new ArgumentException("player.PlayerId cannot be empty.", nameof(player));

            string normalizedTeamId = NormalizeTeamId(player.TeamId, player.PlayerId);
            if (_players.TryGetValue(player.PlayerId, out var existing)
                && !string.IsNullOrWhiteSpace(existing.TeamId)
                && !string.Equals(existing.TeamId, normalizedTeamId, StringComparison.Ordinal)
                && _teams.TryGetValue(existing.TeamId, out var oldTeam))
            {
                oldTeam.PlayerIds.Remove(player.PlayerId);
            }

            player.TeamId = normalizedTeamId;
            _players[player.PlayerId] = player;

            EnsureTeamExists(normalizedTeamId);
            AddPlayerToTeam(normalizedTeamId, player.PlayerId);

            if (player.HeroEntity != null)
            {
                player.HeroEntity.OwnerPlayerId = player.PlayerId;
                player.HeroEntity.TeamId = normalizedTeamId;
                RegisterEntity(player.HeroEntity);
            }
        }

        public void RegisterTeam(BattleTeamState team)
        {
            if (team == null)
                throw new ArgumentNullException(nameof(team));
            if (string.IsNullOrWhiteSpace(team.TeamId))
                throw new ArgumentException("team.TeamId cannot be empty.", nameof(team));

            if (!_teams.TryGetValue(team.TeamId, out var existing))
            {
                _teams[team.TeamId] = team;
                return;
            }

            foreach (var playerId in team.PlayerIds)
            {
                if (!existing.PlayerIds.Contains(playerId))
                    existing.PlayerIds.Add(playerId);
            }

            if (!string.IsNullOrWhiteSpace(team.ObjectiveEntityId))
                existing.ObjectiveEntityId = team.ObjectiveEntityId;
        }

        public BattleTeamState? GetTeam(string teamId)
        {
            _teams.TryGetValue(teamId, out var team);
            return team;
        }

        public IReadOnlyDictionary<string, BattleTeamState> AllTeams => _teams;

        public void RegisterEntity(Entity entity)
        {
            if (entity == null)
                throw new ArgumentNullException(nameof(entity));
            if (string.IsNullOrWhiteSpace(entity.EntityId))
                throw new ArgumentException("entity.EntityId cannot be empty.", nameof(entity));

            if (string.IsNullOrWhiteSpace(entity.TeamId) && !string.IsNullOrWhiteSpace(entity.OwnerPlayerId))
            {
                var owner = GetPlayer(entity.OwnerPlayerId);
                if (owner != null)
                    entity.TeamId = owner.TeamId;
            }

            if (!string.IsNullOrWhiteSpace(entity.TeamId))
                EnsureTeamExists(entity.TeamId);

            _entities[entity.EntityId] = entity;
        }

        public PlayerData? GetPlayer(string playerId)
        {
            _players.TryGetValue(playerId, out var player);
            return player;
        }

        public PlayerData? GetPlayerByEntityId(string entityId)
        {
            if (!_entities.TryGetValue(entityId, out var entity))
                return null;

            return string.IsNullOrWhiteSpace(entity.OwnerPlayerId)
                ? null
                : GetPlayer(entity.OwnerPlayerId);
        }

        public IReadOnlyDictionary<string, PlayerData> AllPlayers => _players;
        public IReadOnlyDictionary<string, Entity> AllEntities => _entities;

        public Entity? GetEntity(string entityId)
        {
            _entities.TryGetValue(entityId, out var entity);
            return entity;
        }

        public List<PlayerData> GetPlayersByTeam(string teamId)
        {
            var result = new List<PlayerData>();
            foreach (var player in _players.Values)
            {
                if (string.Equals(player.TeamId, teamId, StringComparison.Ordinal))
                    result.Add(player);
            }

            return result;
        }

        public List<Entity> GetEntitiesByTeam(string teamId)
        {
            var result = new List<Entity>();
            foreach (var entity in _entities.Values)
            {
                if (string.Equals(entity.TeamId, teamId, StringComparison.Ordinal))
                    result.Add(entity);
            }

            return result;
        }

        public List<Entity> GetObjectivesByTeam(string teamId)
        {
            var result = new List<Entity>();
            foreach (var entity in _entities.Values)
            {
                if (entity.Type == EntityType.Structure
                    && string.Equals(entity.TeamId, teamId, StringComparison.Ordinal))
                {
                    result.Add(entity);
                }
            }

            return result;
        }

        public Entity? GetObjectiveForTeam(string teamId)
        {
            if (_teams.TryGetValue(teamId, out var team)
                && !string.IsNullOrWhiteSpace(team.ObjectiveEntityId))
            {
                return GetEntity(team.ObjectiveEntityId);
            }

            foreach (var entity in _entities.Values)
            {
                if (entity.Type == EntityType.Structure
                    && string.Equals(entity.TeamId, teamId, StringComparison.Ordinal))
                {
                    return entity;
                }
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

        public Func<string, BattleCardDefinition?>? CardDefinitionProvider { get; }

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
            Func<string, BattleCardDefinition?>? cardDefinitionProvider = null,
            BattleRuleset? ruleset = null)
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
            Ruleset = ruleset ?? new BattleRuleset();
        }

        private static string NormalizeTeamId(string? rawTeamId, string playerId)
        {
            return string.IsNullOrWhiteSpace(rawTeamId) ? playerId : rawTeamId;
        }

        private void EnsureTeamExists(string teamId)
        {
            if (!_teams.ContainsKey(teamId))
            {
                _teams[teamId] = new BattleTeamState
                {
                    TeamId = teamId,
                };
            }
        }

        private void AddPlayerToTeam(string teamId, string playerId)
        {
            var team = _teams[teamId];
            if (!team.PlayerIds.Contains(playerId))
                team.PlayerIds.Add(playerId);
        }
    }
}
