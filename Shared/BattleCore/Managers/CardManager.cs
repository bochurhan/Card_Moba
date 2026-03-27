#pragma warning disable CS8632

using System;
using System.Collections.Generic;
using System.Linq;
using CardMoba.BattleCore.Context;
using CardMoba.BattleCore.EventBus;
using CardMoba.BattleCore.Foundation;
using CardMoba.BattleCore.Handlers;

namespace CardMoba.BattleCore.Managers
{
    /// <summary>
    /// 管理 BattleCard 实例的生命周期与区位流转。
    /// 不负责效果结算本身，结算副作用通过 EventBus / TriggerManager 向外广播。
    /// </summary>
    public class CardManager : ICardManager
    {
        private int _instanceCounter;

        public void InitBattleDeck(
            BattleContext ctx,
            string playerId,
            List<(string configId, int count)> deckConfig)
        {
            var player = ctx.GetPlayer(playerId);
            if (player == null)
            {
                ctx.RoundLog.Add($"[CardManager] player {playerId} not found; skip deck init.");
                return;
            }

            player.AllCards.Clear();

            foreach (var (configId, count) in deckConfig)
            {
                for (int i = 0; i < count; i++)
                {
                    var definition = ctx.GetCardDefinition(configId);
                    player.AllCards.Add(new BattleCard
                    {
                        InstanceId = $"bc_{++_instanceCounter:D4}",
                        ConfigId = configId,
                        OwnerId = playerId,
                        Zone = CardZone.Deck,
                        IsExhaust = definition?.IsExhaust ?? false,
                        IsStatCard = definition?.IsStatCard ?? false,
                    });
                }
            }

            ShuffleDeck(ctx, playerId);
            ctx.RoundLog.Add($"[CardManager] deck initialized for {playerId}: {player.AllCards.Count} cards.");
        }

        public List<BattleCard> DrawCards(BattleContext ctx, string playerId, int count)
        {
            var player = ctx.GetPlayer(playerId);
            if (player == null)
                return new List<BattleCard>();

            if (ctx.BuffManager.HasBuffType(ctx, player.HeroEntity.EntityId, Protocol.Enums.BuffType.NoDrawThisTurn))
            {
                ctx.RoundLog.Add($"[CardManager] {playerId} draw blocked by NoDrawThisTurn.");
                return new List<BattleCard>();
            }

            var drawn = new List<BattleCard>();
            var deck = player.GetCardsInZone(CardZone.Deck);

            for (int i = 0; i < count; i++)
            {
                if (deck.Count == 0)
                {
                    var discard = player.GetCardsInZone(CardZone.Discard);
                    if (discard.Count == 0)
                    {
                        ctx.RoundLog.Add($"[CardManager] {playerId} has no cards left to draw.");
                        break;
                    }

                    foreach (var card in discard)
                        card.Zone = CardZone.Deck;

                    ShuffleDeck(ctx, playerId);
                    ctx.RoundLog.Add($"[CardManager] reshuffled {discard.Count} discard cards back into deck for {playerId}.");
                    deck = player.GetCardsInZone(CardZone.Deck);
                }

                var cardToDraw = deck[0];
                MoveCard(ctx, cardToDraw, CardZone.Hand);
                drawn.Add(cardToDraw);

                string effectiveConfigId = cardToDraw.GetEffectiveConfigId();
                ctx.EventBus.Publish(new CardDrawnEvent
                {
                    PlayerId = playerId,
                    CardInstanceId = cardToDraw.InstanceId,
                    CardConfigId = effectiveConfigId,
                });
                ctx.TriggerManager.Fire(ctx, TriggerTiming.OnCardDrawn, new TriggerContext
                {
                    SourceEntityId = player.HeroEntity.EntityId,
                    Extra = new Dictionary<string, object>
                    {
                        ["playerId"] = playerId,
                        ["cardInstanceId"] = cardToDraw.InstanceId,
                        ["cardConfigId"] = effectiveConfigId,
                        ["cardBaseConfigId"] = cardToDraw.ConfigId,
                    },
                });

                ctx.RoundLog.Add($"[CardManager] {playerId} drew [{effectiveConfigId}] ({cardToDraw.InstanceId}).");
                deck = player.GetCardsInZone(CardZone.Deck);
            }

            return drawn;
        }

        public bool CommitPlanCard(BattleContext ctx, string cardInstanceId)
        {
            var card = FindCard(ctx, cardInstanceId);
            if (card == null)
            {
                ctx.RoundLog.Add($"[CardManager] plan card {cardInstanceId} not found.");
                return false;
            }

            if (card.Zone != CardZone.Hand)
            {
                ctx.RoundLog.Add($"[CardManager] plan card {cardInstanceId} is not in hand (zone={card.Zone}).");
                return false;
            }

            var definition = ctx.GetEffectiveCardDefinition(card);
            bool isStatCard = definition?.IsStatCard ?? card.IsStatCard;
            if (isStatCard)
            {
                ctx.RoundLog.Add($"[CardManager] status card {cardInstanceId} cannot be committed as a plan card.");
                return false;
            }

            var targetZone = ResolvePostPlayZone(ctx, card);
            MoveCard(ctx, card, targetZone);
            ctx.RoundLog.Add(
                $"[CardManager] {card.OwnerId} committed [{card.GetEffectiveConfigId()}] ({card.InstanceId}); snapshot queued, card moved to {targetZone}.");
            return true;
        }

        public void MoveCard(BattleContext ctx, BattleCard card, CardZone targetZone)
        {
            var previousZone = card.Zone;
            card.Zone = targetZone;
            ctx.RoundLog.Add($"[CardManager] [{card.GetEffectiveConfigId()}] ({card.InstanceId}) {previousZone} -> {targetZone}.");
        }

        public bool MoveCardToTopOfDeck(BattleContext ctx, BattleCard card)
        {
            var player = ctx.GetPlayer(card.OwnerId);
            if (player == null)
            {
                ctx.RoundLog.Add($"[CardManager] cannot move {card.InstanceId} to deck top; player {card.OwnerId} not found.");
                return false;
            }

            int existingIndex = player.AllCards.IndexOf(card);
            if (existingIndex < 0)
            {
                ctx.RoundLog.Add($"[CardManager] cannot move {card.InstanceId} to deck top; card not owned by {card.OwnerId}.");
                return false;
            }

            var previousZone = card.Zone;
            player.AllCards.RemoveAt(existingIndex);
            card.Zone = CardZone.Deck;

            int firstDeckIndex = player.AllCards.FindIndex(c => c.Zone == CardZone.Deck);
            if (firstDeckIndex < 0)
                player.AllCards.Add(card);
            else
                player.AllCards.Insert(firstDeckIndex, card);

            ctx.RoundLog.Add($"[CardManager] [{card.GetEffectiveConfigId()}] ({card.InstanceId}) {previousZone} -> Deck(top).");
            return true;
        }

        public void ScanStatCards(BattleContext ctx)
        {
            foreach (var kv in ctx.AllPlayers)
            {
                var player = kv.Value;
                foreach (var card in player.GetCardsInZone(CardZone.Hand))
                {
                    var definition = ctx.GetEffectiveCardDefinition(card);
                    bool isStatCard = definition?.IsStatCard ?? card.IsStatCard;
                    if (!isStatCard)
                        continue;

                    var triggerContext = new TriggerContext
                    {
                        SourceEntityId = player.HeroEntity.EntityId,
                    };
                    triggerContext.Extra["cardInstanceId"] = card.InstanceId;
                    triggerContext.Extra["cardConfigId"] = card.GetEffectiveConfigId();
                    triggerContext.Extra["cardBaseConfigId"] = card.ConfigId;
                    ctx.TriggerManager.Fire(ctx, TriggerTiming.OnStatCardHeld, triggerContext);
                }
            }
        }

        public void OnRoundStart(BattleContext ctx, int round)
        {
            foreach (var kv in ctx.AllPlayers)
            {
                var player = kv.Value;
                player.Energy = player.MaxEnergy;
                ctx.RoundLog.Add($"[CardManager] {player.PlayerId} energy reset to {player.Energy}/{player.MaxEnergy}.");
                DrawCards(ctx, kv.Key, 5);
            }
        }

        public void OnRoundEnd(BattleContext ctx, int round)
        {
            foreach (var kv in ctx.AllPlayers)
            {
                var player = kv.Value;

                var handCards = player.GetCardsInZone(CardZone.Hand);
                foreach (var card in handCards)
                    MoveCard(ctx, card, CardZone.Discard);

                if (handCards.Count > 0)
                    ctx.RoundLog.Add($"[CardManager] {player.PlayerId} discarded {handCards.Count} hand card(s) at round end.");

                ClearEndOfTurnProjections(ctx, player);
                ResolveEndRoundReturnToHand(ctx, player, round);
            }
        }

        public void DestroyTempCards(BattleContext ctx)
        {
            foreach (var kv in ctx.AllPlayers)
            {
                var player = kv.Value;
                var tempCards = player.AllCards.Where(c => c.TempCard).ToList();
                foreach (var card in tempCards)
                {
                    player.AllCards.Remove(card);
                    ctx.RoundLog.Add($"[CardManager] temp card [{card.ConfigId}] ({card.InstanceId}) destroyed.");
                }
            }
        }

        public BattleCard GenerateCard(
            BattleContext ctx,
            string ownerId,
            string configId,
            CardZone targetZone,
            bool tempCard = true)
        {
            var player = ctx.GetPlayer(ownerId);
            if (player == null)
            {
                ctx.RoundLog.Add($"[CardManager] cannot generate card {configId}; player {ownerId} not found.");
                return new BattleCard
                {
                    InstanceId = $"bc_{++_instanceCounter:D4}",
                    ConfigId = configId,
                    OwnerId = ownerId,
                    Zone = targetZone,
                    TempCard = tempCard,
                };
            }

            var definition = ctx.GetCardDefinition(configId);
            var generatedCard = new BattleCard
            {
                InstanceId = $"bc_{++_instanceCounter:D4}",
                ConfigId = configId,
                OwnerId = ownerId,
                Zone = targetZone,
                TempCard = tempCard,
                IsExhaust = definition?.IsExhaust ?? false,
                IsStatCard = definition?.IsStatCard ?? false,
            };

            player.AllCards.Add(generatedCard);
            ctx.RoundLog.Add($"[CardManager] generated [{configId}] ({generatedCard.InstanceId}) for {ownerId} -> {targetZone}, temp={tempCard}.");
            return generatedCard;
        }

        public BattleCard? GetCard(BattleContext ctx, string instanceId)
        {
            return FindCard(ctx, instanceId);
        }

        public BattleCard? PrepareInstantCard(BattleContext ctx, string playerId, string cardInstanceId)
        {
            var card = FindCard(ctx, cardInstanceId);
            if (card == null)
            {
                ctx.RoundLog.Add($"[CardManager] instant card {cardInstanceId} not found.");
                return null;
            }

            if (!card.OwnerId.Equals(playerId, StringComparison.Ordinal))
            {
                ctx.RoundLog.Add($"[CardManager] instant card {cardInstanceId} does not belong to {playerId}.");
                return null;
            }

            if (card.Zone != CardZone.Hand)
            {
                ctx.RoundLog.Add($"[CardManager] instant card {cardInstanceId} is not in hand (zone={card.Zone}).");
                return null;
            }

            var definition = ctx.GetEffectiveCardDefinition(card);
            bool isStatCard = definition?.IsStatCard ?? card.IsStatCard;
            if (isStatCard)
            {
                ctx.RoundLog.Add($"[CardManager] status card {cardInstanceId} cannot be played directly.");
                return null;
            }

            MoveCard(ctx, card, ResolvePostPlayZone(ctx, card));
            ctx.RoundLog.Add($"[CardManager] {playerId} played instant [{card.GetEffectiveConfigId()}] ({card.InstanceId}).");
            return card;
        }

        private void ResolveEndRoundReturnToHand(BattleContext ctx, PlayerData player, int round)
        {
            var markedCards = player.AllCards
                .Where(card => card.ExtraData.TryGetValue(ReturnSourceCardToHandAtRoundEndHandler.ReturnToHandMarkedRoundKey, out var markedRound)
                    && markedRound is int intRound
                    && intRound == round)
                .ToList();

            foreach (var card in markedCards)
            {
                bool shouldReturn = card.ExtraData.TryGetValue(ReturnSourceCardToHandAtRoundEndHandler.ReturnToHandAtRoundEndKey, out var flag)
                    && flag is bool returnMarked
                    && returnMarked;

                if (shouldReturn && card.Zone == CardZone.Discard)
                {
                    MoveCard(ctx, card, CardZone.Hand);
                    ctx.RoundLog.Add($"[CardManager] [{card.GetEffectiveConfigId()}] ({card.InstanceId}) returned to hand at round end.");
                }

                card.ExtraData.Remove(ReturnSourceCardToHandAtRoundEndHandler.ReturnToHandAtRoundEndKey);
                card.ExtraData.Remove(ReturnSourceCardToHandAtRoundEndHandler.ReturnToHandMarkedRoundKey);
            }
        }

        private static CardZone ResolvePostPlayZone(BattleContext ctx, BattleCard card)
        {
            if (card.ExtraData.TryGetValue("forceConsumeAfterResolve", out var forceConsume)
                && forceConsume is bool consume
                && consume)
            {
                return CardZone.Consume;
            }

            var definition = ctx.GetEffectiveCardDefinition(card);
            bool isExhaust = definition?.IsExhaust ?? card.IsExhaust;
            return isExhaust ? CardZone.Consume : CardZone.Discard;
        }

        private static void ClearEndOfTurnProjections(BattleContext ctx, PlayerData player)
        {
            foreach (var card in player.AllCards)
            {
                if (card.ProjectionLifetime != CardProjectionLifetime.EndOfTurn)
                    continue;

                ctx.RoundLog.Add($"[CardManager] clear EndOfTurn projection for [{card.ConfigId}] ({card.InstanceId}).");
                card.ClearProjection();
            }
        }

        private BattleCard? FindCard(BattleContext ctx, string instanceId)
        {
            foreach (var kv in ctx.AllPlayers)
            {
                var card = kv.Value.AllCards.Find(c => c.InstanceId == instanceId);
                if (card != null)
                    return card;
            }

            return null;
        }

        private void ShuffleDeck(BattleContext ctx, string playerId)
        {
            var player = ctx.GetPlayer(playerId);
            if (player == null)
                return;

            var deckIndices = new List<int>();
            for (int i = 0; i < player.AllCards.Count; i++)
            {
                if (player.AllCards[i].Zone == CardZone.Deck)
                    deckIndices.Add(i);
            }

            for (int i = deckIndices.Count - 1; i > 0; i--)
            {
                int j = ctx.Random.Next(0, i + 1);
                var temp = player.AllCards[deckIndices[i]];
                player.AllCards[deckIndices[i]] = player.AllCards[deckIndices[j]];
                player.AllCards[deckIndices[j]] = temp;
            }
        }
    }
}
