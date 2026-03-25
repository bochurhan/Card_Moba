#pragma warning disable CS8632

using System.Collections.Generic;
using System.Linq;
using CardMoba.BattleCore.Context;
using CardMoba.BattleCore.EventBus;
using CardMoba.BattleCore.Foundation;
using CardMoba.BattleCore.Handlers;

namespace CardMoba.BattleCore.Core
{
    public class SettlementEngine
    {
        private readonly HandlerPool _handlerPool;
        private readonly Resolvers.TargetResolver _targetResolver;

        public SettlementEngine()
        {
            _handlerPool = HandlerPool.Instance;
            _targetResolver = new Resolvers.TargetResolver();
        }

        public List<EffectResult> ResolveInstantFromCard(
            BattleContext ctx,
            string playerId,
            BattleCard card,
            List<EffectUnit> effects)
        {
            var playerData = ctx.GetPlayer(playerId);
            if (playerData == null)
            {
                ctx.RoundLog.Add($"[SettlementEngine] 找不到玩家 {playerId}，跳过瞬策结算（卡牌 {card.InstanceId}）。");
                return new List<EffectResult>();
            }

            var source = playerData.HeroEntity;
            var triggerContext = new TriggerContext
            {
                SourceEntityId = source.EntityId,
                Extra = new Dictionary<string, object>
                {
                    ["cardInstanceId"] = card.InstanceId,
                    ["cardConfigId"] = card.ConfigId,
                },
            };

            ctx.TriggerManager.Fire(ctx, TriggerTiming.BeforePlayCard, triggerContext);
            DrainPendingQueue(ctx);

            if (source.IsSilenced)
            {
                ctx.RoundLog.Add($"[SettlementEngine] {playerId} 处于沉默状态，无法打出 {card.InstanceId}。");
                return new List<EffectResult>();
            }

            var preparedCard = ctx.CardManager.PrepareInstantCard(ctx, playerId, card.InstanceId);
            if (preparedCard == null)
                return new List<EffectResult>();

            var priorResults = new List<EffectResult>();
            foreach (var effect in effects)
            {
                var targets = _targetResolver.Resolve(ctx, effect.TargetType, source);
                var result = _handlerPool.Execute(ctx, effect, source, targets, priorResults, null);
                priorResults.Add(result);
                DrainPendingQueue(ctx);
            }

            ctx.TriggerManager.Fire(ctx, TriggerTiming.AfterPlayCard, triggerContext);
            DrainPendingQueue(ctx);

            ctx.EventBus.Publish(new CardPlayedEvent
            {
                PlayerId = playerId,
                CardInstanceId = preparedCard.InstanceId,
                CardConfigId = preparedCard.ConfigId,
            });

            return priorResults;
        }

        internal void ResolvePlanCards(BattleContext ctx, List<PendingPlanCard> planCards)
        {
            ctx.RoundLog.Add("[SettlementEngine] Layer 0：反制结算。");
            ResolveLayer0_Counter(ctx, planCards);
            DrainPendingQueue(ctx);

            ctx.RoundLog.Add("[SettlementEngine] Layer 1：防御/修正结算。");
            ResolveLayer(ctx, planCards, SettleLayer.Defense);
            DrainPendingQueue(ctx);

            ctx.RoundLog.Add("[SettlementEngine] Pre-Layer 2：拍防御快照。");
            TakeDefenseSnapshots(ctx);

            ctx.RoundLog.Add("[SettlementEngine] Layer 2：伤害结算。");
            ResolveLayer2_Damage(ctx, planCards);
            ClearDefenseSnapshots(ctx);

            ctx.RoundLog.Add("[SettlementEngine] Layer 3：资源结算。");
            ResolveLayer(ctx, planCards, SettleLayer.Resource);
            DrainPendingQueue(ctx);

            ctx.RoundLog.Add("[SettlementEngine] Layer 4：Buff/特殊结算。");
            ResolveLayer(ctx, planCards, SettleLayer.BuffSpecial);
            DrainPendingQueue(ctx);
        }

        private void ResolveLayer0_Counter(BattleContext ctx, List<PendingPlanCard> planCards)
        {
            var counterCards = planCards
                .Where(c => !c.IsCountered && c.Effects.Any(e => e.Layer == SettleLayer.Counter))
                .OrderBy(c => c.SubmitOrder)
                .ToList();

            foreach (var planCard in counterCards)
            {
                var playerData = ctx.GetPlayer(planCard.PlayerId);
                if (playerData == null) continue;

                var source = playerData.HeroEntity;
                foreach (var effect in planCard.Effects.Where(e => e.Layer == SettleLayer.Counter))
                {
                    var targets = _targetResolver.Resolve(ctx, effect.TargetType, source);
                    _handlerPool.Execute(ctx, effect, source, targets, new List<EffectResult>(), null);
                }
            }
        }

        private void ResolveLayer2_Damage(BattleContext ctx, List<PendingPlanCard> planCards)
        {
            var damageCards = planCards
                .Where(c => !c.IsCountered && c.Effects.Any(e => e.Layer == SettleLayer.Damage))
                .OrderBy(c => c.SubmitOrder)
                .ToList();

            foreach (var planCard in damageCards)
            {
                var playerData = ctx.GetPlayer(planCard.PlayerId);
                if (playerData == null) continue;

                var source = playerData.HeroEntity;
                var priorResults = new List<EffectResult>();

                foreach (var effect in planCard.Effects.Where(e => e.Layer == SettleLayer.Damage))
                {
                    var targets = _targetResolver.Resolve(ctx, effect.TargetType, source);
                    var result = _handlerPool.Execute(ctx, effect, source, targets, priorResults, null);
                    priorResults.Add(result);
                }

                DrainPendingQueue(ctx);
            }
        }

        private void ResolveLayer(BattleContext ctx, List<PendingPlanCard> planCards, SettleLayer layer)
        {
            var layerCards = planCards
                .Where(c => !c.IsCountered && c.Effects.Any(e => e.Layer == layer))
                .OrderBy(c => c.SubmitOrder)
                .ToList();

            foreach (var planCard in layerCards)
            {
                var playerData = ctx.GetPlayer(planCard.PlayerId);
                if (playerData == null) continue;

                var source = playerData.HeroEntity;
                var priorResults = new List<EffectResult>();

                foreach (var effect in planCard.Effects.Where(e => e.Layer == layer))
                {
                    var targets = _targetResolver.Resolve(ctx, effect.TargetType, source);
                    var result = _handlerPool.Execute(ctx, effect, source, targets, priorResults, null);
                    priorResults.Add(result);
                }
            }
        }

        private void TakeDefenseSnapshots(BattleContext ctx)
        {
            foreach (var kv in ctx.AllPlayers)
            {
                var player = kv.Value;
                player.CurrentDefenseSnapshot = new DefenseSnapshot
                {
                    PlayerId = player.PlayerId,
                    Hp = player.HeroEntity.Hp,
                    Shield = player.HeroEntity.Shield,
                    Armor = player.HeroEntity.Armor,
                    IsInvincible = player.HeroEntity.IsInvincible,
                };
            }

            ctx.RoundLog.Add("[SettlementEngine] 防御快照已拍摄。");
        }

        private void ClearDefenseSnapshots(BattleContext ctx)
        {
            foreach (var kv in ctx.AllPlayers)
                kv.Value.CurrentDefenseSnapshot = null;
        }

        public void DrainPendingQueue(BattleContext ctx)
        {
            int safetyLimit = 1000;
            int count = 0;

            while (ctx.PendingQueue.Count > 0 && count < safetyLimit)
            {
                count++;
                var entry = ctx.PendingQueue.Dequeue();
                if (entry == null) break;

                var source = ctx.GetEntity(entry.SourceEntityId);
                if (source == null)
                {
                    ctx.RoundLog.Add($"[DrainPendingQueue] 找不到施法实体 {entry.SourceEntityId}，跳过队列效果。");
                    continue;
                }

                List<Entity> targets;
                if (entry.PreResolvedTargetIds != null && entry.PreResolvedTargetIds.Count > 0)
                {
                    targets = ResolveEntityIds(ctx, entry.PreResolvedTargetIds);
                }
                else
                {
                    targets = _targetResolver.Resolve(ctx, entry.Effect.TargetType, source);
                }

                _handlerPool.Execute(
                    ctx,
                    entry.Effect,
                    source,
                    targets,
                    new List<EffectResult>(),
                    entry.TriggerContext);
            }

            if (count >= safetyLimit)
                ctx.RoundLog.Add("[DrainPendingQueue] 达到安全上限（1000 次），可能存在无限触发循环。");
        }

        private List<Entity> ResolveEntityIds(BattleContext ctx, List<string> ids)
        {
            var result = new List<Entity>();
            foreach (var id in ids)
            {
                var entity = ctx.GetEntity(id);
                if (entity != null)
                    result.Add(entity);
            }
            return result;
        }
    }
}
