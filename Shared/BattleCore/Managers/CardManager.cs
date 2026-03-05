#pragma warning disable CS8632

using System;
using System.Collections.Generic;
using System.Linq;
using CardMoba.BattleCore.Foundation;
using CardMoba.BattleCore.Context;
using CardMoba.BattleCore.EventBus;

namespace CardMoba.BattleCore.Managers
{
    /// <summary>
    /// CardManager —— ICardManager 的具体实现。
    ///
    /// 负责 BattleCard 实例的生命周期和区域流转。
    /// 区域移动通过 MoveCard() 统一完成，不在此类以外直接修改 BattleCard.Zone。
    /// 结算逻辑（抽牌触发效果等）通过 EventBus 广播，不在此类内处理。
    /// </summary>
    public class CardManager : ICardManager
    {
        private int _instanceCounter = 0;

        // ══════════════════════════════════════════════════════════
        // 初始化
        // ══════════════════════════════════════════════════════════

        /// <inheritdoc/>
        public void InitBattleDeck(
            BattleContext ctx,
            string playerId,
            List<(string configId, int count)> deckConfig)
        {
            var player = ctx.GetPlayer(playerId);
            if (player == null)
            {
                ctx.RoundLog.Add($"[CardManager] ⚠️ 找不到玩家 {playerId}，无法初始化卡组。");
                return;
            }

            // 清空现有卡牌（防止重复初始化）
            player.AllCards.Clear();

            foreach (var (configId, count) in deckConfig)
            {
                for (int i = 0; i < count; i++)
                {
                    var card = new BattleCard
                    {
                        InstanceId = $"bc_{++_instanceCounter:D4}",
                        ConfigId   = configId,
                        OwnerId    = playerId,
                        Zone       = CardZone.Deck,
                    };
                    player.AllCards.Add(card);
                }
            }

            // 洗牌（使用确定性随机）
            ShuffleDeck(ctx, playerId);

            ctx.RoundLog.Add($"[CardManager] 玩家 {playerId} 初始化卡组完成，共 {player.AllCards.Count} 张牌。");
        }

        // ══════════════════════════════════════════════════════════
        // 抽牌
        // ══════════════════════════════════════════════════════════

        /// <inheritdoc/>
        public List<BattleCard> DrawCards(BattleContext ctx, string playerId, int count)
        {
            var player = ctx.GetPlayer(playerId);
            if (player == null) return new List<BattleCard>();

            var drawn = new List<BattleCard>();
            var deck  = player.GetCardsInZone(CardZone.Deck);

            for (int i = 0; i < count; i++)
            {
                if (deck.Count == 0)
                {
                    // 卡组空时：将弃牌堆重新洗入卡组
                    var discard = player.GetCardsInZone(CardZone.Discard);
                    if (discard.Count == 0)
                    {
                        ctx.RoundLog.Add($"[CardManager] {playerId} 卡组和弃牌堆均为空，无法继续抽牌。");
                        break;
                    }

                    foreach (var d in discard)
                        d.Zone = CardZone.Deck;

                    ShuffleDeck(ctx, playerId);
                    ctx.RoundLog.Add($"[CardManager] {playerId} 卡组已空，弃牌堆 {discard.Count} 张牌洗回卡组。");
                    deck = player.GetCardsInZone(CardZone.Deck);
                }

                // 从卡组顶（索引 0）抽牌
                var card = deck[0];
                MoveCard(ctx, card, CardZone.Hand);
                drawn.Add(card);

                ctx.EventBus.Publish(new CardDrawnEvent
                {
                    PlayerId       = playerId,
                    CardInstanceId = card.InstanceId,
                    CardConfigId   = card.ConfigId,
                });

                ctx.RoundLog.Add($"[CardManager] {playerId} 抽取 [{card.ConfigId}]（InstanceId={card.InstanceId}）。");

                // 刷新 deck 引用（移动后列表已变）
                deck = player.GetCardsInZone(CardZone.Deck);
            }

            return drawn;
        }

        // ══════════════════════════════════════════════════════════
        // 定策牌提交
        // ══════════════════════════════════════════════════════════

        /// <inheritdoc/>
        public bool CommitPlanCard(BattleContext ctx, string cardInstanceId)
        {
            var card = FindCard(ctx, cardInstanceId);
            if (card == null)
            {
                ctx.RoundLog.Add($"[CardManager] ⚠️ 找不到卡牌实例 {cardInstanceId}。");
                return false;
            }

            if (card.Zone != CardZone.Hand)
            {
                ctx.RoundLog.Add($"[CardManager] ⚠️ 卡牌 {cardInstanceId} 不在手牌区（当前区域={card.Zone}），无法提交。");
                return false;
            }

            MoveCard(ctx, card, CardZone.StrategyZone);
            ctx.RoundLog.Add($"[CardManager] {card.OwnerId} 提交定策牌 [{card.ConfigId}]（{cardInstanceId}）。");
            return true;
        }

        // ══════════════════════════════════════════════════════════
        // 区域移动
        // ══════════════════════════════════════════════════════════

        /// <inheritdoc/>
        public void MoveCard(BattleContext ctx, BattleCard card, CardZone targetZone)
        {
            var prevZone = card.Zone;
            card.Zone = targetZone;
            ctx.RoundLog.Add($"[CardManager] 卡牌 [{card.ConfigId}]（{card.InstanceId}）{prevZone} → {targetZone}。");
        }

        // ══════════════════════════════════════════════════════════
        // 状态牌扫描
        // ══════════════════════════════════════════════════════════

        /// <inheritdoc/>
        public void ScanStatCards(BattleContext ctx)
        {
            foreach (var kv in ctx.AllPlayers)
            {
                var player   = kv.Value;
                var statCards = player.GetCardsInZone(CardZone.StatZone);

                foreach (var card in statCards)
                {
                    var trigCtx = new TriggerContext
                    {
                        SourceEntityId = player.HeroEntity.EntityId,
                    };
                    trigCtx.Extra["cardInstanceId"] = card.InstanceId;
                    ctx.TriggerManager.Fire(ctx, TriggerTiming.OnStatCardHeld, trigCtx);
                }
            }
        }

        // ══════════════════════════════════════════════════════════
        // 回合事件
        // ══════════════════════════════════════════════════════════

        /// <inheritdoc/>
        public void OnRoundStart(BattleContext ctx, int round)
        {
            foreach (var kv in ctx.AllPlayers)
            {
                var player = kv.Value;

                // ── 能量回满 ──────────────────────────────────────────
                player.Energy = player.MaxEnergy;
                ctx.RoundLog.Add($"[CardManager] {player.PlayerId} 能量回满：{player.Energy}/{player.MaxEnergy}");

                // ── 每回合固定抽 5 张 ─────────────────────────────────
                DrawCards(ctx, kv.Key, 5);
            }
        }

        /// <inheritdoc/>
        public void OnRoundEnd(BattleContext ctx, int round)
        {
            // ── 回合结束弃手牌：Hand → Discard ───────────────────────
            // StrategyZone（定策区）的牌在结算完成后也一并清理
            // Exhaust 牌已在出牌时从 AllCards 移除，无需处理
            foreach (var kv in ctx.AllPlayers)
            {
                var player = kv.Value;

                // 弃手牌
                var handCards = player.GetCardsInZone(CardZone.Hand);
                foreach (var card in handCards)
                    MoveCard(ctx, card, CardZone.Discard);

                if (handCards.Count > 0)
                    ctx.RoundLog.Add($"[CardManager] {player.PlayerId} 回合结束弃手牌 {handCards.Count} 张。");

                // 清理遗留定策区（正常情况应已空，防御性清理）
                var stratCards = player.GetCardsInZone(CardZone.StrategyZone);
                foreach (var card in stratCards)
                    MoveCard(ctx, card, CardZone.Discard);

                if (stratCards.Count > 0)
                    ctx.RoundLog.Add($"[CardManager] {player.PlayerId} 清理遗留定策区 {stratCards.Count} 张。");
            }
        }

        // ══════════════════════════════════════════════════════════
        // 临时牌生命周期
        // ══════════════════════════════════════════════════════════

        /// <inheritdoc/>
        public void DestroyTempCards(BattleContext ctx)
        {
            foreach (var kv in ctx.AllPlayers)
            {
                var player   = kv.Value;
                var tempCards = player.AllCards.Where(c => c.TempCard).ToList();
                foreach (var card in tempCards)
                {
                    player.AllCards.Remove(card);
                    ctx.RoundLog.Add($"[CardManager] 临时牌 [{card.ConfigId}]（{card.InstanceId}）已销毁。");
                }
            }
        }

        /// <inheritdoc/>
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
                ctx.RoundLog.Add($"[CardManager] ⚠️ 找不到玩家 {ownerId}，无法生成卡牌 {configId}。");
                // 返回一个占位实例（不加入任何玩家）
                return new BattleCard
                {
                    InstanceId = $"bc_{++_instanceCounter:D4}",
                    ConfigId   = configId,
                    OwnerId    = ownerId,
                    Zone       = targetZone,
                    TempCard   = tempCard,
                };
            }

            var card = new BattleCard
            {
                InstanceId = $"bc_{++_instanceCounter:D4}",
                ConfigId   = configId,
                OwnerId    = ownerId,
                Zone       = targetZone,
                TempCard   = tempCard,
            };

            player.AllCards.Add(card);
            ctx.RoundLog.Add($"[CardManager] 为 {ownerId} 生成卡牌 [{configId}]（{card.InstanceId}）→ {targetZone}。");
            return card;
        }

        // ══════════════════════════════════════════════════════════
        // 查询
        // ══════════════════════════════════════════════════════════

        /// <inheritdoc/>
        public BattleCard? GetCard(BattleContext ctx, string instanceId)
        {
            return FindCard(ctx, instanceId);
        }

        // ══════════════════════════════════════════════════════════
        // 工具方法
        // ══════════════════════════════════════════════════════════

        /// <summary>在所有玩家的 AllCards 中按 InstanceId 查找卡牌</summary>
        private BattleCard? FindCard(BattleContext ctx, string instanceId)
        {
            foreach (var kv in ctx.AllPlayers)
            {
                var card = kv.Value.AllCards.Find(c => c.InstanceId == instanceId);
                if (card != null) return card;
            }
            return null;
        }

        /// <summary>对指定玩家的卡组（Deck 区域）进行确定性洗牌（Fisher-Yates）</summary>
        private void ShuffleDeck(BattleContext ctx, string playerId)
        {
            var player = ctx.GetPlayer(playerId);
            if (player == null) return;

            var deck = player.GetCardsInZone(CardZone.Deck);
            // 对 AllCards 中 Deck 区域的牌做 Fisher-Yates 洗牌
            // 找到它们在 AllCards 中的索引并互换
            var deckIndices = new List<int>();
            for (int i = 0; i < player.AllCards.Count; i++)
            {
                if (player.AllCards[i].Zone == CardZone.Deck)
                    deckIndices.Add(i);
            }

            for (int i = deckIndices.Count - 1; i > 0; i--)
            {
                int j = ctx.Random.Next(0, i + 1);
                // 交换 AllCards[deckIndices[i]] 与 AllCards[deckIndices[j]]
                var tmp = player.AllCards[deckIndices[i]];
                player.AllCards[deckIndices[i]] = player.AllCards[deckIndices[j]];
                player.AllCards[deckIndices[j]] = tmp;
            }
        }
    }
}