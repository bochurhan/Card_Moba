using System;
using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using Xunit;
using CardMoba.BattleCore.Core;
using CardMoba.BattleCore.Results;
using CardMoba.ConfigModels.Card;
using CardMoba.MatchFlow.Catalog;
using CardMoba.MatchFlow.Context;
using CardMoba.MatchFlow.Core;
using CardMoba.MatchFlow.Deck;
using CardMoba.MatchFlow.Definitions;
using CardMoba.MatchFlow.Rules;
using CardMoba.Protocol.Enums;

namespace CardMoba.Tests
{
    public class MatchFlowTests
    {
        [Fact]
        public void CreateMatch_RegistersPlayersTeamsAndRuleset()
        {
            var factory = new MatchFactory();
            var ruleset = CreateRuleset();

            var context = factory.CreateMatch(
                "match-alpha",
                ruleset,
                CreatePlayers(),
                baseRandomSeed: 123);

            context.MatchId.Should().Be("match-alpha");
            context.BaseRandomSeed.Should().Be(123);
            context.Ruleset.Should().BeSameAs(ruleset);
            context.Players.Should().ContainKey("P1");
            context.Players.Should().ContainKey("P2");
            context.Teams.Should().ContainKey("TeamA");
            context.Teams.Should().ContainKey("TeamB");
            context.Teams["TeamA"].PlayerIds.Should().ContainSingle().Which.Should().Be("P1");
            context.Teams["TeamB"].PlayerIds.Should().ContainSingle().Which.Should().Be("P2");
            context.Players["P1"].Loadout.ClassId.Should().Be(HeroClass.Warrior);
        }

        [Fact]
        public void BuildCatalogAssembler_MapsCardConfigsAndRegistersDefaultPools()
        {
            var assembler = new BuildCatalogAssembler();
            var catalog = assembler.Create(
                new[]
                {
                    MakeCardConfig(1001, HeroClass.Warrior, rarity: 1, upgradedCardConfigId: "1002"),
                    MakeCardConfig(1002, HeroClass.Warrior, rarity: 1),
                    MakeCardConfig(2001, HeroClass.Warrior, rarity: 3),
                    MakeCardConfig(3001, HeroClass.Universal, rarity: 1, tags: CardTag.Status),
                    MakeCardConfig(9001, HeroClass.Warrior, rarity: 5, tags: CardTag.Legendary),
                },
                new[]
                {
                    new EquipmentDefinition
                    {
                        EquipmentId = "burning_blood",
                        ClassId = HeroClass.Warrior,
                        EffectType = EquipmentEffectType.HealAfterBattleFlat,
                        EffectValue = 6,
                    },
                });

            catalog.GetCardDefinition("1001")!.CanUpgrade.Should().BeTrue();
            catalog.GetCardDefinition("1001")!.CanAppearInBuildReward.Should().BeTrue();
            catalog.GetCardDefinition("1002")!.CanAppearInBuildReward.Should().BeFalse();
            catalog.GetCardDefinition("3001")!.CanAppearInBuildReward.Should().BeFalse();
            catalog.GetCardDefinition("3001")!.CanRemove.Should().BeFalse();
            catalog.GetCardDefinition("9001")!.CanAppearInBuildReward.Should().BeFalse();
            catalog.GetDraftPoolCards(DefaultWarriorPoolId, HeroClass.Warrior)
                .Select(card => card.ConfigId)
                .Should().Contain(new[] { "1001", "2001" })
                .And.NotContain(new[] { "1002", "3001", "9001" });
            catalog.GetEquipmentDefinition("burning_blood")!.EffectValue.Should().Be(6);
        }

        [Fact]
        public void BattleSetupBuilder_UsesPersistentHpIncludingZero_AndBuildsDeckConfig()
        {
            var context = CreateMatchContext(CreateRuleset(), p1Hp: 22, p2Hp: 0);
            var builder = new BattleSetupBuilder();

            var plan = builder.BuildCurrentStep(context);

            plan.BattleId.Should().Be("match-test_step_00");
            plan.BattleSeed.Should().Be(77);
            plan.Players.Should().HaveCount(2);
            plan.Players[0].PlayerId.Should().Be("P1");
            plan.Players[0].InitialHp.Should().Be(22);
            plan.Players[0].UseInitialHp.Should().BeTrue();
            plan.Players[0].DeckConfig.Should().Contain(("slash", 2));
            plan.Players[0].DeckConfig.Should().Contain(("guard", 1));
            plan.Players[1].PlayerId.Should().Be("P2");
            plan.Players[1].InitialHp.Should().Be(0);
            plan.Players[1].UseInitialHp.Should().BeTrue();
        }

        [Fact]
        public void StartMatch_StartsFirstBattleStep_AndRespectsPersistentHp()
        {
            var context = CreateMatchContext(CreateRuleset(), p1Hp: 30, p2Hp: 0, includeDefaultEquipment: true);
            var manager = CreateMatchManager();

            manager.StartMatch(context);

            context.CurrentPhase.Should().Be(MatchPhase.BattleInProgress);
            context.ActiveBattleContext.Should().NotBeNull();
            context.ActiveRoundManager.Should().NotBeNull();
            context.ActiveBattleContext!.AllPlayers["P2"].HeroEntity.Hp.Should().Be(0);
            context.ActiveBattleContext.AllPlayers["P2"].HeroEntity.IsAlive.Should().BeFalse();
            context.ActiveEquipmentRuntimes.Should().HaveCount(2);
        }

        [Fact]
        public void CompleteCurrentBattle_OpensBuildWindowWithForcedRecoveryAndEquipmentHealing()
        {
            var ruleset = CreateRuleset(openBuildWindowAfterFirst: true, includeSecondStep: true);
            var context = CreateMatchContext(ruleset, includeDefaultEquipment: true);
            var manager = CreateMatchManager();
            manager.StartMatch(context);

            context.ActiveBattleContext!.AllPlayers["P1"].HeroEntity.Hp = 10;
            context.ActiveBattleContext.AllPlayers["P2"].HeroEntity.Hp = 0;
            var summary = new BattleSummary
            {
                BattleId = context.ActiveBattleContext.BattleId,
                BattleEndReason = BattleEndReason.RoundLimitReached,
            };
            summary.DeadPlayerIds.Add("P2");

            manager.CompleteCurrentBattle(context, summary);

            context.Players["P1"].PersistentHp.Should().Be(16);
            context.Players["P2"].PersistentHp.Should().Be(6);
            context.CurrentPhase.Should().Be(MatchPhase.BuildWindow);
            context.ActiveBuildWindow.Should().NotBeNull();

            var p1Window = context.ActiveBuildWindow!.PlayerWindows["P1"];
            p1Window.RestrictionMode.Should().Be(BuildWindowRestrictionMode.None);
            p1Window.OpportunityCount.Should().Be(1);
            p1Window.Opportunities[0].AvailableActions.Should().Contain(new[]
            {
                BuildActionType.Heal,
                BuildActionType.UpgradeCard,
                BuildActionType.RemoveCard,
                BuildActionType.AddCard,
            });
            p1Window.Opportunities[0].Offers.DraftGroupsRevealed.Should().BeFalse();
            p1Window.Opportunities[0].Offers.DraftGroups.Should().BeEmpty();

            var p2Window = context.ActiveBuildWindow.PlayerWindows["P2"];
            p2Window.RestrictionMode.Should().Be(BuildWindowRestrictionMode.ForcedRecovery);
            p2Window.OpportunityCount.Should().Be(1);
            p2Window.PreviewHp.Should().Be(6);
            p2Window.Opportunities[0].AvailableActions.Should().ContainSingle().Which.Should().Be(BuildActionType.Heal);
            p2Window.Opportunities[0].Offers.HealAmount.Should().Be(12);
        }

        [Fact]
        public void ApplyBuildActions_UpgradeAddAndRemove_AreCommittedOnAdvance()
        {
            var ruleset = CreateRuleset(openBuildWindowAfterFirst: true, includeSecondStep: true);
            var context = CreateMatchContext(ruleset);
            var manager = CreateMatchManager();
            manager.StartMatch(context);

            var summary = new BattleSummary
            {
                BattleId = context.ActiveBattleContext!.BattleId,
                BattleEndReason = BattleEndReason.RoundLimitReached,
            };
            summary.ExtraBuildPickPlayerIds.Add("P1");
            manager.CompleteCurrentBattle(context, summary);

            var p1Window = context.ActiveBuildWindow!.PlayerWindows["P1"];
            p1Window.OpportunityCount.Should().Be(2);
            var upgradeTargetId = p1Window.Opportunities[0].Offers.UpgradableCards.First(card => card.EffectiveConfigId == "slash").PersistentCardId;
            manager.ApplyBuildAction(context, "P1", new BuildChoice
            {
                ActionType = BuildActionType.UpgradeCard,
                TargetPersistentCardId = upgradeTargetId,
            });

            var addOpportunity = context.ActiveBuildWindow.PlayerWindows["P1"].Opportunities[1];
            addOpportunity.Offers.DraftGroupsRevealed.Should().BeFalse();
            addOpportunity.Offers.DraftGroups.Should().BeEmpty();

            manager.ApplyBuildAction(context, "P1", BuildActionType.AddCard);

            context.ActiveBuildWindow.PlayerWindows["P1"].NextOpportunityIndex.Should().Be(1);
            addOpportunity.CommittedActionType.Should().Be(BuildActionType.AddCard);
            addOpportunity.Offers.DraftGroupsRevealed.Should().BeTrue();
            addOpportunity.Offers.DraftGroups.Should().HaveCountGreaterThan(0);
            addOpportunity.AvailableActions.Should().ContainSingle().Which.Should().Be(BuildActionType.AddCard);

            var addChoice = BuildChoice.Create(BuildActionType.AddCard);
            foreach (var group in addOpportunity.Offers.DraftGroups)
                addChoice.SelectedDraftOfferIdsByGroup[group.GroupIndex] = group.Offers.First().OfferId;
            manager.ApplyBuildAction(context, "P1", addChoice);
            manager.LockBuildChoice(context, "P1");

            var p2Window = context.ActiveBuildWindow.PlayerWindows["P2"];
            var removeTargetId = p2Window.Opportunities[0].Offers.RemovableCards.First().PersistentCardId;
            manager.ApplyBuildAction(context, "P2", new BuildChoice
            {
                ActionType = BuildActionType.RemoveCard,
                TargetPersistentCardId = removeTargetId,
            });
            manager.LockBuildChoice(context, "P2");

            manager.AdvanceIfReady(context).Should().BeTrue();

            context.CurrentStepIndex.Should().Be(1);
            context.CurrentPhase.Should().Be(MatchPhase.BattleInProgress);
            context.Players["P1"].BonusBuildPickCount.Should().Be(0);
            context.Players["P1"].WasDefeatedInLastBattle.Should().BeFalse();
            context.Players["P1"].Deck.FindCard(upgradeTargetId)!.CurrentConfigId.Should().Be("slash_plus");
            context.Players["P1"].Deck.Cards.Should().HaveCount(5);
            context.Players["P2"].Deck.Cards.Should().HaveCount(1);
        }

        [Fact]
        public void AddCardCommit_RevealsDraftGroups_AndPreventsSwitchingToOtherActions()
        {
            var ruleset = CreateRuleset(openBuildWindowAfterFirst: true, includeSecondStep: true);
            var context = CreateMatchContext(ruleset);
            var manager = CreateMatchManager();
            manager.StartMatch(context);

            var summary = new BattleSummary
            {
                BattleId = context.ActiveBattleContext!.BattleId,
                BattleEndReason = BattleEndReason.RoundLimitReached,
            };
            summary.ExtraBuildPickPlayerIds.Add("P1");
            manager.CompleteCurrentBattle(context, summary);

            manager.ApplyBuildAction(context, "P1", BuildActionType.Heal);

            var addOpportunity = context.ActiveBuildWindow!.PlayerWindows["P1"].Opportunities[1];
            manager.ApplyBuildAction(context, "P1", BuildActionType.AddCard);

            addOpportunity.CommittedActionType.Should().Be(BuildActionType.AddCard);
            addOpportunity.Offers.DraftGroupsRevealed.Should().BeTrue();
            addOpportunity.IsResolved.Should().BeFalse();

            Action act = () => manager.ApplyBuildAction(context, "P1", BuildActionType.Heal);
            act.Should().Throw<InvalidOperationException>();
        }

        [Fact]
        public void ForcedRecoveryWindow_RejectsNonHealActions()
        {
            var ruleset = CreateRuleset(openBuildWindowAfterFirst: true, includeSecondStep: true);
            var context = CreateMatchContext(ruleset);
            var manager = CreateMatchManager();
            manager.StartMatch(context);

            context.ActiveBattleContext!.AllPlayers["P2"].HeroEntity.Hp = 0;
            var summary = new BattleSummary
            {
                BattleId = context.ActiveBattleContext.BattleId,
                BattleEndReason = BattleEndReason.RoundLimitReached,
            };
            summary.DeadPlayerIds.Add("P2");
            manager.CompleteCurrentBattle(context, summary);

            Action act = () => manager.ApplyBuildAction(context, "P2", BuildActionType.RemoveCard);
            act.Should().Throw<InvalidOperationException>();
        }

        [Fact]
        public void CompleteCurrentBattle_MatchTerminated_EndsMatchAndFlagsDestroyedObjective()
        {
            var ruleset = CreateRuleset(includeObjective: true);
            var context = CreateMatchContext(ruleset);
            var manager = CreateMatchManager();
            manager.StartMatch(context);

            var summary = new BattleSummary
            {
                BattleId = context.ActiveBattleContext!.BattleId,
                MatchTerminated = true,
                MatchEndReason = MatchEndReason.ObjectiveDestroyed,
                WinningTeamId = "TeamA",
                DestroyedObjectiveEntityId = "crystal_team_b",
            };

            manager.CompleteCurrentBattle(context, summary);

            context.IsMatchOver.Should().BeTrue();
            context.CurrentPhase.Should().Be(MatchPhase.MatchEnded);
            context.WinnerTeamId.Should().Be("TeamA");
            context.Teams["TeamB"].ObjectiveDestroyed.Should().BeTrue();
        }

        [Fact]
        public void HandleBuildWindowTimeout_DefaultsRemainingChoicesToHealAndAdvances()
        {
            var ruleset = CreateRuleset(openBuildWindowAfterFirst: true, includeSecondStep: true);
            var context = CreateMatchContext(ruleset);
            var manager = CreateMatchManager();
            manager.StartMatch(context);

            var summary = new BattleSummary
            {
                BattleId = context.ActiveBattleContext!.BattleId,
                BattleEndReason = BattleEndReason.RoundLimitReached,
            };
            summary.ExtraBuildPickPlayerIds.Add("P1");
            manager.CompleteCurrentBattle(context, summary);

            context.ActiveBuildWindow!.DeadlineUnixMs = 10;
            manager.HandleBuildWindowTimeout(context, nowUnixMs: 10).Should().BeTrue();

            context.CurrentStepIndex.Should().Be(1);
            context.CurrentPhase.Should().Be(MatchPhase.BattleInProgress);
            context.Players["P1"].PersistentHp.Should().Be(30);
            context.Players["P2"].PersistentHp.Should().Be(30);
        }

        [Fact]
        public void CompleteCurrentBattle_UsesRoundManagerCompletedSummary_WhenNoSummaryIsProvided()
        {
            var ruleset = new MatchRuleset();
            var firstStep = new MatchBattleStep
            {
                StepId = "step_auto_summary",
                Mode = BattleStepMode.Duel1v1,
                BattleRuleset = new BattleRuleset
                {
                    LocalEndPolicy = BattleLocalEndPolicy.RoundLimit,
                    MaxRounds = 1,
                },
                OpensBuildWindowAfter = true,
                BuildPoolId = DefaultWarriorPoolId,
            };
            firstStep.ParticipantPlayerIds.Add("P1");
            firstStep.ParticipantPlayerIds.Add("P2");

            var secondStep = new MatchBattleStep
            {
                StepId = "step_followup",
                Mode = BattleStepMode.Duel1v1,
                BattleRuleset = new BattleRuleset
                {
                    LocalEndPolicy = BattleLocalEndPolicy.RoundLimit,
                    MaxRounds = 2,
                },
            };
            secondStep.ParticipantPlayerIds.Add("P1");
            secondStep.ParticipantPlayerIds.Add("P2");

            ruleset.Steps.Add(firstStep);
            ruleset.Steps.Add(secondStep);

            var context = CreateMatchContext(ruleset);
            var manager = CreateMatchManager();
            manager.StartMatch(context);

            context.ActiveBattleContext!.AllPlayers["P2"].HeroEntity.Hp = 0;
            context.ActiveRoundManager!.BeginRound(context.ActiveBattleContext);
            context.ActiveRoundManager.EndRound(context.ActiveBattleContext);

            context.ActiveRoundManager.CompletedBattleSummary.Should().NotBeNull();
            manager.CompleteCurrentBattle(context);

            context.Players["P2"].PersistentHp.Should().Be(0);
            context.Players["P1"].BonusBuildPickCount.Should().Be(1);
            context.CurrentPhase.Should().Be(MatchPhase.BuildWindow);
            context.ActiveBuildWindow.Should().NotBeNull();
            context.ActiveBuildWindow!.PlayerWindows["P2"].RestrictionMode.Should().Be(BuildWindowRestrictionMode.ForcedRecovery);
        }

        private static string DefaultWarriorPoolId => BuildCatalogAssembler.GetDefaultPoolId(HeroClass.Warrior);

        private static MatchManager CreateMatchManager(InMemoryBuildCatalog? catalog = null)
        {
            return new MatchManager(new BattleFactory(), buildCatalog: catalog ?? CreateCatalog());
        }

        private static MatchContext CreateMatchContext(
            MatchRuleset ruleset,
            int p1Hp = 30,
            int p2Hp = 30,
            bool includeDefaultEquipment = false)
        {
            var factory = new MatchFactory();
            return factory.CreateMatch(
                "match-test",
                ruleset,
                CreatePlayers(p1Hp, p2Hp, includeDefaultEquipment),
                baseRandomSeed: 77);
        }

        private static IEnumerable<PlayerMatchState> CreatePlayers(
            int p1Hp = 30,
            int p2Hp = 30,
            bool includeDefaultEquipment = false)
        {
            return new[]
            {
                new PlayerMatchState
                {
                    PlayerId = "P1",
                    TeamId = "TeamA",
                    PersistentHp = p1Hp,
                    MaxHp = 30,
                    Deck = CreateDeck("slash", "slash", "guard"),
                    Loadout = CreateLoadout(includeDefaultEquipment),
                },
                new PlayerMatchState
                {
                    PlayerId = "P2",
                    TeamId = "TeamB",
                    PersistentHp = p2Hp,
                    MaxHp = 30,
                    Deck = CreateDeck("bolt", "guard"),
                    Loadout = CreateLoadout(includeDefaultEquipment),
                },
            };
        }

        private static PlayerLoadout CreateLoadout(bool includeDefaultEquipment)
        {
            var loadout = new PlayerLoadout
            {
                ClassId = HeroClass.Warrior,
                DefaultBuildPoolId = DefaultWarriorPoolId,
            };
            if (includeDefaultEquipment)
                loadout.EquipmentIds.Add("burning_blood");
            return loadout;
        }

        private static MatchRuleset CreateRuleset(
            bool openBuildWindowAfterFirst = false,
            bool includeSecondStep = false,
            bool includeObjective = false)
        {
            var ruleset = new MatchRuleset();
            var firstStep = new MatchBattleStep
            {
                StepId = "step_01",
                Mode = BattleStepMode.Duel1v1,
                BattleRuleset = new BattleRuleset
                {
                    LocalEndPolicy = BattleLocalEndPolicy.RoundLimit,
                    MaxRounds = 3,
                    EnableObjectives = includeObjective,
                    MatchTerminalPolicy = includeObjective
                        ? MatchTerminalPolicy.ObjectiveDestroyed
                        : MatchTerminalPolicy.None,
                },
                OpensBuildWindowAfter = openBuildWindowAfterFirst,
                BuildPoolId = DefaultWarriorPoolId,
            };
            firstStep.ParticipantPlayerIds.Add("P1");
            firstStep.ParticipantPlayerIds.Add("P2");

            if (includeObjective)
            {
                var objective = new ObjectiveSetupData
                {
                    EntityId = "crystal_team_b",
                    TeamId = "TeamB",
                    MaxHp = 20,
                    InitialHp = 20,
                    IsTargetable = true,
                    EndsMatchWhenDestroyed = true,
                };
                objective.RequiredDeadEntityIdsToTarget.Add("hero_P2");
                firstStep.Objectives.Add(objective);
            }

            ruleset.Steps.Add(firstStep);

            if (includeSecondStep)
            {
                var secondStep = new MatchBattleStep
                {
                    StepId = "step_02",
                    Mode = BattleStepMode.Duel1v1,
                    BattleRuleset = new BattleRuleset
                    {
                        LocalEndPolicy = BattleLocalEndPolicy.RoundLimit,
                        MaxRounds = 2,
                    },
                    BuildPoolId = DefaultWarriorPoolId,
                };
                secondStep.ParticipantPlayerIds.Add("P1");
                secondStep.ParticipantPlayerIds.Add("P2");
                ruleset.Steps.Add(secondStep);
            }

            return ruleset;
        }

        private static PersistentDeckState CreateDeck(params string[] configIds)
        {
            var deck = new PersistentDeckState();
            for (int i = 0; i < configIds.Length; i++)
            {
                deck.Cards.Add(new PersistentDeckCard
                {
                    PersistentCardId = $"pc_{i:D2}",
                    BaseConfigId = configIds[i],
                    CurrentConfigId = configIds[i],
                });
            }

            return deck;
        }

        private static CardConfig MakeCardConfig(
            int cardId,
            HeroClass heroClass,
            int rarity,
            string upgradedCardConfigId = "",
            CardTag tags = CardTag.None)
        {
            return new CardConfig
            {
                CardId = cardId,
                CardName = $"card_{cardId}",
                HeroClass = heroClass,
                Rarity = rarity,
                UpgradedCardConfigId = upgradedCardConfigId,
                Tags = tags,
                TrackType = CardTrackType.Plan,
                TargetType = CardTargetType.CurrentEnemy,
            };
        }

        private static InMemoryBuildCatalog CreateCatalog()
        {
            return new InMemoryBuildCatalog()
                .AddCardDefinition(new BuildCardDefinition
                {
                    ConfigId = "slash",
                    ClassId = HeroClass.Warrior,
                    Rarity = BuildCardRarity.Common,
                    CanRemove = true,
                    UpgradedConfigId = "slash_plus",
                    CanAppearInBuildReward = false,
                })
                .AddCardDefinition(new BuildCardDefinition
                {
                    ConfigId = "slash_plus",
                    ClassId = HeroClass.Warrior,
                    Rarity = BuildCardRarity.Common,
                    CanRemove = true,
                    CanAppearInBuildReward = false,
                })
                .AddCardDefinition(new BuildCardDefinition
                {
                    ConfigId = "guard",
                    ClassId = HeroClass.Warrior,
                    Rarity = BuildCardRarity.Common,
                    CanRemove = true,
                    CanAppearInBuildReward = false,
                })
                .AddCardDefinition(new BuildCardDefinition
                {
                    ConfigId = "bolt",
                    ClassId = HeroClass.Warrior,
                    Rarity = BuildCardRarity.Common,
                    CanRemove = true,
                    CanAppearInBuildReward = false,
                })
                .AddCardDefinition(new BuildCardDefinition
                {
                    ConfigId = "cleave",
                    ClassId = HeroClass.Warrior,
                    Rarity = BuildCardRarity.Uncommon,
                    CanRemove = true,
                    UpgradedConfigId = "cleave_plus",
                })
                .AddCardDefinition(new BuildCardDefinition
                {
                    ConfigId = "cleave_plus",
                    ClassId = HeroClass.Warrior,
                    Rarity = BuildCardRarity.Uncommon,
                    CanRemove = true,
                    CanAppearInBuildReward = false,
                })
                .AddCardDefinition(new BuildCardDefinition
                {
                    ConfigId = "bash",
                    ClassId = HeroClass.Warrior,
                    Rarity = BuildCardRarity.Rare,
                    CanRemove = true,
                })
                .AddCardDefinition(new BuildCardDefinition
                {
                    ConfigId = "charge",
                    ClassId = HeroClass.Warrior,
                    Rarity = BuildCardRarity.Common,
                    CanRemove = true,
                })
                .AddCardDefinition(new BuildCardDefinition
                {
                    ConfigId = "legendary_test",
                    ClassId = HeroClass.Warrior,
                    Rarity = BuildCardRarity.Legendary,
                    CanRemove = true,
                })
                .AddPoolCard(DefaultWarriorPoolId, "cleave")
                .AddPoolCard(DefaultWarriorPoolId, "bash")
                .AddPoolCard(DefaultWarriorPoolId, "charge")
                .AddPoolCard(DefaultWarriorPoolId, "legendary_test")
                .AddEquipmentDefinition(new EquipmentDefinition
                {
                    EquipmentId = "burning_blood",
                    ClassId = HeroClass.Warrior,
                    EffectType = EquipmentEffectType.HealAfterBattleFlat,
                    EffectValue = 6,
                });
        }
    }
}
