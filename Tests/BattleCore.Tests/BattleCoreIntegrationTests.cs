#pragma warning disable CS8632
#pragma warning disable CS8625

using System;
using System.Collections.Generic;
using FluentAssertions;
using Xunit;
using CardMoba.BattleCore.Context;
using CardMoba.BattleCore.Core;
using CardMoba.BattleCore.Costs;
using CardMoba.BattleCore.Definitions;
using CardMoba.BattleCore.EventBus;
using CardMoba.BattleCore.Foundation;
using CardMoba.BattleCore.Handlers;
using CardMoba.BattleCore.Rules.Play;
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
                Layer = SettlementLayer.Damage,
            };

        private static EffectUnit MakePierceEffect(int value, string targetType = "Enemy")
            => new EffectUnit
            {
                EffectId = $"pierce_{value}",
                Type = EffectType.Pierce,
                TargetType = targetType,
                ValueExpression = value.ToString(),
                Layer = SettlementLayer.Damage,
            };

        private static EffectUnit MakeLifestealEffect(int percent)
            => new EffectUnit
            {
                EffectId = $"lifesteal_{percent}",
                Type = EffectType.Lifesteal,
                TargetType = "Self",
                ValueExpression = percent.ToString(),
                Layer = SettlementLayer.Damage,
            };

        private static EffectUnit MakeHealEffect(int value)
            => new EffectUnit
            {
                EffectId = $"heal_{value}",
                Type = EffectType.Heal,
                TargetType = "Self",
                ValueExpression = value.ToString(),
                Layer = SettlementLayer.BuffSpecial,
            };

        private static EffectUnit MakeShieldEffect(int value)
            => new EffectUnit
            {
                EffectId = $"shield_{value}",
                Type = EffectType.Shield,
                TargetType = "Self",
                ValueExpression = value.ToString(),
                Layer = SettlementLayer.Defense,
            };

        private static EffectUnit MakeGainEnergyEffect(int value)
            => new EffectUnit
            {
                EffectId = $"gain_energy_{value}",
                Type = EffectType.GainEnergy,
                TargetType = "Self",
                ValueExpression = value.ToString(),
                Layer = SettlementLayer.Resource,
            };

        private static EffectUnit MakeUpgradeCardsInHandEffect(string projectionLifetime)
            => new EffectUnit
            {
                EffectId = $"upgrade_hand_{projectionLifetime}",
                Type = EffectType.UpgradeCardsInHand,
                TargetType = "Self",
                ValueExpression = "0",
                Layer = SettlementLayer.BuffSpecial,
                Params = new Dictionary<string, string>
                {
                    ["projectionLifetime"] = projectionLifetime,
                },
            };

        private static EffectUnit MakeAddBuffEffect(string buffConfigId, int value, int duration, string targetType = "Self")
            => new EffectUnit
            {
                EffectId = $"add_buff_{buffConfigId}_{value}_{duration}",
                Type = EffectType.AddBuff,
                TargetType = targetType,
                ValueExpression = value.ToString(),
                Layer = SettlementLayer.BuffSpecial,
                Params = new Dictionary<string, string>
                {
                    ["buffConfigId"] = buffConfigId,
                    ["duration"] = duration.ToString(),
                },
            };

        private static EffectUnit MakeGenerateCardEffect(
            string generatedConfigId,
            string targetType = "Self",
            string targetZone = "Deck",
            bool tempCard = false,
            int count = 1,
            string valueExpression = "")
            => new EffectUnit
            {
                EffectId = $"gen_{generatedConfigId}_{targetType}_{targetZone}_{count}",
                Type = EffectType.GenerateCard,
                TargetType = targetType,
                ValueExpression = valueExpression,
                Layer = SettlementLayer.Resource,
                Params = new Dictionary<string, string>
                {
                    ["configId"] = generatedConfigId,
                    ["targetZone"] = targetZone,
                    ["count"] = count.ToString(),
                    ["tempCard"] = tempCard ? "true" : "false",
                },
            };

        private static EffectUnit MakeReturnSourceCardToHandAtRoundEndEffect()
            => new EffectUnit
            {
                EffectId = "return_source_card_end_round",
                Type = EffectType.ReturnSourceCardToHandAtRoundEnd,
                TargetType = "Self",
                ValueExpression = "0",
                Layer = SettlementLayer.Resource,
            };

        private static EffectUnit MakeMoveSelectedCardToDeckTopEffect()
            => new EffectUnit
            {
                EffectId = "move_selected_card_to_deck_top",
                Type = EffectType.MoveSelectedCardToDeckTop,
                TargetType = "Self",
                ValueExpression = "0",
                Layer = SettlementLayer.Resource,
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

        private static BattleCard GiveCardInZone(
            BattleContext ctx,
            string playerId,
            string instanceId,
            CardZone zone,
            string configId = "test_card")
        {
            var player = ctx.GetPlayer(playerId)!;
            var card = new BattleCard
            {
                InstanceId = instanceId,
                ConfigId = configId,
                OwnerId = playerId,
                Zone = zone,
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
            int energyCost = 0,
            params EffectUnit[] effects)
            => new BattleCardDefinition
            {
                ConfigId = configId,
                IsExhaust = isExhaust,
                IsStatCard = isStatCard,
                EnergyCost = energyCost,
                Effects = new List<EffectUnit>(effects),
            };

        private static BattleCardDefinition MakeCardDefinition(string configId, params EffectUnit[] effects)
            => MakeCardDefinition(configId, false, false, 0, effects);

        private static BattleCardDefinition MakeCardDefinition(
            string configId,
            bool isExhaust,
            bool isStatCard,
            params EffectUnit[] effects)
            => MakeCardDefinition(configId, isExhaust, isStatCard, 0, effects);

        private static BattleCardDefinition MakeCardDefinitionWithUpgrade(
            string configId,
            string upgradedConfigId,
            int energyCost = 0,
            params EffectUnit[] effects)
        {
            return new BattleCardDefinition
            {
                ConfigId = configId,
                EnergyCost = energyCost,
                UpgradedConfigId = upgradedConfigId,
                Effects = new List<EffectUnit>(effects),
            };
        }

        private static BattleCardDefinition MakeCardDefinitionWithUpgrade(
            string configId,
            string upgradedConfigId,
            params EffectUnit[] effects)
            => MakeCardDefinitionWithUpgrade(configId, upgradedConfigId, 0, effects);

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
                        Layer = SettlementLayer.BuffSpecial,
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
                        Layer = SettlementLayer.BuffSpecial,
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
                        Layer = SettlementLayer.BuffSpecial,
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
                        Layer = SettlementLayer.BuffSpecial,
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
                        Layer = SettlementLayer.BuffSpecial,
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
                        Layer = SettlementLayer.BuffSpecial,
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
                        Layer = SettlementLayer.BuffSpecial,
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
                        Layer = SettlementLayer.BuffSpecial,
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
                        Layer = SettlementLayer.BuffSpecial,
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

        [Fact]
        public void T30_NoDrawThisTurn_BlocksRoundStartDraws()
        {
            var buffProvider = MakeBuffProvider(MakeBuffConfig("no_draw_this_turn", BuffType.NoDrawThisTurn, 0));
            var result = CreateTestBattle(buffConfigProvider: buffProvider);
            var (ctx, rm) = (result.Context, result.RoundManager);

            for (int i = 0; i < 5; i++)
                ctx.CardManager.GenerateCard(ctx, "P1", $"draw_block_test_{i}", CardZone.Deck, tempCard: false);

            var heroP1 = ctx.AllPlayers["P1"].HeroEntity.EntityId;
            ctx.BuffManager.AddBuff(ctx, heroP1, "no_draw_this_turn", heroP1, value: 0, duration: 1);

            rm.BeginRound(ctx);

            ctx.AllPlayers["P1"].GetCardsInZone(CardZone.Hand).Should().BeEmpty();
            ctx.AllPlayers["P1"].GetCardsInZone(CardZone.Deck).Should().HaveCount(5);
        }

        [Fact]
        public void T31_NoDamageCardThisTurn_BlocksDamageCardPlay()
        {
            var buffProvider = MakeBuffProvider(MakeBuffConfig("no_damage_card_this_turn", BuffType.NoDamageCardThisTurn, 0));
            var definitions = CreateMutableCardDefinitions();
            var result = CreateTestBattle(buffConfigProvider: buffProvider, mutableCardDefinitions: definitions);
            var (ctx, rm) = (result.Context, result.RoundManager);
            rm.BeginRound(ctx);

            var heroP1 = ctx.AllPlayers["P1"].HeroEntity.EntityId;
            ctx.BuffManager.AddBuff(ctx, heroP1, "no_damage_card_this_turn", heroP1, value: 0, duration: 1);

            var instantDamageCard = GiveHandCard(ctx, definitions, "P1", "no_damage_instant", "damage_6", MakeDamageEffect(6));
            rm.PlayInstantCard(ctx, "P1", instantDamageCard.InstanceId).Should().BeEmpty();
            instantDamageCard.Zone.Should().Be(CardZone.Hand);
            ctx.AllPlayers["P2"].HeroEntity.Hp.Should().Be(30);

            var planDamageCard = GiveHandCard(ctx, definitions, "P1", "no_damage_plan", "plan_damage_6", MakeDamageEffect(6));
            rm.CommitPlanCard(ctx, new CommittedPlanCard
            {
                PlayerId = "P1",
                CardInstanceId = planDamageCard.InstanceId,
            }).Should().BeFalse();
            planDamageCard.Zone.Should().Be(CardZone.Hand);

            PlayStrictInstantCard(ctx, rm, definitions, "P1", "no_damage_shield_ok", "shield_5", MakeShieldEffect(5));
            ctx.AllPlayers["P1"].HeroEntity.Shield.Should().Be(5);
        }

        [Fact]
        public void T32_GainEnergy_Effect_AddsCurrentEnergy()
        {
            var definitions = CreateMutableCardDefinitions();
            var result = CreateTestBattle(mutableCardDefinitions: definitions);
            var (ctx, rm) = (result.Context, result.RoundManager);
            rm.BeginRound(ctx);

            ctx.AllPlayers["P1"].Energy = 1;

            PlayStrictInstantCard(ctx, rm, definitions, "P1", "gain_energy_card", "gain_energy_2", MakeGainEnergyEffect(2));

            ctx.AllPlayers["P1"].Energy.Should().Be(3);
        }

        [Fact]
        public void T33_Weak_ReducesOutgoingDamageBy25Percent()
        {
            var buffProvider = MakeBuffProvider(MakeBuffConfig("weak", BuffType.Weak, 0));
            var definitions = CreateMutableCardDefinitions();
            var result = CreateTestBattle(buffConfigProvider: buffProvider, mutableCardDefinitions: definitions);
            var (ctx, rm) = (result.Context, result.RoundManager);
            rm.BeginRound(ctx);

            var heroP1 = ctx.AllPlayers["P1"].HeroEntity.EntityId;
            ctx.BuffManager.AddBuff(ctx, heroP1, "weak", heroP1, value: 25, duration: 1);

            PlayStrictInstantCard(ctx, rm, definitions, "P1", "weak_damage_card", "damage_8", MakeDamageEffect(8));

            ctx.AllPlayers["P2"].HeroEntity.Hp.Should().Be(24);
        }

        [Fact]
        public void T34_FullStrike_BlocksDamageCards_OnNextRoundOnly()
        {
            var buffProvider = MakeBuffProvider(MakeBuffConfig("no_damage_card_this_turn", BuffType.NoDamageCardThisTurn, 0));
            var definitions = CreateMutableCardDefinitions();
            var result = CreateTestBattle(buffConfigProvider: buffProvider, mutableCardDefinitions: definitions);
            var (ctx, rm) = (result.Context, result.RoundManager);

            rm.BeginRound(ctx);
            CommitStrictPlanCard(
                ctx,
                rm,
                definitions,
                "P1",
                "full_strike_card",
                "full_strike",
                MakeDamageEffect(25),
                MakeAddBuffEffect("no_damage_card_this_turn", 1, 2)).Should().BeTrue();

            rm.EndRound(ctx);
            ctx.AllPlayers["P2"].HeroEntity.Hp.Should().Be(5);
            ctx.BuffManager.HasBuffType(ctx, ctx.AllPlayers["P1"].HeroEntity.EntityId, BuffType.NoDamageCardThisTurn).Should().BeTrue();

            rm.BeginRound(ctx);
            var blockedDamageCard = GiveHandCard(ctx, definitions, "P1", "blocked_damage", "blocked_damage_6", MakeDamageEffect(6));
            rm.PlayInstantCard(ctx, "P1", blockedDamageCard.InstanceId).Should().BeEmpty();
            blockedDamageCard.Zone.Should().Be(CardZone.Hand);
            ctx.AllPlayers["P2"].HeroEntity.Hp.Should().Be(5);

            rm.EndRound(ctx);
            ctx.BuffManager.HasBuffType(ctx, ctx.AllPlayers["P1"].HeroEntity.EntityId, BuffType.NoDamageCardThisTurn).Should().BeFalse();

            rm.BeginRound(ctx);
            var allowedDamageCard = GiveHandCard(ctx, definitions, "P1", "allowed_damage", "allowed_damage_6", MakeDamageEffect(6));
            rm.PlayInstantCard(ctx, "P1", allowedDamageCard.InstanceId).Should().NotBeEmpty();
            ctx.AllPlayers["P2"].HeroEntity.Hp.Should().BeLessOrEqualTo(0);
        }

        [Fact]
        public void T35_Rend_GeneratesWoundInOpponentDeck_WhenRealHpDamagePositive()
        {
            var definitions = CreateMutableCardDefinitions(
                MakeCardDefinition("wound", false, true),
                MakeCardDefinition(
                    "rend",
                    MakeDamageEffect(12),
                    MakeGenerateCardEffect("wound", "Opponent", "Deck", tempCard: false, count: 1, valueExpression: "{{preEffect[dmg_12].totalRealHpDamage}}")));
            var result = CreateTestBattle(mutableCardDefinitions: definitions);
            var (ctx, rm) = (result.Context, result.RoundManager);

            rm.BeginRound(ctx);
            GiveHandCard(ctx, "P1", "rend_card", "rend");
            rm.CommitPlanCard(ctx, new CommittedPlanCard
            {
                PlayerId = "P1",
                CardInstanceId = "rend_card",
            }).Should().BeTrue();

            rm.EndRound(ctx);

            ctx.AllPlayers["P2"].HeroEntity.Hp.Should().Be(18);
            var woundCards = ctx.AllPlayers["P2"].AllCards.FindAll(c => c.ConfigId == "wound");
            woundCards.Should().HaveCount(1);
            woundCards[0].Zone.Should().Be(CardZone.Deck);
            woundCards[0].TempCard.Should().BeFalse();
            woundCards[0].IsStatCard.Should().BeTrue();
        }

        [Fact]
        public void T36_Rend_DoesNotGenerateWound_WhenDamageIsFullyBlocked()
        {
            var definitions = CreateMutableCardDefinitions(
                MakeCardDefinition("wound", false, true),
                MakeCardDefinition(
                    "rend",
                    MakeDamageEffect(12),
                    MakeGenerateCardEffect("wound", "Opponent", "Deck", tempCard: false, count: 1, valueExpression: "{{preEffect[dmg_12].totalRealHpDamage}}")));
            var result = CreateTestBattle(mutableCardDefinitions: definitions);
            var (ctx, rm) = (result.Context, result.RoundManager);

            ctx.AllPlayers["P2"].HeroEntity.Shield = 20;

            rm.BeginRound(ctx);
            GiveHandCard(ctx, "P1", "rend_blocked_card", "rend");
            rm.CommitPlanCard(ctx, new CommittedPlanCard
            {
                PlayerId = "P1",
                CardInstanceId = "rend_blocked_card",
            }).Should().BeTrue();

            rm.EndRound(ctx);

            ctx.AllPlayers["P2"].HeroEntity.Hp.Should().Be(30);
            ctx.AllPlayers["P2"].AllCards.Should().NotContain(c => c.ConfigId == "wound");
        }

        [Fact]
        public void T37_Anger_GeneratesPermanentCopyIntoOwnDeck()
        {
            var definitions = CreateMutableCardDefinitions(
                MakeCardDefinition(
                    "anger",
                    MakeDamageEffect(6),
                    MakeGenerateCardEffect("anger", "Self", "Deck", tempCard: false)));
            var result = CreateTestBattle(mutableCardDefinitions: definitions);
            var (ctx, rm) = (result.Context, result.RoundManager);

            rm.BeginRound(ctx);
            GiveHandCard(ctx, "P1", "anger_card", "anger");
            rm.CommitPlanCard(ctx, new CommittedPlanCard
            {
                PlayerId = "P1",
                CardInstanceId = "anger_card",
            }).Should().BeTrue();

            rm.EndRound(ctx);

            var angerCards = ctx.AllPlayers["P1"].AllCards.FindAll(c => c.ConfigId == "anger");
            angerCards.Should().HaveCount(2);
            angerCards.Should().Contain(c => c.Zone == CardZone.Discard);
            angerCards.Should().Contain(c => c.Zone == CardZone.Deck && !c.TempCard);
        }

        [Fact]
        public void T38_GeneratedStatusCard_CannotBePlayed()
        {
            var definitions = CreateMutableCardDefinitions(MakeCardDefinition("wound", false, true));
            var result = CreateTestBattle(mutableCardDefinitions: definitions);
            var (ctx, rm) = (result.Context, result.RoundManager);

            rm.BeginRound(ctx);
            var wound = ctx.CardManager.GenerateCard(ctx, "P1", "wound", CardZone.Hand, tempCard: false);

            rm.PlayInstantCard(ctx, "P1", wound.InstanceId).Should().BeEmpty();
            wound.Zone.Should().Be(CardZone.Hand);
        }

        [Fact]
        public void T39_PainStrike_AppliesVulnerable_OnNextRoundStartOnly()
        {
            var delayedVulnerable = new BuffConfig
            {
                BuffId = "delayed_vulnerable_next_round",
                BuffName = "�»غ�����",
                BuffType = BuffType.DelayedVulnerableNextRound,
                DefaultValue = 50,
                DefaultDuration = 2,
                StackRule = BuffStackRule.StackValue,
                MaxStacks = 99,
            };
            var vulnerable = new BuffConfig
            {
                BuffId = "vulnerable",
                BuffName = "易伤",
                BuffType = BuffType.Vulnerable,
                DefaultValue = 50,
                DefaultDuration = 1,
                StackRule = BuffStackRule.StackValue,
                MaxStacks = 99,
            };

            var definitions = CreateMutableCardDefinitions();
            var result = CreateTestBattle(
                buffConfigProvider: MakeBuffProvider(delayedVulnerable, vulnerable),
                mutableCardDefinitions: definitions);
            var (ctx, rm) = (result.Context, result.RoundManager);

            rm.BeginRound(ctx);
            CommitStrictPlanCard(
                ctx,
                rm,
                definitions,
                "P1",
                "pain_strike_card",
                "pain_strike",
                MakeDamageEffect(8),
                MakeAddBuffEffect("delayed_vulnerable_next_round", 50, 2, "Enemy")).Should().BeTrue();

            rm.EndRound(ctx);

            var heroP2 = ctx.AllPlayers["P2"].HeroEntity.EntityId;
            ctx.AllPlayers["P2"].HeroEntity.Hp.Should().Be(22);
            ctx.BuffManager.HasBuffType(ctx, heroP2, BuffType.Vulnerable).Should().BeFalse();
            ctx.BuffManager.HasBuffType(ctx, heroP2, BuffType.DelayedVulnerableNextRound).Should().BeTrue();

            rm.BeginRound(ctx);

            ctx.BuffManager.HasBuffType(ctx, heroP2, BuffType.Vulnerable).Should().BeTrue();
            PlayStrictInstantCard(ctx, rm, definitions, "P1", "followup_damage", "damage_4", MakeDamageEffect(4));
            ctx.AllPlayers["P2"].HeroEntity.Hp.Should().Be(16);

            rm.EndRound(ctx);
            ctx.BuffManager.HasBuffType(ctx, heroP2, BuffType.Vulnerable).Should().BeFalse();
            ctx.BuffManager.HasBuffType(ctx, heroP2, BuffType.DelayedVulnerableNextRound).Should().BeFalse();
        }

        [Fact]
        public void T40_Boomerang_ReturnsSourceCardToHand_AtRoundEnd()
        {
            var definitions = CreateMutableCardDefinitions(
                MakeCardDefinition(
                    "boomerang",
                    MakeDamageEffect(8),
                    MakeReturnSourceCardToHandAtRoundEndEffect()));
            var result = CreateTestBattle(mutableCardDefinitions: definitions);
            var (ctx, rm) = (result.Context, result.RoundManager);

            rm.BeginRound(ctx);
            var boomerang = GiveHandCard(ctx, "P1", "boomerang_card", "boomerang");
            rm.CommitPlanCard(ctx, new CommittedPlanCard
            {
                PlayerId = "P1",
                CardInstanceId = boomerang.InstanceId,
            }).Should().BeTrue();

            rm.EndRound(ctx);

            ctx.AllPlayers["P2"].HeroEntity.Hp.Should().Be(22);
            boomerang.Zone.Should().Be(CardZone.Hand);
            boomerang.ExtraData.Should().NotContainKey(ReturnSourceCardToHandAtRoundEndHandler.ReturnToHandAtRoundEndKey);
            boomerang.ExtraData.Should().NotContainKey(ReturnSourceCardToHandAtRoundEndHandler.ReturnToHandMarkedRoundKey);
        }

        [Fact]
        public void T41_Weapon_ProjectsCurrentHandOnly_AndClearsAtRoundEnd()
        {
            var definitions = CreateMutableCardDefinitions(
                MakeCardDefinitionWithUpgrade("strike", "strike_plus", MakeDamageEffect(5)),
                MakeCardDefinition("strike_plus", MakeDamageEffect(9)),
                MakeCardDefinition("weapon", MakeUpgradeCardsInHandEffect("EndOfTurn")));
            var result = CreateTestBattle(mutableCardDefinitions: definitions);
            var (ctx, rm) = (result.Context, result.RoundManager);

            rm.BeginRound(ctx);
            var strikeInHand = GiveHandCard(ctx, "P1", "strike_a", "strike");
            GiveHandCard(ctx, "P1", "weapon_card", "weapon");

            rm.PlayInstantCard(ctx, "P1", "weapon_card").Should().NotBeEmpty();
            strikeInHand.ProjectedConfigId.Should().Be("strike_plus");
            strikeInHand.ProjectionLifetime.Should().Be(CardProjectionLifetime.EndOfTurn);

            var drawnLater = ctx.CardManager.GenerateCard(ctx, "P1", "strike", CardZone.Hand, tempCard: false);
            drawnLater.HasProjection.Should().BeFalse();

            rm.EndRound(ctx);

            strikeInHand.HasProjection.Should().BeFalse();
            drawnLater.HasProjection.Should().BeFalse();
        }

        [Fact]
        public void T42_WeaponPlus_PersistsProjectionAcrossRounds()
        {
            var definitions = CreateMutableCardDefinitions(
                MakeCardDefinitionWithUpgrade("strike", "strike_plus", MakeDamageEffect(5)),
                MakeCardDefinition("strike_plus", MakeDamageEffect(9)),
                MakeCardDefinition("weapon_plus", MakeUpgradeCardsInHandEffect("EndOfBattle")));
            var result = CreateTestBattle(mutableCardDefinitions: definitions);
            var (ctx, rm) = (result.Context, result.RoundManager);

            rm.BeginRound(ctx);
            var strike = GiveHandCard(ctx, "P1", "strike_endbattle", "strike");
            GiveHandCard(ctx, "P1", "weapon_plus_card", "weapon_plus");

            rm.PlayInstantCard(ctx, "P1", "weapon_plus_card").Should().NotBeEmpty();
            strike.ProjectedConfigId.Should().Be("strike_plus");
            strike.ProjectionLifetime.Should().Be(CardProjectionLifetime.EndOfBattle);

            rm.EndRound(ctx);
            strike.ProjectedConfigId.Should().Be("strike_plus");
            strike.Zone.Should().Be(CardZone.Discard);

            rm.BeginRound(ctx);
            ctx.CardManager.MoveCard(ctx, strike, CardZone.Hand);
            rm.PlayInstantCard(ctx, "P1", strike.InstanceId).Should().NotBeEmpty();

            ctx.AllPlayers["P2"].HeroEntity.Hp.Should().Be(21);
        }

        [Fact]
        public void T43_Rampage_TracksPlayedCount_PerInstance()
        {
            var rampageDamage = new EffectUnit
            {
                EffectId = "rampage_damage",
                Type = EffectType.Damage,
                TargetType = "Enemy",
                ValueExpression = "{{6 + sourceCard.instancePlayedCount * 5}}",
                Layer = SettlementLayer.Damage,
            };

            var definitions = CreateMutableCardDefinitions(
                MakeCardDefinition("rampage", rampageDamage));
            var result = CreateTestBattle(mutableCardDefinitions: definitions);
            var (ctx, rm) = (result.Context, result.RoundManager);

            rm.BeginRound(ctx);
            var rampageA = GiveHandCard(ctx, "P1", "rampage_A", "rampage");
            var rampageB = GiveHandCard(ctx, "P1", "rampage_B", "rampage");

            rm.PlayInstantCard(ctx, "P1", rampageA.InstanceId).Should().NotBeEmpty();
            ctx.AllPlayers["P2"].HeroEntity.Hp.Should().Be(24);

            ctx.CardManager.MoveCard(ctx, rampageA, CardZone.Hand);
            rm.PlayInstantCard(ctx, "P1", rampageB.InstanceId).Should().NotBeEmpty();
            ctx.AllPlayers["P2"].HeroEntity.Hp.Should().Be(18);

            rm.PlayInstantCard(ctx, "P1", rampageA.InstanceId).Should().NotBeEmpty();
            ctx.AllPlayers["P2"].HeroEntity.Hp.Should().Be(7);
        }

        [Fact]
        public void T44_WeaponedRampage_UsesUpgradedFormula_AndKeepsInstanceHistory()
        {
            var baseRampage = new EffectUnit
            {
                EffectId = "rampage_damage",
                Type = EffectType.Damage,
                TargetType = "Enemy",
                ValueExpression = "{{6 + sourceCard.instancePlayedCount * 5}}",
                Layer = SettlementLayer.Damage,
            };
            var upgradedRampage = new EffectUnit
            {
                EffectId = "rampage_plus_damage",
                Type = EffectType.Damage,
                TargetType = "Enemy",
                ValueExpression = "{{6 + sourceCard.instancePlayedCount * 6}}",
                Layer = SettlementLayer.Damage,
            };

            var definitions = CreateMutableCardDefinitions(
                MakeCardDefinitionWithUpgrade("rampage", "rampage_plus", baseRampage),
                MakeCardDefinition("rampage_plus", upgradedRampage),
                MakeCardDefinition("weapon", MakeUpgradeCardsInHandEffect("EndOfTurn")));
            var result = CreateTestBattle(mutableCardDefinitions: definitions);
            var (ctx, rm) = (result.Context, result.RoundManager);

            rm.BeginRound(ctx);
            var rampage = GiveHandCard(ctx, "P1", "rampage_weaponed", "rampage");
            GiveHandCard(ctx, "P1", "weapon_for_rampage", "weapon");

            rm.PlayInstantCard(ctx, "P1", rampage.InstanceId).Should().NotBeEmpty();
            ctx.AllPlayers["P2"].HeroEntity.Hp.Should().Be(24);

            ctx.CardManager.MoveCard(ctx, rampage, CardZone.Hand);
            rm.PlayInstantCard(ctx, "P1", "weapon_for_rampage").Should().NotBeEmpty();
            rampage.ProjectedConfigId.Should().Be("rampage_plus");

            rm.PlayInstantCard(ctx, "P1", rampage.InstanceId).Should().NotBeEmpty();
            ctx.AllPlayers["P2"].HeroEntity.Hp.Should().Be(12);
        }

        [Fact]
        public void T45_BloodRitual_TriggersOnlyWhenRealHpDamagePositive()
        {
            var buffProvider = MakeBuffProvider(
                MakeBuffConfig("blood_ritual", BuffType.BloodRitual, 1),
                MakeBuffConfig("strength", BuffType.Strength, 0));
            var definitions = CreateMutableCardDefinitions();
            var result = CreateTestBattle(buffConfigProvider: buffProvider, mutableCardDefinitions: definitions);
            var (ctx, rm) = (result.Context, result.RoundManager);

            rm.BeginRound(ctx);
            var heroP1 = ctx.AllPlayers["P1"].HeroEntity;
            ctx.BuffManager.AddBuff(ctx, heroP1.EntityId, "blood_ritual", heroP1.EntityId, value: 1, duration: 0);

            PlayStrictInstantCard(ctx, rm, definitions, "P2", "blood_ritual_hit", "damage_4", MakeDamageEffect(4));
            ctx.AllPlayers["P1"].HeroEntity.Hp.Should().Be(26);
            ctx.BuffManager.GetBuffs(heroP1.EntityId).Should().ContainSingle(buff => buff.BuffType == BuffType.Strength && buff.Value == 1);

            PlayStrictInstantCard(ctx, rm, definitions, "P1", "blood_ritual_followup", "damage_5", MakeDamageEffect(5));
            ctx.AllPlayers["P2"].HeroEntity.Hp.Should().Be(24);
        }

        [Fact]
        public void T46_BloodRitual_DoesNotTrigger_WhenDamageIsFullyBlockedByShield()
        {
            var buffProvider = MakeBuffProvider(
                MakeBuffConfig("blood_ritual", BuffType.BloodRitual, 1),
                MakeBuffConfig("strength", BuffType.Strength, 0));
            var definitions = CreateMutableCardDefinitions();
            var result = CreateTestBattle(buffConfigProvider: buffProvider, mutableCardDefinitions: definitions);
            var (ctx, rm) = (result.Context, result.RoundManager);

            rm.BeginRound(ctx);
            var heroP1 = ctx.AllPlayers["P1"].HeroEntity;
            heroP1.Shield = 10;
            ctx.BuffManager.AddBuff(ctx, heroP1.EntityId, "blood_ritual", heroP1.EntityId, value: 1, duration: 0);

            PlayStrictInstantCard(ctx, rm, definitions, "P2", "blood_ritual_blocked", "damage_4", MakeDamageEffect(4));
            ctx.AllPlayers["P1"].HeroEntity.Hp.Should().Be(30);
            ctx.BuffManager.GetBuffs(heroP1.EntityId).Should().NotContain(buff => buff.BuffType == BuffType.Strength);

            PlayStrictInstantCard(ctx, rm, definitions, "P1", "blood_ritual_followup_blocked", "damage_5", MakeDamageEffect(5));
            ctx.AllPlayers["P2"].HeroEntity.Hp.Should().Be(25);
        }

        [Fact]
        public void T47_Corruption_ResetRoundQuota_FromTotalBuffValue()
        {
            var corruption = new BuffConfig
            {
                BuffId = "corruption",
                BuffName = "腐化",
                BuffType = BuffType.Corruption,
                DefaultValue = 2,
                DefaultDuration = 0,
                StackRule = BuffStackRule.StackValue,
                MaxStacks = 99,
            };

            var result = CreateTestBattle(buffConfigProvider: MakeBuffProvider(corruption));
            var (ctx, rm) = (result.Context, result.RoundManager);
            var heroP1 = ctx.AllPlayers["P1"].HeroEntity.EntityId;

            ctx.BuffManager.AddBuff(ctx, heroP1, "corruption", heroP1, value: 2, duration: 0);
            ctx.BuffManager.AddBuff(ctx, heroP1, "corruption", heroP1, value: 2, duration: 0);

            rm.BeginRound(ctx);

            ctx.AllPlayers["P1"].CorruptionFreePlaysRemainingThisRound.Should().Be(4);
        }

        [Fact]
        public void T48_Corruption_HitsZeroCostCard_AndStillConsumesCharge()
        {
            var corruption = new BuffConfig
            {
                BuffId = "corruption",
                BuffName = "腐化",
                BuffType = BuffType.Corruption,
                DefaultValue = 2,
                DefaultDuration = 0,
                StackRule = BuffStackRule.StackValue,
                MaxStacks = 99,
            };

            var definitions = CreateMutableCardDefinitions(
                MakeCardDefinition("zero_cost_card", false, false, 0, MakeDamageEffect(6)));
            var result = CreateTestBattle(
                buffConfigProvider: MakeBuffProvider(corruption),
                mutableCardDefinitions: definitions);
            var (ctx, rm) = (result.Context, result.RoundManager);
            var heroP1 = ctx.AllPlayers["P1"].HeroEntity.EntityId;

            ctx.BuffManager.AddBuff(ctx, heroP1, "corruption", heroP1, value: 2, duration: 0);
            rm.BeginRound(ctx);

            var zeroCostCard = GiveHandCard(ctx, "P1", "zero_cost_instance", "zero_cost_card");
            var playRules = rm.ResolvePlayRules(ctx, "P1", zeroCostCard);
            var cost = rm.ResolvePlayCost(ctx, "P1", zeroCostCard, playRules);

            cost.BaseCost.Should().Be(0);
            cost.FinalCost.Should().Be(0);
            playRules.HitCorruption.Should().BeTrue();
            playRules.ConsumeCorruptionChargeOnSuccess.Should().BeTrue();
            playRules.ForceConsumeAfterResolve.Should().BeTrue();

            rm.CommitSuccessfulPlayRules(ctx, "P1", playRules);
            ctx.AllPlayers["P1"].CorruptionFreePlaysRemainingThisRound.Should().Be(1);
        }

        [Fact]
        public void T49_Corruption_FirstTwoPlaysAreFree_ThirdUsesBaseCost()
        {
            var corruption = new BuffConfig
            {
                BuffId = "corruption",
                BuffName = "腐化",
                BuffType = BuffType.Corruption,
                DefaultValue = 2,
                DefaultDuration = 0,
                StackRule = BuffStackRule.StackValue,
                MaxStacks = 99,
            };

            var definitions = CreateMutableCardDefinitions(
                MakeCardDefinition("expensive_a", false, false, 3, MakeDamageEffect(5)),
                MakeCardDefinition("expensive_b", false, false, 2, MakeDamageEffect(5)),
                MakeCardDefinition("expensive_c", false, false, 1, MakeDamageEffect(5)));
            var result = CreateTestBattle(
                buffConfigProvider: MakeBuffProvider(corruption),
                mutableCardDefinitions: definitions);
            var (ctx, rm) = (result.Context, result.RoundManager);
            var heroP1 = ctx.AllPlayers["P1"].HeroEntity.EntityId;

            ctx.BuffManager.AddBuff(ctx, heroP1, "corruption", heroP1, value: 2, duration: 0);
            rm.BeginRound(ctx);

            var cardA = GiveHandCard(ctx, "P1", "corrupt_a", "expensive_a");
            var cardB = GiveHandCard(ctx, "P1", "corrupt_b", "expensive_b");
            var cardC = GiveHandCard(ctx, "P1", "corrupt_c", "expensive_c");

            var rulesA = rm.ResolvePlayRules(ctx, "P1", cardA);
            var costA = rm.ResolvePlayCost(ctx, "P1", cardA, rulesA);
            costA.FinalCost.Should().Be(0);
            rulesA.HitCorruption.Should().BeTrue();
            rm.CommitSuccessfulPlayRules(ctx, "P1", rulesA);

            var rulesB = rm.ResolvePlayRules(ctx, "P1", cardB);
            var costB = rm.ResolvePlayCost(ctx, "P1", cardB, rulesB);
            costB.FinalCost.Should().Be(0);
            rulesB.HitCorruption.Should().BeTrue();
            rm.CommitSuccessfulPlayRules(ctx, "P1", rulesB);

            var rulesC = rm.ResolvePlayRules(ctx, "P1", cardC);
            var costC = rm.ResolvePlayCost(ctx, "P1", cardC, rulesC);
            costC.BaseCost.Should().Be(1);
            costC.FinalCost.Should().Be(1);
            rulesC.HitCorruption.Should().BeFalse();
        }

        [Fact]
        public void T50_Corruption_SecondCastStacksQuota_ForNextRound()
        {
            var corruption = new BuffConfig
            {
                BuffId = "corruption",
                BuffName = "腐化",
                BuffType = BuffType.Corruption,
                DefaultValue = 2,
                DefaultDuration = 0,
                StackRule = BuffStackRule.StackValue,
                MaxStacks = 99,
            };

            var result = CreateTestBattle(buffConfigProvider: MakeBuffProvider(corruption));
            var (ctx, rm) = (result.Context, result.RoundManager);
            var heroP1 = ctx.AllPlayers["P1"].HeroEntity.EntityId;

            ctx.BuffManager.AddBuff(ctx, heroP1, "corruption", heroP1, value: 2, duration: 0);
            rm.BeginRound(ctx);
            ctx.AllPlayers["P1"].CorruptionFreePlaysRemainingThisRound.Should().Be(2);

            rm.CommitSuccessfulPlayRules(ctx, "P1", new PlayRuleResolution
            {
                ConsumeCorruptionChargeOnSuccess = true,
                HitCorruption = true,
                ForceConsumeAfterResolve = true,
            });
            ctx.AllPlayers["P1"].CorruptionFreePlaysRemainingThisRound.Should().Be(1);

            rm.EndRound(ctx);
            ctx.BuffManager.AddBuff(ctx, heroP1, "corruption", heroP1, value: 2, duration: 0);
            rm.BeginRound(ctx);

            ctx.AllPlayers["P1"].CorruptionFreePlaysRemainingThisRound.Should().Be(4);
        }

        [Fact]
        public void T51_ResolvePlayCost_UsesEffectiveProjectedConfigEnergyCost()
        {
            var definitions = CreateMutableCardDefinitions(
                MakeCardDefinitionWithUpgrade("strike", "strike_plus", 1, MakeDamageEffect(5)),
                MakeCardDefinition("strike_plus", false, false, 4, MakeDamageEffect(9)),
                MakeCardDefinition("weapon", MakeUpgradeCardsInHandEffect("EndOfTurn")));
            var result = CreateTestBattle(mutableCardDefinitions: definitions);
            var (ctx, rm) = (result.Context, result.RoundManager);

            rm.BeginRound(ctx);
            var strike = GiveHandCard(ctx, "P1", "strike_cost_projection", "strike");
            GiveHandCard(ctx, "P1", "weapon_cost_projection", "weapon");

            rm.ResolvePlayCost(ctx, "P1", strike).FinalCost.Should().Be(1);
            rm.PlayInstantCard(ctx, "P1", "weapon_cost_projection").Should().NotBeEmpty();
            rm.ResolvePlayCost(ctx, "P1", strike).FinalCost.Should().Be(4);
        }

        [Fact]
        public void T52_Retrieve_MovesSelectedDiscardCard_ToDeckTop_AndDrawsExactInstance()
        {
            var definitions = CreateMutableCardDefinitions(
                MakeCardDefinition("retrieve", MakeMoveSelectedCardToDeckTopEffect()),
                MakeCardDefinition("deck_filler", MakeDamageEffect(1)),
                MakeCardDefinition("rampage", MakeDamageEffect(6)));
            var result = CreateTestBattle(mutableCardDefinitions: definitions);
            var (ctx, rm) = (result.Context, result.RoundManager);

            rm.BeginRound(ctx);
            var retrieve = GiveHandCard(ctx, "P1", "retrieve_card", "retrieve");
            var selected = GiveCardInZone(ctx, "P1", "discard_target", CardZone.Discard, "rampage");
            GiveCardInZone(ctx, "P1", "deck_card", CardZone.Deck, "deck_filler");

            var playResults = rm.PlayInstantCard(
                ctx,
                "P1",
                retrieve.InstanceId,
                new Dictionary<string, string>
                {
                    [MoveSelectedCardToDeckTopHandler.SelectedCardInstanceIdKey] = selected.InstanceId,
                });

            playResults.Should().ContainSingle();
            playResults[0].Success.Should().BeTrue();
            selected.Zone.Should().Be(CardZone.Deck);

            var drawn = ctx.CardManager.DrawCards(ctx, "P1", 1);
            drawn.Should().ContainSingle();
            drawn[0].InstanceId.Should().Be(selected.InstanceId);
        }

        [Fact]
        public void T53_Retrieve_RejectsSelectedCard_OutsideOwnDiscard()
        {
            var definitions = CreateMutableCardDefinitions(
                MakeCardDefinition("retrieve", MakeMoveSelectedCardToDeckTopEffect()),
                MakeCardDefinition("deck_filler", MakeDamageEffect(1)));
            var result = CreateTestBattle(mutableCardDefinitions: definitions);
            var (ctx, rm) = (result.Context, result.RoundManager);

            rm.BeginRound(ctx);
            var retrieve = GiveHandCard(ctx, "P1", "retrieve_invalid", "retrieve");
            var enemyDiscard = GiveCardInZone(ctx, "P2", "enemy_discard_target", CardZone.Discard, "deck_filler");
            var ownHandCard = GiveHandCard(ctx, "P1", "own_hand_target", "deck_filler");

            var enemySelectionResult = rm.PlayInstantCard(
                ctx,
                "P1",
                retrieve.InstanceId,
                new Dictionary<string, string>
                {
                    [MoveSelectedCardToDeckTopHandler.SelectedCardInstanceIdKey] = enemyDiscard.InstanceId,
                });

            enemySelectionResult.Should().ContainSingle();
            enemySelectionResult[0].Success.Should().BeFalse();
            enemyDiscard.Zone.Should().Be(CardZone.Discard);

            ctx.CardManager.MoveCard(ctx, retrieve, CardZone.Hand);
            var handSelectionResult = rm.PlayInstantCard(
                ctx,
                "P1",
                retrieve.InstanceId,
                new Dictionary<string, string>
                {
                    [MoveSelectedCardToDeckTopHandler.SelectedCardInstanceIdKey] = ownHandCard.InstanceId,
                });

            handSelectionResult.Should().ContainSingle();
            handSelectionResult[0].Success.Should().BeFalse();
            ownHandCard.Zone.Should().Be(CardZone.Hand);
        }

        [Fact]
        public void T54_Retrieve_PreservesSelectedCard_InstanceState()
        {
            var definitions = CreateMutableCardDefinitions(
                MakeCardDefinition("retrieve", MakeMoveSelectedCardToDeckTopEffect()),
                MakeCardDefinition("rampage", MakeDamageEffect(6)));
            var result = CreateTestBattle(mutableCardDefinitions: definitions);
            var (ctx, rm) = (result.Context, result.RoundManager);

            rm.BeginRound(ctx);
            var retrieve = GiveHandCard(ctx, "P1", "retrieve_preserve", "retrieve");
            var selected = GiveCardInZone(ctx, "P1", "rampage_preserve", CardZone.Discard, "rampage");
            ctx.AllPlayers["P1"].PlayedCountByInstanceId[selected.InstanceId] = 2;

            var playResults = rm.PlayInstantCard(
                ctx,
                "P1",
                retrieve.InstanceId,
                new Dictionary<string, string>
                {
                    [MoveSelectedCardToDeckTopHandler.SelectedCardInstanceIdKey] = selected.InstanceId,
                });

            playResults.Should().ContainSingle();
            playResults[0].Success.Should().BeTrue();
            ctx.AllPlayers["P1"].PlayedCountByInstanceId[selected.InstanceId].Should().Be(2);

            var drawn = ctx.CardManager.DrawCards(ctx, "P1", 1);
            drawn.Should().ContainSingle();
            drawn[0].InstanceId.Should().Be(selected.InstanceId);
            ctx.AllPlayers["P1"].PlayedCountByInstanceId[drawn[0].InstanceId].Should().Be(2);
        }

        [Fact]
        public void T55_PlanCard_CommitsAsSnapshot_AndLeavesHandImmediately()
        {
            var definitions = CreateMutableCardDefinitions(
                MakeCardDefinition("plan_damage", MakeDamageEffect(7)));
            var result = CreateTestBattle(mutableCardDefinitions: definitions);
            var (ctx, rm) = (result.Context, result.RoundManager);

            rm.BeginRound(ctx);
            var planCard = GiveHandCard(ctx, "P1", "plan_snapshot_card", "plan_damage");

            rm.CommitPlanCard(ctx, new CommittedPlanCard
            {
                PlayerId = "P1",
                CardInstanceId = planCard.InstanceId,
            }).Should().BeTrue();

            planCard.Zone.Should().Be(CardZone.Discard);
            ctx.AllPlayers["P1"].GetCardsInZone(CardZone.StrategyZone).Should().BeEmpty();
            ctx.PendingPlanSnapshots.Should().ContainSingle();
            ctx.PendingPlanSnapshots[0].SourceCardInstanceId.Should().Be(planCard.InstanceId);
            ctx.PendingPlanSnapshots[0].CommittedEffectiveConfigId.Should().Be("plan_damage");

            rm.EndRound(ctx);
            ctx.AllPlayers["P2"].HeroEntity.Hp.Should().Be(23);
        }

        [Fact]
        public void T56_PlanSnapshot_FreezesPlayedCountInput_WhenSameInstanceIsCommittedAgain()
        {
            var rampageDamage = new EffectUnit
            {
                EffectId = "rampage_plan_damage",
                Type = EffectType.Damage,
                TargetType = "Enemy",
                ValueExpression = "{{6 + frozen.sourceCard.instancePlayedCount * 5}}",
                Layer = SettlementLayer.Damage,
            };

            var definitions = CreateMutableCardDefinitions(
                MakeCardDefinition("rampage_plan", rampageDamage));
            var result = CreateTestBattle(mutableCardDefinitions: definitions);
            var (ctx, rm) = (result.Context, result.RoundManager);

            rm.BeginRound(ctx);
            var rampage = GiveHandCard(ctx, "P1", "rampage_plan_instance", "rampage_plan");

            rm.CommitPlanCard(ctx, new CommittedPlanCard
            {
                PlayerId = "P1",
                CardInstanceId = rampage.InstanceId,
            }).Should().BeTrue();

            ctx.PendingPlanSnapshots.Should().HaveCount(1);
            ctx.PendingPlanSnapshots[0].FrozenInputs["sourceCard.instancePlayedCount"].Should().Be("0");
            rampage.Zone.Should().Be(CardZone.Discard);

            ctx.CardManager.MoveCard(ctx, rampage, CardZone.Hand);
            rm.CommitPlanCard(ctx, new CommittedPlanCard
            {
                PlayerId = "P1",
                CardInstanceId = rampage.InstanceId,
            }).Should().BeTrue();

            ctx.PendingPlanSnapshots.Should().HaveCount(2);
            ctx.PendingPlanSnapshots[1].FrozenInputs["sourceCard.instancePlayedCount"].Should().Be("1");

            rm.EndRound(ctx);
            ctx.AllPlayers["P2"].HeroEntity.Hp.Should().Be(13);
        }

        [Fact]
        public void T57_ShieldSlam_UsesSettlementSnapshotShield_NotCommitTimeShield()
        {
            var shieldSlamDamage = new EffectUnit
            {
                EffectId = "shield_slam_damage",
                Type = EffectType.Damage,
                TargetType = "Enemy",
                ValueExpression = "{{6 + snapshot.self.shield}}",
                Layer = SettlementLayer.Damage,
            };

            var definitions = CreateMutableCardDefinitions(
                MakeCardDefinition("shield_slam", shieldSlamDamage));
            var result = CreateTestBattle(mutableCardDefinitions: definitions);
            var (ctx, rm) = (result.Context, result.RoundManager);

            rm.BeginRound(ctx);
            var shieldSlam = GiveHandCard(ctx, "P1", "shield_slam_instance", "shield_slam");
            ctx.AllPlayers["P1"].HeroEntity.Shield = 5;

            rm.CommitPlanCard(ctx, new CommittedPlanCard
            {
                PlayerId = "P1",
                CardInstanceId = shieldSlam.InstanceId,
            }).Should().BeTrue();

            ctx.AllPlayers["P1"].HeroEntity.Shield = 2;
            rm.EndRound(ctx);

            ctx.AllPlayers["P2"].HeroEntity.Hp.Should().Be(22);
        }
    }
}




