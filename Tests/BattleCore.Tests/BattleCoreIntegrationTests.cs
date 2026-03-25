#pragma warning disable CS8632
#pragma warning disable CS8625

using System;
using System.Collections.Generic;
using FluentAssertions;
using Xunit;
using CardMoba.BattleCore.Buff;
using CardMoba.BattleCore.Context;
using CardMoba.BattleCore.Core;
using CardMoba.BattleCore.EventBus;
using CardMoba.BattleCore.Foundation;
using CardMoba.Protocol.Enums;

namespace CardMoba.Tests
{
    public class BattleCoreIntegrationTests
    {
        private static BattleCreateResult CreateTestBattle(
            int seed = 42,
            Func<string, BuffConfig?>? buffConfigProvider = null,
            Func<string, BattleCardDefinition?>? cardDefinitionProvider = null,
            Dictionary<string, BattleCardDefinition>? mutableCardDefinitions = null,
            IEventBus? eventBus = null)
        {
            Func<string, BattleCardDefinition?>? effectiveCardDefinitionProvider = cardDefinitionProvider;
            if (mutableCardDefinitions != null)
            {
                effectiveCardDefinitionProvider = configId =>
                {
                    if (mutableCardDefinitions.TryGetValue(configId, out var definition))
                        return definition;
                    return cardDefinitionProvider?.Invoke(configId);
                };
            }

            var factory = new BattleFactory
            {
                BuffConfigProvider = buffConfigProvider,
                CardDefinitionProvider = effectiveCardDefinitionProvider,
            };

            var players = new List<PlayerSetupData>
            {
                new PlayerSetupData
                {
                    PlayerId = "P1",
                    MaxHp = 30,
                    InitialHp = 30,
                    InitialArmor = 0,
                    DeckConfig = new List<(string, int)>(),
                },
                new PlayerSetupData
                {
                    PlayerId = "P2",
                    MaxHp = 30,
                    InitialHp = 30,
                    InitialArmor = 0,
                    DeckConfig = new List<(string, int)>(),
                },
            };

            return factory.CreateBattle("test-battle", seed, players, eventBus);
        }

        private static EffectUnit MakeDamageEffect(int value, string targetType = "Enemy")
            => new EffectUnit
            {
                EffectId = $"dmg_{value}",
                Type = EffectType.Damage,
                TargetType = targetType,
                ValueExpression = value.ToString(),
                Layer = SettleLayer.Damage,
            };

        private static EffectUnit MakePierceEffect(int value, string targetType = "Enemy")
            => new EffectUnit
            {
                EffectId = $"pierce_{value}",
                Type = EffectType.Pierce,
                TargetType = targetType,
                ValueExpression = value.ToString(),
                Layer = SettleLayer.Damage,
            };

        private static EffectUnit MakeLifestealEffect(int percent)
            => new EffectUnit
            {
                EffectId = $"lifesteal_{percent}",
                Type = EffectType.Lifesteal,
                TargetType = "Self",
                ValueExpression = percent.ToString(),
                Layer = SettleLayer.Damage,
            };

        private static EffectUnit MakeHealEffect(int value)
            => new EffectUnit
            {
                EffectId = $"heal_{value}",
                Type = EffectType.Heal,
                TargetType = "Self",
                ValueExpression = value.ToString(),
                Layer = SettleLayer.BuffSpecial,
            };

        private static EffectUnit MakeShieldEffect(int value)
            => new EffectUnit
            {
                EffectId = $"shield_{value}",
                Type = EffectType.Shield,
                TargetType = "Self",
                ValueExpression = value.ToString(),
                Layer = SettleLayer.Defense,
            };

        private static BattleCard GiveHandCard(
            BattleContext ctx,
            string playerId,
            string instanceId,
            string configId = "test_card")
        {
            var player = ctx.GetPlayer(playerId)!;
            var card = new BattleCard
            {
                InstanceId = instanceId,
                ConfigId = configId,
                OwnerId = playerId,
                Zone = CardZone.Hand,
            };
            player.AllCards.Add(card);
            return card;
        }

        private static BattleCard GiveHandCard(
            BattleContext ctx,
            IDictionary<string, BattleCardDefinition> definitions,
            string playerId,
            string instanceId,
            string configId,
            params EffectUnit[] effects)
        {
            definitions[configId] = MakeCardDefinition(configId, effects);
            return GiveHandCard(ctx, playerId, instanceId, configId);
        }

        private static BattleCard GiveHandCard(
            BattleContext ctx,
            IDictionary<string, BattleCardDefinition> definitions,
            string playerId,
            string instanceId,
            string configId,
            bool isExhaust,
            bool isStatCard,
            params EffectUnit[] effects)
        {
            definitions[configId] = MakeCardDefinition(configId, isExhaust, isStatCard, effects);
            return GiveHandCard(ctx, playerId, instanceId, configId);
        }

        private static BuffConfig MakeBuffConfig(string buffId, BuffType buffType, int defaultValue)
            => new BuffConfig
            {
                BuffId = buffId,
                BuffName = buffId,
                BuffType = buffType,
                DefaultValue = defaultValue,
                DefaultDuration = 0,
                StackRule = BuffStackRule.RefreshDuration,
                MaxStacks = 99,
            };

        private static Func<string, BuffConfig?> MakeBuffProvider(params BuffConfig[] configs)
        {
            var map = new Dictionary<string, BuffConfig>(StringComparer.OrdinalIgnoreCase);
            foreach (var config in configs)
                map[config.BuffId] = config;

            return buffId => map.TryGetValue(buffId, out var config) ? config : null;
        }

        private static BattleCardDefinition MakeCardDefinition(
            string configId,
            bool isExhaust = false,
            bool isStatCard = false,
            params EffectUnit[] effects)
            => new BattleCardDefinition
            {
                ConfigId = configId,
                IsExhaust = isExhaust,
                IsStatCard = isStatCard,
                Effects = new List<EffectUnit>(effects),
            };

        private static BattleCardDefinition MakeCardDefinition(string configId, params EffectUnit[] effects)
            => MakeCardDefinition(configId, false, false, effects);

        private static Func<string, BattleCardDefinition?> MakeCardDefinitionProvider(params BattleCardDefinition[] definitions)
        {
            var map = new Dictionary<string, BattleCardDefinition>(StringComparer.OrdinalIgnoreCase);
            foreach (var definition in definitions)
                map[definition.ConfigId] = definition;

            return configId => map.TryGetValue(configId, out var definition) ? definition : null;
        }

        private static Dictionary<string, BattleCardDefinition> CreateMutableCardDefinitions(params BattleCardDefinition[] definitions)
        {
            var map = new Dictionary<string, BattleCardDefinition>(StringComparer.OrdinalIgnoreCase);
            foreach (var definition in definitions)
                map[definition.ConfigId] = definition;
            return map;
        }

        private static List<EffectResult> PlayStrictInstantCard(
            BattleContext ctx,
            RoundManager rm,
            IDictionary<string, BattleCardDefinition> definitions,
            string playerId,
            string instanceId,
            string configId,
            params EffectUnit[] effects)
        {
            var card = GiveHandCard(ctx, definitions, playerId, instanceId, configId, effects);
            return rm.PlayInstantCard(ctx, playerId, card.InstanceId);
        }

        private static bool CommitStrictPlanCard(
            BattleContext ctx,
            RoundManager rm,
            IDictionary<string, BattleCardDefinition> definitions,
            string playerId,
            string instanceId,
            string configId,
            params EffectUnit[] effects)
        {
            var card = GiveHandCard(ctx, definitions, playerId, instanceId, configId, effects);
            return rm.CommitPlanCard(ctx, new CommittedPlanCard
            {
                PlayerId = playerId,
                CardInstanceId = card.InstanceId,
            });
        }

        [Fact]
        public void T01_BattleFactory_CreatesContext_WithTwoPlayers()
        {
            var result = CreateTestBattle();
            var ctx = result.Context;

            ctx.Should().NotBeNull();
            ctx.AllPlayers.Should().ContainKey("P1");
            ctx.AllPlayers.Should().ContainKey("P2");
            ctx.AllPlayers["P1"].HeroEntity.Hp.Should().Be(30);
            ctx.AllPlayers["P2"].HeroEntity.Hp.Should().Be(30);
            result.RoundManager.CurrentRound.Should().Be(0);
            result.RoundManager.IsBattleOver.Should().BeFalse();
        }

        [Fact]
        public void T02_InstantDamageCard_ReducesEnemyHp()
        {
            var definitions = CreateMutableCardDefinitions();
            var result = CreateTestBattle(mutableCardDefinitions: definitions);
            var (ctx, rm) = (result.Context, result.RoundManager);
            rm.BeginRound(ctx);

            PlayStrictInstantCard(ctx, rm, definitions, "P1", "card_instant_01", "instant_damage_8", MakeDamageEffect(8));

            ctx.AllPlayers["P2"].HeroEntity.Hp.Should().Be(22);
            ctx.AllPlayers["P1"].HeroEntity.Hp.Should().Be(30);
        }

        [Fact]
        public void T03_InstantHealCard_RestoresHp_CappedAtMaxHp()
        {
            var definitions = CreateMutableCardDefinitions();
            var result = CreateTestBattle(mutableCardDefinitions: definitions);
            var (ctx, rm) = (result.Context, result.RoundManager);
            rm.BeginRound(ctx);

            PlayStrictInstantCard(ctx, rm, definitions, "P2", "card_dmg", "instant_damage_10", MakeDamageEffect(10));
            ctx.AllPlayers["P1"].HeroEntity.Hp.Should().Be(20);

            PlayStrictInstantCard(ctx, rm, definitions, "P1", "card_heal", "instant_heal_15", MakeHealEffect(15));

            ctx.AllPlayers["P1"].HeroEntity.Hp.Should().Be(30);
        }

        [Fact]
        public void T04_Shield_AbsorbsDamage_BeforeHp()
        {
            var definitions = CreateMutableCardDefinitions();
            var result = CreateTestBattle(mutableCardDefinitions: definitions);
            var (ctx, rm) = (result.Context, result.RoundManager);
            rm.BeginRound(ctx);

            PlayStrictInstantCard(ctx, rm, definitions, "P1", "card_shield", "instant_shield_5", MakeShieldEffect(5));
            ctx.AllPlayers["P1"].HeroEntity.Shield.Should().Be(5);

            PlayStrictInstantCard(ctx, rm, definitions, "P2", "card_dmg8", "instant_damage_8", MakeDamageEffect(8));

            ctx.AllPlayers["P1"].HeroEntity.Shield.Should().Be(0);
            ctx.AllPlayers["P1"].HeroEntity.Hp.Should().Be(27);
        }

        [Fact]
        public void T05_PlanCards_BothPlayersDealtDamage_InSameRound()
        {
            var definitions = CreateMutableCardDefinitions();
            var result = CreateTestBattle(mutableCardDefinitions: definitions);
            var (ctx, rm) = (result.Context, result.RoundManager);
            rm.BeginRound(ctx);

            CommitStrictPlanCard(ctx, rm, definitions, "P1", "plan_P1", "plan_damage_10_a", MakeDamageEffect(10)).Should().BeTrue();
            CommitStrictPlanCard(ctx, rm, definitions, "P2", "plan_P2", "plan_damage_10_b", MakeDamageEffect(10)).Should().BeTrue();

            rm.EndRound(ctx);

            ctx.AllPlayers["P1"].HeroEntity.Hp.Should().Be(20);
            ctx.AllPlayers["P2"].HeroEntity.Hp.Should().Be(20);
            rm.IsBattleOver.Should().BeFalse();
        }

        [Fact]
        public void T06_PlayerDies_BattleEnds_WithCorrectWinner()
        {
            var definitions = CreateMutableCardDefinitions();
            var result = CreateTestBattle(mutableCardDefinitions: definitions);
            var (ctx, rm) = (result.Context, result.RoundManager);
            rm.BeginRound(ctx);

            PlayStrictInstantCard(ctx, rm, definitions, "P1", "card_overkill", "instant_damage_30", MakeDamageEffect(30));

            ctx.AllPlayers["P2"].HeroEntity.Hp.Should().BeLessOrEqualTo(0);
            rm.IsBattleOver.Should().BeTrue();
            rm.WinnerId.Should().Be("P1");
        }

        [Fact]
        public void T07_SameSeed_ProducesSameBattleLog()
        {
            var definitions1 = CreateMutableCardDefinitions(
                MakeCardDefinition("instant_damage_5", MakeDamageEffect(5)),
                MakeCardDefinition("plan_damage_7", MakeDamageEffect(7)));
            var definitions2 = CreateMutableCardDefinitions(
                MakeCardDefinition("instant_damage_5", MakeDamageEffect(5)),
                MakeCardDefinition("plan_damage_7", MakeDamageEffect(7)));

            var r1 = CreateTestBattle(seed: 1234, mutableCardDefinitions: definitions1);
            var r2 = CreateTestBattle(seed: 1234, mutableCardDefinitions: definitions2);

            var (ctx1, rm1) = (r1.Context, r1.RoundManager);
            var (ctx2, rm2) = (r2.Context, r2.RoundManager);

            Action<BattleContext, RoundManager, IDictionary<string, BattleCardDefinition>> runOneBattle = (ctx, rm, definitions) =>
            {
                rm.BeginRound(ctx);
                PlayStrictInstantCard(ctx, rm, definitions, "P1", "c1", "instant_damage_5", MakeDamageEffect(5));
                CommitStrictPlanCard(ctx, rm, definitions, "P2", "c2", "plan_damage_7", MakeDamageEffect(7));
                rm.EndRound(ctx);
            };

            runOneBattle(ctx1, rm1, definitions1);
            runOneBattle(ctx2, rm2, definitions2);

            ctx1.AllPlayers["P1"].HeroEntity.Hp.Should().Be(ctx2.AllPlayers["P1"].HeroEntity.Hp);
            ctx1.AllPlayers["P2"].HeroEntity.Hp.Should().Be(ctx2.AllPlayers["P2"].HeroEntity.Hp);
            rm1.IsBattleOver.Should().Be(rm2.IsBattleOver);
        }

        [Fact]
        public void T08_MultiRound_BattleEndsWhenHpReachesZero()
        {
            var definitions = CreateMutableCardDefinitions();
            var result = CreateTestBattle(mutableCardDefinitions: definitions);
            var (ctx, rm) = (result.Context, result.RoundManager);

            for (int round = 1; round <= 3; round++)
            {
                if (rm.IsBattleOver)
                    break;

                rm.BeginRound(ctx);
                CommitStrictPlanCard(ctx, rm, definitions, "P1", $"plan_r{round}", "plan_damage_11", MakeDamageEffect(11)).Should().BeTrue();
                rm.EndRound(ctx);
            }

            rm.IsBattleOver.Should().BeTrue();
            rm.WinnerId.Should().Be("P1");
            ctx.AllPlayers["P2"].HeroEntity.IsAlive.Should().BeFalse();
        }

        [Fact]
        public void T09_PlanDamageSnapshot_ConsumesShieldAcrossMultipleHits()
        {
            var definitions = CreateMutableCardDefinitions();
            var result = CreateTestBattle(mutableCardDefinitions: definitions);
            var (ctx, rm) = (result.Context, result.RoundManager);
            rm.BeginRound(ctx);

            ctx.AllPlayers["P2"].HeroEntity.Shield = 5;

            CommitStrictPlanCard(
                ctx,
                rm,
                definitions,
                "P1",
                "plan_double_hit",
                "plan_double_hit_cfg",
                MakeDamageEffect(3),
                MakeDamageEffect(4)).Should().BeTrue();

            rm.EndRound(ctx);

            ctx.AllPlayers["P2"].HeroEntity.Hp.Should().Be(28);
        }

        [Fact]
        public void T10_AddBuff_OnHeroEntity_BindsOwnerPlayerIdToPlayer()
        {
            var provider = MakeBuffProvider(MakeBuffConfig("strength", BuffType.Strength, 0));
            var result = CreateTestBattle(buffConfigProvider: provider);
            var ctx = result.Context;

            var targetEntityId = ctx.AllPlayers["P1"].HeroEntity.EntityId;
            var sourceEntityId = ctx.AllPlayers["P2"].HeroEntity.EntityId;

            var buff = ctx.BuffManager.AddBuff(ctx, targetEntityId, "strength", sourceEntityId, value: 3, duration: 1);

            buff.Should().NotBeNull();
            buff.OwnerPlayerId.Should().Be("P1");
        }

        [Fact]
        public void T11_Strength_ModifiesOutgoingDamage_Only()
        {
            var provider = MakeBuffProvider(MakeBuffConfig("strength", BuffType.Strength, 0));
            var definitions = CreateMutableCardDefinitions();
            var result = CreateTestBattle(buffConfigProvider: provider, mutableCardDefinitions: definitions);
            var (ctx, rm) = (result.Context, result.RoundManager);
            rm.BeginRound(ctx);

            var heroP1 = ctx.AllPlayers["P1"].HeroEntity.EntityId;
            ctx.BuffManager.AddBuff(ctx, heroP1, "strength", heroP1, value: 2, duration: 1);

            PlayStrictInstantCard(ctx, rm, definitions, "P1", "card_strength_outgoing", "damage_5", MakeDamageEffect(5));
            ctx.AllPlayers["P2"].HeroEntity.Hp.Should().Be(23);

            PlayStrictInstantCard(ctx, rm, definitions, "P2", "card_strength_incoming", "damage_5_p2", MakeDamageEffect(5));
            ctx.AllPlayers["P1"].HeroEntity.Hp.Should().Be(25);
        }

        [Fact]
        public void T12_Armor_ReducesDamage_ButNotPierce()
        {
            var provider = MakeBuffProvider(MakeBuffConfig("armor", BuffType.Armor, 0));
            var definitions = CreateMutableCardDefinitions();
            var result = CreateTestBattle(buffConfigProvider: provider, mutableCardDefinitions: definitions);
            var (ctx, rm) = (result.Context, result.RoundManager);
            rm.BeginRound(ctx);

            var heroP1 = ctx.AllPlayers["P1"].HeroEntity.EntityId;
            var heroP2 = ctx.AllPlayers["P2"].HeroEntity.EntityId;
            ctx.BuffManager.AddBuff(ctx, heroP2, "armor", heroP1, value: 3, duration: 1);

            PlayStrictInstantCard(ctx, rm, definitions, "P1", "card_damage_armor", "damage_5", MakeDamageEffect(5));
            ctx.AllPlayers["P2"].HeroEntity.Hp.Should().Be(28);

            PlayStrictInstantCard(ctx, rm, definitions, "P1", "card_pierce_armor", "pierce_5", MakePierceEffect(5));
            ctx.AllPlayers["P2"].HeroEntity.Hp.Should().Be(23);
        }

        [Fact]
        public void T13_Vulnerable_AmplifiesDamage_AndPierce()
        {
            var provider = MakeBuffProvider(MakeBuffConfig("vulnerable", BuffType.Vulnerable, 0));
            var definitions = CreateMutableCardDefinitions();
            var result = CreateTestBattle(buffConfigProvider: provider, mutableCardDefinitions: definitions);
            var (ctx, rm) = (result.Context, result.RoundManager);
            rm.BeginRound(ctx);

            var heroP1 = ctx.AllPlayers["P1"].HeroEntity.EntityId;
            var heroP2 = ctx.AllPlayers["P2"].HeroEntity.EntityId;
            ctx.BuffManager.AddBuff(ctx, heroP2, "vulnerable", heroP1, value: 50, duration: 1);

            PlayStrictInstantCard(ctx, rm, definitions, "P1", "card_damage_vulnerable", "damage_4", MakeDamageEffect(4));
            ctx.AllPlayers["P2"].HeroEntity.Hp.Should().Be(24);

            PlayStrictInstantCard(ctx, rm, definitions, "P1", "card_pierce_vulnerable", "pierce_4", MakePierceEffect(4));
            ctx.AllPlayers["P2"].HeroEntity.Hp.Should().Be(18);
        }

        [Fact]
        public void T14_QueuedTriggerEffect_CanReadTrigCtxValue()
        {
            var definitions = CreateMutableCardDefinitions();
            var result = CreateTestBattle(mutableCardDefinitions: definitions);
            var (ctx, rm) = (result.Context, result.RoundManager);
            rm.BeginRound(ctx);

            ctx.TriggerManager.Register(new TriggerUnit
            {
                TriggerName = "copy_damage_to_shield",
                Timing = TriggerTiming.AfterDealDamage,
                OwnerPlayerId = "P1",
                SourceId = "test_copy_damage_to_shield",
                Effects = new List<EffectUnit>
                {
                    new EffectUnit
                    {
                        EffectId = "queued_shield_from_damage",
                        Type = EffectType.Shield,
                        TargetType = "Self",
                        ValueExpression = "{{trigCtx.value}}",
                        Layer = SettleLayer.BuffSpecial,
                    },
                },
            });

            PlayStrictInstantCard(ctx, rm, definitions, "P1", "card_trigger_value", "damage_4", MakeDamageEffect(4));

            ctx.AllPlayers["P1"].HeroEntity.Shield.Should().Be(4);
        }

        [Fact]
        public void T15_QueuedTriggerEffect_Condition_CanReadTrigCtxValue()
        {
            var definitions = CreateMutableCardDefinitions();
            var result = CreateTestBattle(mutableCardDefinitions: definitions);
            var (ctx, rm) = (result.Context, result.RoundManager);
            rm.BeginRound(ctx);

            ctx.AllPlayers["P1"].HeroEntity.Hp = 20;

            ctx.TriggerManager.Register(new TriggerUnit
            {
                TriggerName = "heal_when_damage_positive",
                Timing = TriggerTiming.AfterDealDamage,
                OwnerPlayerId = "P1",
                SourceId = "test_heal_when_damage_positive",
                Effects = new List<EffectUnit>
                {
                    new EffectUnit
                    {
                        EffectId = "queued_heal_when_damage_positive",
                        Type = EffectType.Heal,
                        TargetType = "Self",
                        ValueExpression = "3",
                        Layer = SettleLayer.BuffSpecial,
                        Conditions = new List<string> { "trigCtx.value > 0" },
                    },
                },
            });

            PlayStrictInstantCard(ctx, rm, definitions, "P1", "card_trigger_condition", "damage_5", MakeDamageEffect(5));

            ctx.AllPlayers["P1"].HeroEntity.Hp.Should().Be(23);
        }

        [Fact]
        public void T16_QueuedTriggerEffect_CanReadTrigCtxExtraNumeric()
        {
            var result = CreateTestBattle();
            var ctx = result.Context;
            var settlement = new SettlementEngine();

            ctx.TriggerManager.Register(new TriggerUnit
            {
                TriggerName = "shield_from_trigger_extra",
                Timing = TriggerTiming.OnRoundStart,
                OwnerPlayerId = "P1",
                SourceId = "test_trigger_extra",
                Effects = new List<EffectUnit>
                {
                    new EffectUnit
                    {
                        EffectId = "queued_shield_from_extra",
                        Type = EffectType.Shield,
                        TargetType = "Self",
                        ValueExpression = "{{trigCtx.extra.bonus}}",
                        Layer = SettleLayer.BuffSpecial,
                    },
                },
            });

            ctx.TriggerManager.Fire(ctx, TriggerTiming.OnRoundStart, new TriggerContext
            {
                Round = 1,
                Extra = new Dictionary<string, object> { ["bonus"] = 6 },
            });
            settlement.DrainPendingQueue(ctx);

            ctx.AllPlayers["P1"].HeroEntity.Shield.Should().Be(6);
        }

        [Fact]
        public void T17_OnDeath_CanDriveQueuedFollowUpEffect()
        {
            var definitions = CreateMutableCardDefinitions();
            var result = CreateTestBattle(mutableCardDefinitions: definitions);
            var (ctx, rm) = (result.Context, result.RoundManager);
            rm.BeginRound(ctx);

            ctx.TriggerManager.Register(new TriggerUnit
            {
                TriggerName = "gain_shield_on_death",
                Timing = TriggerTiming.OnDeath,
                OwnerPlayerId = "P1",
                SourceId = "test_gain_shield_on_death",
                Effects = new List<EffectUnit>
                {
                    new EffectUnit
                    {
                        EffectId = "shield_on_death",
                        Type = EffectType.Shield,
                        TargetType = "Self",
                        ValueExpression = "3",
                        Layer = SettleLayer.BuffSpecial,
                    },
                },
            });

            PlayStrictInstantCard(ctx, rm, definitions, "P1", "card_death_followup", "damage_30", MakeDamageEffect(30));

            ctx.AllPlayers["P1"].HeroEntity.Shield.Should().Be(3);
            ctx.AllPlayers["P2"].HeroEntity.IsAlive.Should().BeFalse();
            rm.IsBattleOver.Should().BeTrue();
        }

        [Fact]
        public void T18_ShieldBroken_DamageDealtEvent_IsPublishedOnce()
        {
            var bus = new BattleEventBus();
            var damageEvents = new List<DamageDealtEvent>();
            bus.Subscribe<DamageDealtEvent>(evt => damageEvents.Add(evt));

            var definitions = CreateMutableCardDefinitions();
            var result = CreateTestBattle(mutableCardDefinitions: definitions, eventBus: bus);
            var (ctx, rm) = (result.Context, result.RoundManager);
            rm.BeginRound(ctx);

            ctx.AllPlayers["P2"].HeroEntity.Shield = 5;

            PlayStrictInstantCard(ctx, rm, definitions, "P1", "card_break_shield", "damage_8", MakeDamageEffect(8));

            damageEvents.Should().HaveCount(1);
            damageEvents[0].ShieldBroken.Should().BeTrue();
            damageEvents[0].ShieldAbsorbed.Should().Be(5);
            damageEvents[0].RealHpDamage.Should().Be(3);
            damageEvents[0].SourceEntityId.Should().Be(ctx.AllPlayers["P1"].HeroEntity.EntityId);
            damageEvents[0].TargetEntityId.Should().Be(ctx.AllPlayers["P2"].HeroEntity.EntityId);
        }

        [Fact]
        public void T19_Lifesteal_FiresOnHealed_AndExecutesQueuedFollowUpEffect()
        {
            var definitions = CreateMutableCardDefinitions();
            var result = CreateTestBattle(mutableCardDefinitions: definitions);
            var (ctx, rm) = (result.Context, result.RoundManager);
            rm.BeginRound(ctx);

            ctx.AllPlayers["P1"].HeroEntity.Hp = 20;

            ctx.TriggerManager.Register(new TriggerUnit
            {
                TriggerName = "gain_shield_on_healed",
                Timing = TriggerTiming.OnHealed,
                OwnerPlayerId = "P1",
                SourceId = "test_gain_shield_on_healed",
                Effects = new List<EffectUnit>
                {
                    new EffectUnit
                    {
                        EffectId = "shield_from_lifesteal_heal",
                        Type = EffectType.Shield,
                        TargetType = "Self",
                        ValueExpression = "2",
                        Layer = SettleLayer.BuffSpecial,
                    },
                },
            });

            PlayStrictInstantCard(
                ctx,
                rm,
                definitions,
                "P1",
                "card_damage_lifesteal",
                "damage_lifesteal",
                MakeDamageEffect(10),
                MakeLifestealEffect(50));

            ctx.AllPlayers["P1"].HeroEntity.Hp.Should().Be(25);
            ctx.AllPlayers["P1"].HeroEntity.Shield.Should().Be(2);
            ctx.AllPlayers["P2"].HeroEntity.Hp.Should().Be(20);
        }

        [Fact]
        public void T20_StrictInstantPath_UsesCardDefinitionProvider_AndPublishesCardConfigId()
        {
            var bus = new BattleEventBus();
            var playedEvents = new List<CardPlayedEvent>();
            bus.Subscribe<CardPlayedEvent>(evt => playedEvents.Add(evt));

            var provider = MakeCardDefinitionProvider(
                MakeCardDefinition("strike", MakeDamageEffect(6)));

            var result = CreateTestBattle(cardDefinitionProvider: provider, eventBus: bus);
            var (ctx, rm) = (result.Context, result.RoundManager);
            rm.BeginRound(ctx);

            var card = GiveHandCard(ctx, "P1", "strict_instant_strike", "strike");
            rm.PlayInstantCard(ctx, "P1", card.InstanceId);

            ctx.AllPlayers["P2"].HeroEntity.Hp.Should().Be(24);
            card.Zone.Should().Be(CardZone.Discard);
            playedEvents.Should().ContainSingle();
            playedEvents[0].CardInstanceId.Should().Be(card.InstanceId);
            playedEvents[0].CardConfigId.Should().Be("strike");
        }

        [Fact]
        public void T21_StrictInstantPath_RejectsWrongOwner_AndNonHandCard()
        {
            var provider = MakeCardDefinitionProvider(
                MakeCardDefinition("strike", MakeDamageEffect(6)));

            var result = CreateTestBattle(cardDefinitionProvider: provider);
            var (ctx, rm) = (result.Context, result.RoundManager);
            rm.BeginRound(ctx);

            var card = GiveHandCard(ctx, "P1", "strict_invalid_strike", "strike");
            card.Zone = CardZone.Discard;

            rm.PlayInstantCard(ctx, "P1", card.InstanceId);
            ctx.AllPlayers["P2"].HeroEntity.Hp.Should().Be(30);

            card.Zone = CardZone.Hand;
            rm.PlayInstantCard(ctx, "P2", card.InstanceId);
            ctx.AllPlayers["P2"].HeroEntity.Hp.Should().Be(30);
            card.Zone.Should().Be(CardZone.Hand);
        }

        [Fact]
        public void T22_PlanCard_UsesProviderEffects_InsteadOfCallerPayload()
        {
            var provider = MakeCardDefinitionProvider(
                MakeCardDefinition("plan_strike", MakeDamageEffect(7)));

            var result = CreateTestBattle(cardDefinitionProvider: provider);
            var (ctx, rm) = (result.Context, result.RoundManager);
            rm.BeginRound(ctx);

            var card = GiveHandCard(ctx, "P1", "plan_provider_strike", "plan_strike");
            rm.CommitPlanCard(ctx, new CommittedPlanCard
            {
                PlayerId = "P1",
                CardInstanceId = card.InstanceId,
            }).Should().BeTrue();

            rm.EndRound(ctx);

            ctx.AllPlayers["P1"].HeroEntity.Hp.Should().Be(30);
            ctx.AllPlayers["P2"].HeroEntity.Hp.Should().Be(23);
        }

        [Fact]
        public void T23_BuffThorns_UsesDamageSemantics_AndConsumesAttackerShield()
        {
            var buffProvider = MakeBuffProvider(MakeBuffConfig("thorns", BuffType.Thorns, 0));
            var cardProvider = MakeCardDefinitionProvider(
                MakeCardDefinition("strike", MakeDamageEffect(10)));

            var result = CreateTestBattle(
                buffConfigProvider: buffProvider,
                cardDefinitionProvider: cardProvider);
            var (ctx, rm) = (result.Context, result.RoundManager);
            rm.BeginRound(ctx);

            ctx.AllPlayers["P1"].HeroEntity.Shield = 4;
            var heroP2 = ctx.AllPlayers["P2"].HeroEntity.EntityId;
            ctx.BuffManager.AddBuff(ctx, heroP2, "thorns", heroP2, value: 50, duration: 1);

            var card = GiveHandCard(ctx, "P1", "thorns_strike", "strike");
            rm.PlayInstantCard(ctx, "P1", card.InstanceId);

            ctx.AllPlayers["P2"].HeroEntity.Hp.Should().Be(20);
            ctx.AllPlayers["P1"].HeroEntity.Shield.Should().Be(0);
            ctx.AllPlayers["P1"].HeroEntity.Hp.Should().Be(29);
        }

        [Fact]
        public void T24_BuffRegeneration_GoesThroughOnHealed_FollowUp()
        {
            var buffProvider = MakeBuffProvider(MakeBuffConfig("regen", BuffType.Regeneration, 0));
            var result = CreateTestBattle(buffConfigProvider: buffProvider);
            var (ctx, rm) = (result.Context, result.RoundManager);

            ctx.AllPlayers["P1"].HeroEntity.Hp = 20;
            ctx.TriggerManager.Register(new TriggerUnit
            {
                TriggerName = "gain_shield_when_healed_by_regen",
                Timing = TriggerTiming.OnHealed,
                OwnerPlayerId = "P1",
                SourceId = "test_regen_on_healed",
                Effects = new List<EffectUnit>
                {
                    new EffectUnit
                    {
                        EffectId = "shield_from_regen_heal",
                        Type = EffectType.Shield,
                        TargetType = "Self",
                        ValueExpression = "2",
                        Layer = SettleLayer.BuffSpecial,
                    },
                },
            });

            var heroP1 = ctx.AllPlayers["P1"].HeroEntity.EntityId;
            ctx.BuffManager.AddBuff(ctx, heroP1, "regen", heroP1, value: 3, duration: 1);

            rm.BeginRound(ctx);

            ctx.AllPlayers["P1"].HeroEntity.Hp.Should().Be(23);
            ctx.AllPlayers["P1"].HeroEntity.Shield.Should().Be(2);
        }

        [Fact]
        public void T25_OnBuffAdded_OnBuffRemoved_AndOnCardDrawn_Timings_Fire()
        {
            var buffProvider = MakeBuffProvider(MakeBuffConfig("strength", BuffType.Strength, 0));
            var result = CreateTestBattle(buffConfigProvider: buffProvider);
            var ctx = result.Context;
            var settlement = new SettlementEngine();

            ctx.TriggerManager.Register(new TriggerUnit
            {
                TriggerName = "shield_on_buff_added",
                Timing = TriggerTiming.OnBuffAdded,
                OwnerPlayerId = "P1",
                SourceId = "test_shield_on_buff_added",
                Effects = new List<EffectUnit>
                {
                    new EffectUnit
                    {
                        EffectId = "shield_on_buff_added_effect",
                        Type = EffectType.Shield,
                        TargetType = "Self",
                        ValueExpression = "1",
                        Layer = SettleLayer.BuffSpecial,
                    },
                },
            });
            ctx.TriggerManager.Register(new TriggerUnit
            {
                TriggerName = "shield_on_buff_removed",
                Timing = TriggerTiming.OnBuffRemoved,
                OwnerPlayerId = "P1",
                SourceId = "test_shield_on_buff_removed",
                Effects = new List<EffectUnit>
                {
                    new EffectUnit
                    {
                        EffectId = "shield_on_buff_removed_effect",
                        Type = EffectType.Shield,
                        TargetType = "Self",
                        ValueExpression = "2",
                        Layer = SettleLayer.BuffSpecial,
                    },
                },
            });
            ctx.TriggerManager.Register(new TriggerUnit
            {
                TriggerName = "shield_on_card_drawn",
                Timing = TriggerTiming.OnCardDrawn,
                OwnerPlayerId = "P1",
                SourceId = "test_shield_on_card_drawn",
                Effects = new List<EffectUnit>
                {
                    new EffectUnit
                    {
                        EffectId = "shield_on_card_drawn_effect",
                        Type = EffectType.Shield,
                        TargetType = "Self",
                        ValueExpression = "3",
                        Layer = SettleLayer.BuffSpecial,
                    },
                },
            });

            var heroP1 = ctx.AllPlayers["P1"].HeroEntity.EntityId;
            var buff = ctx.BuffManager.AddBuff(ctx, heroP1, "strength", heroP1, value: 2, duration: 1);
            settlement.DrainPendingQueue(ctx);
            ctx.AllPlayers["P1"].HeroEntity.Shield.Should().Be(1);

            ctx.BuffManager.RemoveBuff(ctx, heroP1, buff.RuntimeId).Should().BeTrue();
            settlement.DrainPendingQueue(ctx);
            ctx.AllPlayers["P1"].HeroEntity.Shield.Should().Be(3);

            ctx.CardManager.GenerateCard(ctx, "P1", "draw_test_card", CardZone.Deck, tempCard: false);
            ctx.CardManager.DrawCards(ctx, "P1", 1).Should().HaveCount(1);
            settlement.DrainPendingQueue(ctx);
            ctx.AllPlayers["P1"].HeroEntity.Shield.Should().Be(6);
        }

        [Fact]
        public void T26_TempCards_AreDestroyed_AtRoundEnd()
        {
            var result = CreateTestBattle();
            var (ctx, rm) = (result.Context, result.RoundManager);
            rm.BeginRound(ctx);

            var generated = ctx.CardManager.GenerateCard(ctx, "P1", "temp_generated", CardZone.Hand, tempCard: true);
            ctx.AllPlayers["P1"].AllCards.Should().Contain(generated);

            rm.EndRound(ctx);

            ctx.AllPlayers["P1"].AllCards.Should().NotContain(generated);
        }

        [Fact]
        public void T27_HandStateCard_FiresOnStatCardHeld_DuringEndRound()
        {
            var result = CreateTestBattle();
            var (ctx, rm) = (result.Context, result.RoundManager);
            rm.BeginRound(ctx);

            ctx.TriggerManager.Register(new TriggerUnit
            {
                TriggerName = "state_card_self_ping",
                Timing = TriggerTiming.OnStatCardHeld,
                OwnerPlayerId = "P1",
                SourceId = "test_state_card_self_ping",
                Effects = new List<EffectUnit>
                {
                    MakeDamageEffect(2, "Self"),
                },
            });

            var stateCard = GiveHandCard(ctx, "P1", "state_burn_01", "burn_state");
            stateCard.IsStatCard = true;

            rm.EndRound(ctx);

            ctx.AllPlayers["P1"].HeroEntity.Hp.Should().Be(28);
            stateCard.Zone.Should().Be(CardZone.Discard);
        }

        [Fact]
        public void T28_StateCard_CannotBePlayedOrCommitted()
        {
            var provider = MakeCardDefinitionProvider(
                MakeCardDefinition("burn_state", false, true, MakeDamageEffect(5)));

            var result = CreateTestBattle(cardDefinitionProvider: provider);
            var (ctx, rm) = (result.Context, result.RoundManager);
            rm.BeginRound(ctx);

            var instantStateCard = GiveHandCard(ctx, "P1", "state_instant_01", "burn_state");
            instantStateCard.IsStatCard = true;

            rm.PlayInstantCard(ctx, "P1", instantStateCard.InstanceId).Should().BeEmpty();
            ctx.AllPlayers["P2"].HeroEntity.Hp.Should().Be(30);
            instantStateCard.Zone.Should().Be(CardZone.Hand);

            var planStateCard = GiveHandCard(ctx, "P1", "state_plan_01", "burn_state");
            planStateCard.IsStatCard = true;

            rm.CommitPlanCard(ctx, new CommittedPlanCard
            {
                PlayerId = "P1",
                CardInstanceId = planStateCard.InstanceId,
            }).Should().BeFalse();
            planStateCard.Zone.Should().Be(CardZone.Hand);
        }

        [Fact]
        public void T29_BuffManager_IsTheOnlyRuntimeSourceOfTruth()
        {
            var buffProvider = MakeBuffProvider(MakeBuffConfig("strength", BuffType.Strength, 0));
            var result = CreateTestBattle(buffConfigProvider: buffProvider);
            var ctx = result.Context;

            var heroP1 = ctx.AllPlayers["P1"].HeroEntity;
            ctx.BuffManager.AddBuff(ctx, heroP1.EntityId, "strength", heroP1.EntityId, value: 2, duration: 1);

            ctx.BuffManager.GetBuffs(heroP1.EntityId).Should().ContainSingle();
            typeof(Entity).GetProperty("ActiveBuffs").Should().BeNull();
        }
    }
}
