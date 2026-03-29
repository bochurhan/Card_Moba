using System;
using System.Collections.Generic;
using System.Linq;
using CardMoba.BattleCore.Random;
using CardMoba.MatchFlow.Catalog;
using CardMoba.MatchFlow.Context;
using CardMoba.MatchFlow.Definitions;
using CardMoba.MatchFlow.Rules;
using CardMoba.Protocol.Enums;

namespace CardMoba.MatchFlow.Core
{
    public sealed class BuildOfferGenerator
    {
        private readonly IBuildCatalog _catalog;

        public BuildOfferGenerator(IBuildCatalog catalog)
        {
            _catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
        }

        public BuildWindowState CreateBuildWindow(MatchContext context, long nowUnixMs)
        {
            if (context == null)
                throw new ArgumentNullException(nameof(context));

            var buildWindow = new BuildWindowState
            {
                StepIndex = context.CurrentStepIndex,
                OpenAtUnixMs = nowUnixMs,
                DeadlineUnixMs = nowUnixMs + context.Ruleset.BuildWindowTimeoutMs,
            };

            foreach (var player in context.Players.Values)
            {
                bool forcedRecovery = player.WasDefeatedInLastBattle;
                int opportunityCount = forcedRecovery
                    ? 1
                    : Math.Max(1, context.Ruleset.BuildWindowRules.BaseOpportunityCount + player.BonusBuildPickCount);
                float healPercent = forcedRecovery
                    ? context.Ruleset.BuildWindowRules.ForcedRecoveryHealPercent
                    : context.Ruleset.BuildWindowRules.DefaultHealPercent;

                var playerWindow = new PlayerBuildWindowState
                {
                    PlayerId = player.PlayerId,
                    OpportunityCount = opportunityCount,
                    NextOpportunityIndex = 0,
                    PreviewHp = Math.Clamp(player.PersistentHp, 0, player.MaxHp),
                    MaxHp = player.MaxHp,
                    HealPercent = healPercent,
                    RestrictionMode = forcedRecovery ? BuildWindowRestrictionMode.ForcedRecovery : BuildWindowRestrictionMode.None,
                    PreviewDeck = player.Deck.Clone(),
                };

                playerWindow.Opportunities.Add(CreateOpportunity(context, player, playerWindow, 0));
                buildWindow.PlayerWindows[player.PlayerId] = playerWindow;
            }

            return buildWindow;
        }

        public BuildOpportunityState CreateOpportunity(
            MatchContext context,
            PlayerMatchState player,
            PlayerBuildWindowState playerWindow,
            int opportunityIndex)
        {
            if (context == null)
                throw new ArgumentNullException(nameof(context));
            if (player == null)
                throw new ArgumentNullException(nameof(player));
            if (playerWindow == null)
                throw new ArgumentNullException(nameof(playerWindow));

            var opportunity = new BuildOpportunityState
            {
                OpportunityIndex = opportunityIndex,
            };
            opportunity.Offers.HealAmount = CalculateHealAmount(playerWindow.MaxHp, playerWindow.HealPercent);
            opportunity.AvailableActions.Add(BuildActionType.Heal);

            if (playerWindow.RestrictionMode == BuildWindowRestrictionMode.ForcedRecovery)
                return opportunity;

            foreach (var candidate in BuildUpgradeCandidates(playerWindow))
                opportunity.Offers.UpgradableCards.Add(candidate);
            if (opportunity.Offers.UpgradableCards.Count > 0)
                opportunity.AvailableActions.Add(BuildActionType.UpgradeCard);

            foreach (var candidate in BuildRemoveCandidates(playerWindow))
                opportunity.Offers.RemovableCards.Add(candidate);
            if (opportunity.Offers.RemovableCards.Count > 0)
                opportunity.AvailableActions.Add(BuildActionType.RemoveCard);

            foreach (var group in BuildDraftGroups(context, player, opportunityIndex))
                opportunity.Offers.DraftGroups.Add(group);
            if (opportunity.Offers.DraftGroups.Count > 0)
                opportunity.AvailableActions.Add(BuildActionType.AddCard);

            return opportunity;
        }

        private IEnumerable<BuildCardCandidate> BuildUpgradeCandidates(PlayerBuildWindowState playerWindow)
        {
            foreach (var card in playerWindow.PreviewDeck.Cards)
            {
                var definition = _catalog.GetCardDefinition(card.GetEffectiveConfigId());
                if (definition == null || !definition.CanUpgrade)
                    continue;

                yield return ToCandidate(card);
            }
        }

        private IEnumerable<BuildCardCandidate> BuildRemoveCandidates(PlayerBuildWindowState playerWindow)
        {
            foreach (var card in playerWindow.PreviewDeck.Cards)
            {
                var definition = _catalog.GetCardDefinition(card.GetEffectiveConfigId());
                if (definition == null || !definition.CanRemove)
                    continue;

                yield return ToCandidate(card);
            }
        }

        private IEnumerable<BuildDraftGroup> BuildDraftGroups(MatchContext context, PlayerMatchState player, int opportunityIndex)
        {
            string? poolId = context.Ruleset.GetStepOrThrow(context.CurrentStepIndex).BuildPoolId
                ?? player.Loadout.DefaultBuildPoolId;
            var poolCards = _catalog.GetDraftPoolCards(poolId, player.Loadout.ClassId)
                .Where(card => card.CanAppearInBuildReward && !card.IsLegendary)
                .ToList();
            if (poolCards.Count == 0)
                yield break;

            var rules = context.Ruleset.BuildWindowRules;
            for (int groupIndex = 0; groupIndex < rules.DraftGroupCount; groupIndex++)
            {
                var random = new SeededRandom(ComputeDeterministicSeed(context, player.PlayerId, opportunityIndex, groupIndex));
                var group = new BuildDraftGroup { GroupIndex = groupIndex };
                var chosenConfigIds = new HashSet<string>(StringComparer.Ordinal);

                while (group.Offers.Count < rules.DraftOptionsPerGroup)
                {
                    var definition = PickDraftCardDefinition(poolCards, chosenConfigIds, random, rules);
                    if (definition == null)
                        break;

                    chosenConfigIds.Add(definition.ConfigId);
                    bool isUpgraded = definition.CanUpgrade && random.Chance(rules.UpgradedDraftChance);
                    group.Offers.Add(new BuildDraftCardOffer
                    {
                        OfferId = $"{player.PlayerId}_step{context.CurrentStepIndex}_op{opportunityIndex}_g{groupIndex}_slot{group.Offers.Count}",
                        PersistentCardId = $"draft_{player.PlayerId}_{context.CurrentStepIndex}_{opportunityIndex}_{groupIndex}_{group.Offers.Count}_{definition.ConfigId}",
                        BaseConfigId = definition.ConfigId,
                        EffectiveConfigId = isUpgraded ? definition.UpgradedConfigId : definition.ConfigId,
                        UpgradeLevel = isUpgraded ? 1 : 0,
                        Rarity = definition.Rarity,
                        IsUpgraded = isUpgraded,
                    });
                }

                if (group.Offers.Count > 0)
                    yield return group;
            }
        }

        private static int CalculateHealAmount(int maxHp, float percent)
        {
            return Math.Max(1, (int)Math.Ceiling(maxHp * percent));
        }

        private static BuildCardCandidate ToCandidate(MatchFlow.Deck.PersistentDeckCard card)
        {
            return new BuildCardCandidate
            {
                PersistentCardId = card.PersistentCardId,
                BaseConfigId = card.BaseConfigId,
                EffectiveConfigId = card.GetEffectiveConfigId(),
                UpgradeLevel = card.UpgradeLevel,
            };
        }

        private static BuildCardDefinition? PickDraftCardDefinition(
            List<BuildCardDefinition> candidates,
            HashSet<string> chosenConfigIds,
            SeededRandom random,
            BuildWindowRules rules)
        {
            var available = candidates
                .Where(card => !chosenConfigIds.Contains(card.ConfigId))
                .ToList();
            if (available.Count == 0)
                return null;

            var commonCards = available.Where(card => card.Rarity == BuildCardRarity.Common).ToList();
            var uncommonCards = available.Where(card => card.Rarity == BuildCardRarity.Uncommon).ToList();
            var rareCards = available.Where(card => card.Rarity == BuildCardRarity.Rare).ToList();

            var rarityBuckets = new List<List<BuildCardDefinition>> { commonCards, uncommonCards, rareCards };
            var weights = new[]
            {
                commonCards.Count > 0 ? rules.CommonWeight : 0,
                uncommonCards.Count > 0 ? rules.UncommonWeight : 0,
                rareCards.Count > 0 ? rules.RareWeight : 0,
            };

            int pickedIndex = random.WeightedPick(weights);
            if (pickedIndex < 0)
                return available[random.Next(available.Count)];

            var bucket = rarityBuckets[pickedIndex];
            return bucket[random.Next(bucket.Count)];
        }

        private static int ComputeDeterministicSeed(MatchContext context, string playerId, int opportunityIndex, int groupIndex)
        {
            unchecked
            {
                int hash = 17;
                foreach (char c in context.MatchId)
                    hash = hash * 31 + c;
                foreach (char c in playerId)
                    hash = hash * 31 + c;
                hash = hash * 31 + context.BaseRandomSeed;
                hash = hash * 31 + context.CurrentStepIndex;
                hash = hash * 31 + opportunityIndex;
                hash = hash * 31 + groupIndex;
                return hash;
            }
        }
    }
}
