#pragma warning disable CS8632

using System;
using System.Collections.Generic;
using CardMoba.BattleCore.Context;
using CardMoba.BattleCore.EventBus;
using CardMoba.BattleCore.Foundation;
using CardMoba.BattleCore.Managers;

namespace CardMoba.BattleCore.Core
{
    public class CommittedPlanCard
    {
        public string PlayerId { get; set; } = string.Empty;
        public string CardInstanceId { get; set; } = string.Empty;
        public int SubmitOrder { get; set; }
        public List<EffectUnit> Effects { get; set; } = new List<EffectUnit>();
        public bool IsCountered { get; set; }
    }

    public class RoundManager
    {
        private readonly SettlementEngine _settlement;
        private readonly List<CommittedPlanCard> _pendingPlanCards = new List<CommittedPlanCard>();

        public int CurrentRound { get; private set; }
        public bool IsBattleOver { get; private set; }
        public string? WinnerId { get; private set; }

        public RoundManager()
        {
            _settlement = new SettlementEngine();
        }

        public void InitBattle(BattleContext ctx)
        {
            CurrentRound = 0;
            IsBattleOver = false;
            WinnerId = null;
            _pendingPlanCards.Clear();

            ctx.RoundLog.Add("[RoundManager] 战斗初始化完成。");
            ctx.EventBus.Publish(new BattleStartEvent
            {
                BattleId = ctx.BattleId,
                Round = 0,
            });
        }

        public void BeginRound(BattleContext ctx)
        {
            if (IsBattleOver) return;

            CurrentRound++;
            _pendingPlanCards.Clear();

            ctx.CurrentRound = CurrentRound;
            ctx.CurrentPhase = BattleContext.BattlePhase.RoundStart;

            ctx.RoundLog.Add($"[RoundManager] 第 {CurrentRound} 回合开始。");
            ctx.EventBus.Publish(new RoundStartEvent { Round = CurrentRound });

            ctx.TriggerManager.Fire(ctx, TriggerTiming.OnRoundStart, new TriggerContext
            {
                Round = CurrentRound,
            });
            _settlement.DrainPendingQueue(ctx);

            ctx.CardManager.OnRoundStart(ctx, CurrentRound);
            _settlement.DrainPendingQueue(ctx);

            ctx.CurrentPhase = BattleContext.BattlePhase.PlayerAction;
            ctx.RoundLog.Add($"[RoundManager] 第 {CurrentRound} 回合开始处理完毕。");
        }

        public List<EffectResult> PlayInstantCard(
            BattleContext ctx,
            string playerId,
            string cardInstanceId)
        {
            if (IsBattleOver) return new List<EffectResult>();

            var card = ctx.CardManager.GetCard(ctx, cardInstanceId);
            if (card == null)
            {
                ctx.RoundLog.Add($"[RoundManager] ⚠️ 找不到瞬策牌实例 {cardInstanceId}.");
                return new List<EffectResult>();
            }

            if (!card.OwnerId.Equals(playerId, StringComparison.Ordinal))
            {
                ctx.RoundLog.Add($"[RoundManager] ⚠️ 瞬策牌 {cardInstanceId} 不属于玩家 {playerId}。");
                return new List<EffectResult>();
            }

            var effects = ResolveCardEffects(ctx, card.ConfigId, null);
            if (effects == null)
            {
                ctx.RoundLog.Add($"[RoundManager] ⚠️ 找不到瞬策牌 {cardInstanceId}（{card.ConfigId}）的卡牌定义。");
                return new List<EffectResult>();
            }

            StampCardSourceMetadata(effects, card);
            ctx.RoundLog.Add($"[RoundManager] 玩家 {playerId} 打出瞬策牌 {cardInstanceId}。");

            var results = _settlement.ResolveInstantFromCard(ctx, playerId, card, effects);
            CheckDeathAndBattleOver(ctx);
            return results;
        }

        public List<EffectResult> PlayInstantCard(
            BattleContext ctx,
            string playerId,
            string cardInstanceId,
            List<EffectUnit> effects)
        {
            if (IsBattleOver) return new List<EffectResult>();

            var card = ctx.CardManager.GetCard(ctx, cardInstanceId);
            if (card != null)
            {
                if (!card.OwnerId.Equals(playerId, StringComparison.Ordinal))
                {
                    ctx.RoundLog.Add($"[RoundManager] ⚠️ 瞬策牌 {cardInstanceId} 不属于玩家 {playerId}。");
                    return new List<EffectResult>();
                }

                return PlayInstantCard(ctx, playerId, cardInstanceId);
            }

            var clonedEffects = EffectUnitCloner.CloneMany(effects);
            StampCardSourceMetadata(clonedEffects, cardInstanceId, string.Empty);
            ctx.RoundLog.Add($"[RoundManager] 玩家 {playerId} 通过兼容路径打出瞬策牌 {cardInstanceId}。");

            var results = _settlement.ResolveInstant(ctx, playerId, cardInstanceId, clonedEffects);
            CheckDeathAndBattleOver(ctx);
            return results;
        }

        public bool CommitPlanCard(BattleContext ctx, CommittedPlanCard planCard)
        {
            if (IsBattleOver) return false;

            var card = ctx.CardManager.GetCard(ctx, planCard.CardInstanceId);
            if (card == null)
            {
                ctx.RoundLog.Add($"[RoundManager] ⚠️ 找不到定策牌实例 {planCard.CardInstanceId}.");
                return false;
            }

            if (!card.OwnerId.Equals(planCard.PlayerId, StringComparison.Ordinal))
            {
                ctx.RoundLog.Add($"[RoundManager] ⚠️ 定策牌 {planCard.CardInstanceId} 不属于玩家 {planCard.PlayerId}。");
                return false;
            }

            var resolvedEffects = ResolveCardEffects(ctx, card.ConfigId, planCard.Effects);
            if (resolvedEffects == null)
            {
                ctx.RoundLog.Add($"[RoundManager] ⚠️ 找不到定策牌 {planCard.CardInstanceId}（{card.ConfigId}）的卡牌定义。");
                return false;
            }

            StampCardSourceMetadata(resolvedEffects, card);

            if (!ctx.CardManager.CommitPlanCard(ctx, planCard.CardInstanceId))
            {
                ctx.RoundLog.Add($"[RoundManager] ⚠️ 定策牌 {planCard.CardInstanceId} 校验失败，拒绝提交。");
                return false;
            }

            planCard.Effects = resolvedEffects;
            planCard.SubmitOrder = _pendingPlanCards.Count;
            _pendingPlanCards.Add(planCard);

            ctx.RoundLog.Add($"[RoundManager] 玩家 {planCard.PlayerId} 提交定策牌 {planCard.CardInstanceId}（顺序={planCard.SubmitOrder}）。");
            return true;
        }

        public void EndRound(BattleContext ctx)
        {
            if (IsBattleOver) return;

            ctx.RoundLog.Add($"[RoundManager] 第 {CurrentRound} 回合结算开始。");
            ctx.CurrentPhase = BattleContext.BattlePhase.Settlement;

            ctx.CardManager.ScanStatCards(ctx);
            _settlement.DrainPendingQueue(ctx);
            if (CheckDeathAndBattleOver(ctx)) return;

            if (_pendingPlanCards.Count > 0)
            {
                _settlement.ResolvePlanCards(ctx, _pendingPlanCards);
            }
            else
            {
                ctx.RoundLog.Add("[RoundManager] 本回合无定策牌，跳过定策结算。");
            }
            _pendingPlanCards.Clear();

            if (CheckDeathAndBattleOver(ctx)) return;

            ctx.TriggerManager.Fire(ctx, TriggerTiming.OnRoundEnd, new TriggerContext
            {
                Round = CurrentRound,
            });
            _settlement.DrainPendingQueue(ctx);

            ctx.BuffManager.OnRoundEnd(ctx, CurrentRound);
            if (CheckDeathAndBattleOver(ctx)) return;

            foreach (var kv in ctx.AllPlayers)
            {
                var shield = kv.Value.HeroEntity.Shield;
                if (shield <= 0) continue;

                kv.Value.HeroEntity.Shield = 0;
                ctx.RoundLog.Add($"[RoundManager] {kv.Key} 回合结束护盾清零（{shield} → 0）。");
            }

            ctx.TriggerManager.TickDecay(ctx);
            ctx.CardManager.OnRoundEnd(ctx, CurrentRound);
            ctx.CardManager.DestroyTempCards(ctx);

            ctx.CurrentPhase = BattleContext.BattlePhase.RoundEnd;
            ctx.EventBus.Publish(new RoundEndEvent { Round = CurrentRound });
            ctx.RoundLog.Add($"[RoundManager] 第 {CurrentRound} 回合结束。");
        }

        private bool CheckDeathAndBattleOver(BattleContext ctx)
        {
            var deadPlayers = new List<string>();

            foreach (var kv in ctx.AllPlayers)
            {
                if (!kv.Value.HeroEntity.IsAlive)
                    deadPlayers.Add(kv.Key);
            }

            foreach (var deadId in deadPlayers)
            {
                var deadPlayer = ctx.AllPlayers[deadId];
                if (deadPlayer.HeroEntity.DeathEventFired)
                    continue;

                deadPlayer.HeroEntity.DeathEventFired = true;
                ctx.RoundLog.Add($"[RoundManager] 玩家 {deadId} 死亡！");

                ctx.TriggerManager.Fire(ctx, TriggerTiming.OnNearDeath, new TriggerContext
                {
                    SourceEntityId = deadPlayer.HeroEntity.EntityId,
                    Extra = new Dictionary<string, object> { ["playerId"] = deadId },
                });
                _settlement.DrainPendingQueue(ctx);

                if (deadPlayer.HeroEntity.IsAlive)
                {
                    deadPlayer.HeroEntity.DeathEventFired = false;
                    ctx.RoundLog.Add($"[RoundManager] 玩家 {deadId} 被复活！");
                    continue;
                }

                ctx.TriggerManager.Fire(ctx, TriggerTiming.OnDeath, new TriggerContext
                {
                    SourceEntityId = deadPlayer.HeroEntity.EntityId,
                    Extra = new Dictionary<string, object> { ["playerId"] = deadId },
                });
                _settlement.DrainPendingQueue(ctx);

                ctx.EventBus.Publish(new EntityDeathEvent
                {
                    EntityId = deadPlayer.HeroEntity.EntityId,
                    KillerEntityId = string.Empty,
                });
                ctx.EventBus.Publish(new PlayerDeathEvent { PlayerId = deadId });
            }

            var finalAlivePlayers = new List<string>();
            foreach (var kv in ctx.AllPlayers)
            {
                if (kv.Value.HeroEntity.IsAlive)
                    finalAlivePlayers.Add(kv.Key);
            }

            if (finalAlivePlayers.Count == 0)
            {
                IsBattleOver = true;
                WinnerId = null;
                ctx.CurrentPhase = BattleContext.BattlePhase.BattleEnd;
                ctx.RoundLog.Add("[RoundManager] 战斗结束：平局！");
                ctx.EventBus.Publish(new BattleEndEvent { WinnerId = null, IsDraw = true });
                return true;
            }

            if (finalAlivePlayers.Count == 1)
            {
                IsBattleOver = true;
                WinnerId = finalAlivePlayers[0];
                ctx.CurrentPhase = BattleContext.BattlePhase.BattleEnd;
                ctx.RoundLog.Add($"[RoundManager] 战斗结束：玩家 {WinnerId} 获胜！");
                ctx.EventBus.Publish(new BattleEndEvent { WinnerId = WinnerId, IsDraw = false });
                return true;
            }

            return false;
        }

        private static List<EffectUnit>? ResolveCardEffects(
            BattleContext ctx,
            string configId,
            List<EffectUnit>? legacyEffects)
        {
            var definitionEffects = ctx.BuildCardEffects(configId);
            if (definitionEffects != null)
                return definitionEffects;

            if (legacyEffects == null)
                return null;

            return EffectUnitCloner.CloneMany(legacyEffects);
        }

        private static void StampCardSourceMetadata(List<EffectUnit> effects, BattleCard card)
        {
            StampCardSourceMetadata(effects, card.InstanceId, card.ConfigId);
        }

        private static void StampCardSourceMetadata(
            List<EffectUnit> effects,
            string cardInstanceId,
            string cardConfigId)
        {
            foreach (var effect in effects)
            {
                effect.Params["sourceCardInstanceId"] = cardInstanceId;
                effect.Params["sourceCardConfigId"] = cardConfigId;
            }
        }
    }
}
