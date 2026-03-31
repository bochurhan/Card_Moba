using System;
using System.Collections.Generic;
using System.Linq;
using CardMoba.MatchFlow.Catalog;
using CardMoba.MatchFlow.Context;
using CardMoba.MatchFlow.Deck;
using CardMoba.MatchFlow.Rules;

namespace CardMoba.MatchFlow.Core
{
    public sealed class BuildActionApplier
    {
        private readonly IBuildCatalog _catalog;

        public BuildActionApplier(IBuildCatalog catalog)
        {
            _catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
        }

        public void ApplyChoice(
            PlayerMatchState player,
            PlayerBuildWindowState playerWindow,
            BuildOpportunityState opportunity,
            BuildChoice choice)
        {
            if (player == null)
                throw new ArgumentNullException(nameof(player));
            if (playerWindow == null)
                throw new ArgumentNullException(nameof(playerWindow));
            if (opportunity == null)
                throw new ArgumentNullException(nameof(opportunity));
            if (choice == null)
                throw new ArgumentNullException(nameof(choice));
            if (!opportunity.AvailableActions.Contains(choice.ActionType))
                throw new InvalidOperationException($"Build action {choice.ActionType} is not available for player {player.PlayerId} at opportunity {opportunity.OpportunityIndex}.");

            switch (choice.ActionType)
            {
                case BuildActionType.Heal:
                    playerWindow.PreviewHp = Math.Min(playerWindow.MaxHp, playerWindow.PreviewHp + opportunity.Offers.HealAmount);
                    break;

                case BuildActionType.UpgradeCard:
                    ApplyUpgrade(playerWindow, opportunity, choice.TargetPersistentCardId);
                    break;

                case BuildActionType.RemoveCard:
                    ApplyRemove(playerWindow, opportunity, choice.TargetPersistentCardId);
                    break;

                case BuildActionType.AddCard:
                    if (!opportunity.Offers.DraftGroupsRevealed)
                        throw new InvalidOperationException("Add card choices have not been revealed yet.");
                    ApplyAdd(playerWindow, opportunity, choice.SelectedDraftOfferIdsByGroup);
                    break;

                default:
                    throw new InvalidOperationException($"Unsupported build action: {choice.ActionType}.");
            }

            opportunity.Choice = choice;
            opportunity.CommittedActionType = choice.ActionType;
            opportunity.IsResolved = true;
        }

        public void CommitResolvedState(PlayerMatchState player, PlayerBuildWindowState playerWindow)
        {
            if (player == null)
                throw new ArgumentNullException(nameof(player));
            if (playerWindow == null)
                throw new ArgumentNullException(nameof(playerWindow));

            player.PersistentHp = Math.Clamp(playerWindow.PreviewHp, 0, player.MaxHp);
            player.Deck = playerWindow.PreviewDeck.Clone();
            player.BonusBuildPickCount = 0;
            player.IsBuildLocked = false;
            player.WasDefeatedInLastBattle = false;
        }

        private void ApplyUpgrade(PlayerBuildWindowState playerWindow, BuildOpportunityState opportunity, string? targetPersistentCardId)
        {
            if (string.IsNullOrWhiteSpace(targetPersistentCardId))
                throw new InvalidOperationException("Upgrade action requires a target persistent card id.");

            var candidate = opportunity.Offers.UpgradableCards
                .FirstOrDefault(card => string.Equals(card.PersistentCardId, targetPersistentCardId, StringComparison.Ordinal));
            if (candidate == null)
                throw new InvalidOperationException($"Upgrade target not found in available candidates: {targetPersistentCardId}.");

            var card = playerWindow.PreviewDeck.FindCard(targetPersistentCardId);
            if (card == null)
                throw new InvalidOperationException($"Deck card not found for upgrade: {targetPersistentCardId}.");

            var definition = _catalog.GetCardDefinition(card.GetEffectiveConfigId());
            if (definition == null || !definition.CanUpgrade)
                throw new InvalidOperationException($"Card cannot be upgraded: {card.GetEffectiveConfigId()}.");

            card.CurrentConfigId = definition.UpgradedConfigId;
            card.UpgradeLevel++;
        }

        private static void ApplyRemove(PlayerBuildWindowState playerWindow, BuildOpportunityState opportunity, string? targetPersistentCardId)
        {
            if (string.IsNullOrWhiteSpace(targetPersistentCardId))
                throw new InvalidOperationException("Remove action requires a target persistent card id.");

            bool isAvailable = opportunity.Offers.RemovableCards
                .Any(card => string.Equals(card.PersistentCardId, targetPersistentCardId, StringComparison.Ordinal));
            if (!isAvailable)
                throw new InvalidOperationException($"Remove target not found in available candidates: {targetPersistentCardId}.");
            if (!playerWindow.PreviewDeck.RemoveCard(targetPersistentCardId))
                throw new InvalidOperationException($"Deck card not found for removal: {targetPersistentCardId}.");
        }

        private static void ApplyAdd(
            PlayerBuildWindowState playerWindow,
            BuildOpportunityState opportunity,
            IReadOnlyDictionary<int, string?> selectedDraftOfferIdsByGroup)
        {
            foreach (var group in opportunity.Offers.DraftGroups)
            {
                selectedDraftOfferIdsByGroup.TryGetValue(group.GroupIndex, out var selectedOfferId);
                if (string.IsNullOrWhiteSpace(selectedOfferId))
                    continue;

                var offer = group.Offers.FirstOrDefault(item => string.Equals(item.OfferId, selectedOfferId, StringComparison.Ordinal));
                if (offer == null)
                    throw new InvalidOperationException($"Draft offer {selectedOfferId} is not available in group {group.GroupIndex}.");

                playerWindow.PreviewDeck.AddCard(new PersistentDeckCard
                {
                    PersistentCardId = offer.PersistentCardId,
                    BaseConfigId = offer.BaseConfigId,
                    CurrentConfigId = offer.EffectiveConfigId,
                    UpgradeLevel = offer.UpgradeLevel,
                });
            }
        }
    }
}
