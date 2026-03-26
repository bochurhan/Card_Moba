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
    /// 管理 BattleCard 实例的生命周期和区位流转。
    /// 不负责卡牌结算本身，结算副作用通过 EventBus / TriggerManager 向外广播。
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
                ctx.RoundLog.Add($"[CardManager] 找不到玩家 {playerId}，无法初始化牌组。");
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
            ctx.RoundLog.Add($"[CardManager] 玩家 {playerId} 初始化牌组完成，共 {player.AllCards.Count} 张牌。");
        }

        public List<BattleCard> DrawCards(BattleContext ctx, string playerId, int count)
        {
            var player = ctx.GetPlayer(playerId);
            if (player == null)
                return new List<BattleCard>();

            if (ctx.BuffManager.HasBuffType(ctx, player.HeroEntity.EntityId, Protocol.Enums.BuffType.NoDrawThisTurn))
            {
                ctx.RoundLog.Add($"[CardManager] {playerId} 处于 NoDrawThisTurn，抽牌被跳过。");
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
                        ctx.RoundLog.Add($"[CardManager] {playerId} 的牌库和弃牌堆都为空，无法继续抽牌。");
                        break;
                    }

                    foreach (var card in discard)
                        card.Zone = CardZone.Deck;

                    ShuffleDeck(ctx, playerId);
                    ctx.RoundLog.Add($"[CardManager] {playerId} 牌库已空，将 {discard.Count} 张弃牌洗回牌库。");
                    deck = player.GetCardsInZone(CardZone.Deck);
                }

                var cardToDraw = deck[0];
                MoveCard(ctx, cardToDraw, CardZone.Hand);
                drawn.Add(cardToDraw);

                ctx.EventBus.Publish(new CardDrawnEvent
                {
                    PlayerId = playerId,
                    CardInstanceId = cardToDraw.InstanceId,
                    CardConfigId = cardToDraw.ConfigId,
                });
                ctx.TriggerManager.Fire(ctx, TriggerTiming.OnCardDrawn, new TriggerContext
                {
                    SourceEntityId = player.HeroEntity.EntityId,
                    Extra = new Dictionary<string, object>
                    {
                        ["playerId"] = playerId,
                        ["cardInstanceId"] = cardToDraw.InstanceId,
                        ["cardConfigId"] = cardToDraw.ConfigId,
                    },
                });

                ctx.RoundLog.Add($"[CardManager] {playerId} 抽取 [{cardToDraw.ConfigId}]（{cardToDraw.InstanceId}）。");
                deck = player.GetCardsInZone(CardZone.Deck);
            }

            return drawn;
        }

        public bool CommitPlanCard(BattleContext ctx, string cardInstanceId)
        {
            var card = FindCard(ctx, cardInstanceId);
            if (card == null)
            {
                ctx.RoundLog.Add($"[CardManager] 找不到卡牌实例 {cardInstanceId}。");
                return false;
            }

            if (card.Zone != CardZone.Hand)
            {
                ctx.RoundLog.Add($"[CardManager] 卡牌 {cardInstanceId} 不在手牌区（当前={card.Zone}），无法提交。");
                return false;
            }

            if (card.IsStatCard)
            {
                ctx.RoundLog.Add($"[CardManager] 状态牌 {cardInstanceId} 不能作为定策牌提交。");
                return false;
            }

            MoveCard(ctx, card, CardZone.StrategyZone);
            ctx.RoundLog.Add($"[CardManager] {card.OwnerId} 提交定策牌 [{card.ConfigId}]（{cardInstanceId}）。");
            return true;
        }

        public void MoveCard(BattleContext ctx, BattleCard card, CardZone targetZone)
        {
            var previousZone = card.Zone;
            card.Zone = targetZone;
            ctx.RoundLog.Add($"[CardManager] 卡牌 [{card.ConfigId}]（{card.InstanceId}）{previousZone} -> {targetZone}。");
        }

        public void ScanStatCards(BattleContext ctx)
        {
            foreach (var kv in ctx.AllPlayers)
            {
                var player = kv.Value;
                var statCards = player.GetCardsInZone(CardZone.Hand).Where(c => c.IsStatCard).ToList();
                foreach (var card in statCards)
                {
                    var triggerContext = new TriggerContext
                    {
                        SourceEntityId = player.HeroEntity.EntityId,
                    };
                    triggerContext.Extra["cardInstanceId"] = card.InstanceId;
                    triggerContext.Extra["cardConfigId"] = card.ConfigId;
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
                ctx.RoundLog.Add($"[CardManager] {player.PlayerId} 能量回满：{player.Energy}/{player.MaxEnergy}");
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
                    ctx.RoundLog.Add($"[CardManager] {player.PlayerId} 回合结束弃手牌 {handCards.Count} 张。");

                var strategyCards = player.GetCardsInZone(CardZone.StrategyZone);
                foreach (var card in strategyCards)
                    MoveCard(ctx, card, CardZone.Discard);

                if (strategyCards.Count > 0)
                    ctx.RoundLog.Add($"[CardManager] {player.PlayerId} 清理遗留定策区 {strategyCards.Count} 张。");

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
                    ctx.RoundLog.Add($"[CardManager] 临时牌 [{card.ConfigId}]（{card.InstanceId}）已销毁。");
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
                ctx.RoundLog.Add($"[CardManager] 找不到玩家 {ownerId}，无法生成卡牌 {configId}。");
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
            ctx.RoundLog.Add($"[CardManager] 为 {ownerId} 生成卡牌 [{configId}]（{generatedCard.InstanceId}）-> {targetZone}，temp={tempCard}。");
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
                ctx.RoundLog.Add($"[CardManager] 找不到瞬策牌实例 {cardInstanceId}。");
                return null;
            }

            if (!card.OwnerId.Equals(playerId, StringComparison.Ordinal))
            {
                ctx.RoundLog.Add($"[CardManager] 瞬策牌 {cardInstanceId} 不属于玩家 {playerId}。");
                return null;
            }

            if (card.Zone != CardZone.Hand)
            {
                ctx.RoundLog.Add($"[CardManager] 瞬策牌 {cardInstanceId} 不在手牌区（当前={card.Zone}）。");
                return null;
            }

            if (card.IsStatCard)
            {
                ctx.RoundLog.Add($"[CardManager] 状态牌 {cardInstanceId} 不能直接打出。");
                return null;
            }

            MoveCard(ctx, card, card.IsExhaust ? CardZone.Consume : CardZone.Discard);
            ctx.RoundLog.Add($"[CardManager] {playerId} 打出瞬策牌 [{card.ConfigId}]（{cardInstanceId}）。");
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
                    ctx.RoundLog.Add($"[CardManager] [{card.ConfigId}]（{card.InstanceId}）在回合结束返回手牌。");
                }

                card.ExtraData.Remove(ReturnSourceCardToHandAtRoundEndHandler.ReturnToHandAtRoundEndKey);
                card.ExtraData.Remove(ReturnSourceCardToHandAtRoundEndHandler.ReturnToHandMarkedRoundKey);
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
