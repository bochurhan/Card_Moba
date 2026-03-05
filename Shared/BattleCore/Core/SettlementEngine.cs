
#pragma warning disable CS8632

using System;
using System.Collections.Generic;
using System.Linq;
using CardMoba.BattleCore.Context;
using CardMoba.BattleCore.EventBus;
using CardMoba.BattleCore.Foundation;
using CardMoba.BattleCore.Handlers;
using CardMoba.BattleCore.Managers;   // TriggerContext / ITriggerManager

namespace CardMoba.BattleCore.Core
{
    /// <summary>
    /// 定策牌条目 —— 玩家提交的一张定策牌及其归属
    /// </summary>
    public class CommittedPlanCard
    {
        /// <summary>提交玩家 ID</summary>
        public string PlayerId { get; set; } = string.Empty;
        /// <summary>卡牌实例 ID</summary>
        public string CardInstanceId { get; set; } = string.Empty;
        /// <summary>提交顺序索引（用于同 Layer 内的出牌顺序）</summary>
        public int SubmitOrder { get; set; }
        /// <summary>卡牌配置中的效果列表（从 CardConfig 读取）</summary>
        public List<EffectUnit> Effects { get; set; } = new List<EffectUnit>();
        /// <summary>是否被反制（Layer 0 结算后设置）</summary>
        public bool IsCountered { get; set; }
    }

    /// <summary>
    /// 结算引擎（SettlementEngine）—— BattleCore V2 核心。
    ///
    /// 职责：执行效果原子（EffectUnit），管理 PendingEffectQueue 消化。
    /// 是唯一可合法写入 BattleContext 的入口（Handler 内通过 ctx 写入）。
    ///
    /// 两类结算入口：
    ///   ResolveInstant(card)       — 瞬策牌即时结算
    ///   ResolvePlanCards(cards)    — 定策牌五层批量结算
    ///
    /// 每次主结算完成后必须调用 DrainPendingQueue()。
    /// </summary>
    public class SettlementEngine
    {
        private readonly HandlerPool _handlerPool;
        private readonly Resolvers.TargetResolver _targetResolver;

        public SettlementEngine()
        {
            _handlerPool    = HandlerPool.Instance;
            _targetResolver = new Resolvers.TargetResolver();
        }

        // ══════════════════════════════════════════════════════════
        // 瞬策牌结算
        // ══════════════════════════════════════════════════════════

        /// <summary>
        /// 瞬策牌即时结算。
        /// </summary>
        /// <param name="ctx">战斗上下文</param>
        /// <param name="playerId">出牌玩家 ID</param>
        /// <param name="cardInstanceId">卡牌实例 ID</param>
        /// <param name="effects">此卡的效果列表（从配置读取）</param>
        /// <returns>各效果的执行结果列表</returns>
        public List<EffectResult> ResolveInstant(
            BattleContext ctx,
            string playerId,
            string cardInstanceId,
            List<EffectUnit> effects)
        {
            var playerData = ctx.GetPlayer(playerId);
            if (playerData == null)
            {
                ctx.RoundLog.Add($"[SettlementEngine] ⚠️ 找不到玩家 {playerId}，跳过瞬策结算。");
                return new List<EffectResult>();
            }

            var source = playerData.HeroEntity;

            // BeforePlayCard 触发（沉默检查等）
            ctx.TriggerManager.Fire(ctx, TriggerTiming.BeforePlayCard, new TriggerContext
            {
                SourceEntityId = source.EntityId,
            });
            DrainPendingQueue(ctx);

            // 沉默状态检查（沉默不允许打出 Buff/治疗类牌，具体逻辑可扩展）
            if (source.IsSilenced)
            {
                ctx.RoundLog.Add($"[SettlementEngine] {playerId} 处于沉默状态，无法打出此牌。");
                return new List<EffectResult>();
            }

            // 逐效果执行
            var priorResults = new List<EffectResult>();
            foreach (var effect in effects)
            {
                var targets = _targetResolver.Resolve(ctx, effect.TargetType, source);
                var result  = _handlerPool.Execute(ctx, effect, source, targets, priorResults);
                priorResults.Add(result);
                DrainPendingQueue(ctx);
            }

            // AfterPlayCard 触发
            ctx.TriggerManager.Fire(ctx, TriggerTiming.AfterPlayCard, new TriggerContext
            {
                SourceEntityId = source.EntityId,
            });
            DrainPendingQueue(ctx);

            ctx.EventBus.Publish(new CardPlayedEvent
            {
                PlayerId       = playerId,
                CardInstanceId = cardInstanceId,
            });

            return priorResults;
        }

        // ══════════════════════════════════════════════════════════
        // 定策牌五层结算
        // ══════════════════════════════════════════════════════════

        /// <summary>
        /// 定策牌五层批量结算（Layer 0 → 4）。
        /// </summary>
        public void ResolvePlanCards(BattleContext ctx, List<CommittedPlanCard> planCards)
        {
            // ── Layer 0：反制层 ───────────────────────────────────
            ctx.RoundLog.Add("═══ Layer 0：反制结算 ═══");
            ResolveLayer0_Counter(ctx, planCards);
            DrainPendingQueue(ctx);

            // ── Layer 1：防御/修正层 ──────────────────────────────
            ctx.RoundLog.Add("═══ Layer 1：防御/修正结算 ═══");
            ResolveLayer(ctx, planCards, SettleLayer.Defense);
            DrainPendingQueue(ctx);

            // ── Pre-Layer 2：为每位玩家拍摄防御快照 ─────────────────
            ctx.RoundLog.Add("═══ Pre-Layer 2：防御快照 ═══");
            TakeDefenseSnapshots(ctx);

            // ── Layer 2：伤害层 ────────────────────────────────────
            ctx.RoundLog.Add("═══ Layer 2：伤害结算 ═══");
            ResolveLayer2_Damage(ctx, planCards);
            // 注意：Layer 2 内部每张牌完整走 A-B-C 后 DrainPendingQueue，不在此处统一消化

            // 清除防御快照
            ClearDefenseSnapshots(ctx);

            // ── Layer 3：资源层 ────────────────────────────────────
            ctx.RoundLog.Add("═══ Layer 3：资源结算 ═══");
            ResolveLayer(ctx, planCards, SettleLayer.Resource);
            DrainPendingQueue(ctx);

            // ── Layer 4：Buff/特殊层 ──────────────────────────────
            ctx.RoundLog.Add("═══ Layer 4：Buff/特殊结算 ═══");
            ResolveLayer(ctx, planCards, SettleLayer.BuffSpecial);
            DrainPendingQueue(ctx);
        }

        // ── Layer 0 实现 ──────────────────────────────────────────

        private void ResolveLayer0_Counter(BattleContext ctx, List<CommittedPlanCard> planCards)
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
                    _handlerPool.Execute(ctx, effect, source, targets, new List<EffectResult>());
                }
            }
        }

        // ── Layer 2 实现（己方顺序依赖 + 双方快照隔离）────────────────

        private void ResolveLayer2_Damage(BattleContext ctx, List<CommittedPlanCard> planCards)
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
                    // 伤害效果的目标实时状态（己方已被前面的牌修改），
                    // 但对方防御以 DefenseSnapshot 为基准（由 TargetResolver 内部处理）
                    var targets = _targetResolver.Resolve(ctx, effect.TargetType, source);
                    var result  = _handlerPool.Execute(ctx, effect, source, targets, priorResults);
                    priorResults.Add(result);
                }

                // 每张牌 A-B-C 完成后消化队列（支持吸血等触发器效果）
                DrainPendingQueue(ctx);
            }
        }

        // ── 通用 Layer 执行（非 Layer 2）────────────────────────────

        private void ResolveLayer(BattleContext ctx, List<CommittedPlanCard> planCards, SettleLayer layer)
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
                    var result  = _handlerPool.Execute(ctx, effect, source, targets, priorResults);
                    priorResults.Add(result);
                }
            }
        }

        // ── 防御快照工具方法 ──────────────────────────────────────────

        private void TakeDefenseSnapshots(BattleContext ctx)
        {
            foreach (var kv in ctx.AllPlayers)
            {
                var p = kv.Value;
                p.CurrentDefenseSnapshot = new DefenseSnapshot
                {
                    PlayerId    = p.PlayerId,
                    Hp          = p.HeroEntity.Hp,
                    Shield      = p.HeroEntity.Shield,
                    Armor       = p.HeroEntity.Armor,
                    IsInvincible = p.HeroEntity.IsInvincible,
                };
            }
            ctx.RoundLog.Add("[SettlementEngine] 防御快照已拍摄。");
        }

        private void ClearDefenseSnapshots(BattleContext ctx)
        {
            foreach (var kv in ctx.AllPlayers)
                kv.Value.CurrentDefenseSnapshot = null;
        }

        // ══════════════════════════════════════════════════════════
        // PendingQueue 消化
        // ══════════════════════════════════════════════════════════

        /// <summary>
        /// 循环消化延迟效果队列，直到队列为空。
        /// 每条效果执行后可能再次推入新条目，因此持续循环直到清空。
        /// </summary>
        public void DrainPendingQueue(BattleContext ctx)
        {
            int safetyLimit = 1000; // 防止无限循环（理论上不会触发）
            int count = 0;

            while (ctx.PendingQueue.Count > 0 && count < safetyLimit)
            {
                count++;
                var entry = ctx.PendingQueue.Dequeue();
                if (entry == null) break;

                var playerData = ctx.GetPlayer(entry.SourceEntityId);
                // 尝试直接按 EntityId 找 Entity（兼容非玩家实体）
                Entity? source = null;
                foreach (var kv in ctx.AllPlayers)
                {
                    if (kv.Value.HeroEntity.EntityId == entry.SourceEntityId)
                    {
                        source = kv.Value.HeroEntity;
                        break;
                    }
                }

                if (source == null)
                {
                    ctx.RoundLog.Add($"[DrainPendingQueue] ⚠️ 找不到施法实体 {entry.SourceEntityId}，跳过。");
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

                _handlerPool.Execute(ctx, entry.Effect, source, targets, new List<EffectResult>());
            }

            if (count >= safetyLimit)
                ctx.RoundLog.Add("[DrainPendingQueue] ⚠️ 达到安全上限（1000次），可能存在无限触发循环！");
        }

        private List<Entity> ResolveEntityIds(BattleContext ctx, List<string> ids)
        {
            var result = new List<Entity>();
            foreach (var id in ids)
            {
                foreach (var kv in ctx.AllPlayers)
                {
                    if (kv.Value.HeroEntity.EntityId == id)
                    {
                        result.Add(kv.Value.HeroEntity);
                        break;
                    }
                }
            }
            return result;
        }
    }
}
