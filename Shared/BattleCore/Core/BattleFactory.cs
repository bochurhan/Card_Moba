#pragma warning disable CS8632

using System;
using System.Collections.Generic;
using CardMoba.BattleCore.Context;
using CardMoba.BattleCore.Definitions;
using CardMoba.BattleCore.EventBus;
using CardMoba.BattleCore.Foundation;
using CardMoba.BattleCore.Managers;
using CardMoba.BattleCore.Modifiers;
using CardMoba.BattleCore.Rules.Draw;
using CardMoba.BattleCore.Rules.Play;

namespace CardMoba.BattleCore.Core
{
    public class PlayerSetupData
    {
        public string PlayerId { get; set; } = string.Empty;
        public string TeamId { get; set; } = string.Empty;
        public string HeroConfigId { get; set; } = string.Empty;
        public int InitialHp { get; set; }
        public bool UseInitialHp { get; set; }
        public int MaxHp { get; set; } = 30;
        public int InitialArmor { get; set; }
        public List<(string configId, int count)> DeckConfig { get; set; } = new List<(string, int)>();
    }

    public class ObjectiveSetupData
    {
        public string EntityId { get; set; } = string.Empty;
        public string TeamId { get; set; } = string.Empty;
        public int InitialHp { get; set; }
        public int MaxHp { get; set; } = 1;
        public int InitialShield { get; set; }
        public int InitialArmor { get; set; }
        public bool IsTargetable { get; set; } = true;
        public bool EndsMatchWhenDestroyed { get; set; } = true;
        public List<string> RequiredDeadEntityIdsToTarget { get; } = new List<string>();
    }

    public class BattleCreateResult
    {
        public BattleContext Context { get; set; } = null!;
        public RoundManager RoundManager { get; set; } = null!;
        public List<string> SetupLog { get; set; } = new List<string>();
    }

    public class BattleFactory
    {
        public Func<string, BuffConfig?>? BuffConfigProvider { get; set; }
        public Func<string, BattleCardDefinition?>? CardDefinitionProvider { get; set; }

        public BattleCreateResult CreateBattle(
            string battleId,
            int randomSeed,
            List<PlayerSetupData> players,
            IEventBus? eventBus = null,
            BattleRuleset? ruleset = null,
            List<ObjectiveSetupData>? objectives = null)
        {
            var setupLog = new List<string>();

            var triggerManager = new TriggerManager();
            var buffManager = new BuffManager(BuffConfigProvider);
            var cardManager = new CardManager();
            var valueModifierManager = new ValueModifierManager();
            var drawRuleResolver = new DrawRuleResolver();
            var playRuleResolver = new PlayRuleResolver();
            var bus = eventBus ?? new NoOpEventBus();

            setupLog.Add($"[BattleFactory] create battle runtime: battleId={battleId}, seed={randomSeed}.");

            var ctx = new BattleContext(
                battleId,
                randomSeed,
                bus,
                triggerManager,
                cardManager,
                buffManager,
                valueModifierManager,
                drawRuleResolver,
                playRuleResolver,
                CardDefinitionProvider,
                ruleset);

            foreach (var setup in players)
            {
                if (string.IsNullOrWhiteSpace(setup.PlayerId))
                    throw new ArgumentException("[BattleFactory] PlayerSetupData.PlayerId cannot be empty.");

                string teamId = string.IsNullOrWhiteSpace(setup.TeamId) ? setup.PlayerId : setup.TeamId;
                int hp = setup.UseInitialHp
                    ? Math.Clamp(setup.InitialHp, 0, setup.MaxHp)
                    : (setup.InitialHp > 0 ? setup.InitialHp : setup.MaxHp);

                var hero = new Entity
                {
                    EntityId = $"hero_{setup.PlayerId}",
                    OwnerPlayerId = setup.PlayerId,
                    TeamId = teamId,
                    Hp = hp,
                    MaxHp = setup.MaxHp,
                    Armor = setup.InitialArmor,
                    Shield = 0,
                };

                var playerData = new PlayerData
                {
                    PlayerId = setup.PlayerId,
                    TeamId = teamId,
                    HeroEntity = hero,
                };

                ctx.RegisterPlayer(playerData);
                setupLog.Add($"[BattleFactory] registered player {setup.PlayerId} team={teamId} hp={hp}/{setup.MaxHp} armor={setup.InitialArmor}.");

                if (setup.DeckConfig.Count > 0)
                {
                    cardManager.InitBattleDeck(ctx, setup.PlayerId, setup.DeckConfig);
                    setupLog.Add($"[BattleFactory] initialized deck for {setup.PlayerId} with {setup.DeckConfig.Count} entries.");
                }
                else
                {
                    setupLog.Add($"[BattleFactory] player {setup.PlayerId} has no configured deck.");
                }
            }

            if (objectives != null)
            {
                foreach (var setup in objectives)
                {
                    if (string.IsNullOrWhiteSpace(setup.EntityId))
                        throw new ArgumentException("[BattleFactory] ObjectiveSetupData.EntityId cannot be empty.");
                    if (string.IsNullOrWhiteSpace(setup.TeamId))
                        throw new ArgumentException("[BattleFactory] ObjectiveSetupData.TeamId cannot be empty.");

                    int hp = setup.InitialHp > 0 ? setup.InitialHp : setup.MaxHp;
                    var objective = new Entity
                    {
                        EntityId = setup.EntityId,
                        Type = EntityType.Structure,
                        TeamId = setup.TeamId,
                        Hp = hp,
                        MaxHp = setup.MaxHp,
                        Shield = setup.InitialShield,
                        Armor = setup.InitialArmor,
                        IsTargetable = setup.IsTargetable,
                        EndsMatchWhenDestroyed = setup.EndsMatchWhenDestroyed,
                    };

                    foreach (var requiredDeadEntityId in setup.RequiredDeadEntityIdsToTarget)
                        objective.RequiredDeadEntityIdsToTarget.Add(requiredDeadEntityId);

                    ctx.RegisterEntity(objective);
                    ctx.RegisterTeam(new BattleTeamState
                    {
                        TeamId = setup.TeamId,
                        ObjectiveEntityId = objective.EntityId,
                    });

                    setupLog.Add($"[BattleFactory] registered objective {setup.EntityId} for team={setup.TeamId} hp={hp}/{setup.MaxHp}.");
                }
            }

            var roundManager = new RoundManager();
            roundManager.InitBattle(ctx);

            foreach (var log in setupLog)
                ctx.RoundLog.Add(log);

            setupLog.Add("[BattleFactory] battle initialization complete. Call BeginRound() to start the first round.");

            return new BattleCreateResult
            {
                Context = ctx,
                RoundManager = roundManager,
                SetupLog = setupLog,
            };
        }
    }

    public class NoOpEventBus : IEventBus
    {
        public void Publish<T>(T battleEvent) where T : BattleEventBase { }
        public void Subscribe<T>(Action<T> handler) where T : BattleEventBase { }
        public void Unsubscribe<T>(Action<T> handler) where T : BattleEventBase { }
    }
}