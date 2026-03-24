
#pragma warning disable CS8632

using System;
using System.Collections.Generic;
using CardMoba.BattleCore.Foundation;
using CardMoba.BattleCore.Context;
using CardMoba.BattleCore.EventBus;
using CardMoba.BattleCore.Managers;

namespace CardMoba.BattleCore.Core
{
    /// <summary>
    /// 回合管理器（RoundManager）—— 负责管理战斗的回合生命周期。
    ///
    /// 回合流程：
    ///   1. BeginRound()       — 回合开始（OnRoundStart 触发、发牌）
    ///   2. 玩家出牌阶段（瞬策牌由外部调用 PlayInstantCard）
    ///   3. 玩家提交定策牌（CommitPlanCard）
    ///   4. EndRound()         — 提交结算（定策五层 + Buff衰减 + 死亡检查）
    /// </summary>
    public class RoundManager
    {
        private readonly SettlementEngine _settlement;

        /// <summary>当前回合号（从 1 开始）</summary>
        public int CurrentRound { get; private set; } = 0;

        /// <summary>战斗是否已结束</summary>
        public bool IsBattleOver { get; private set; } = false;

        /// <summary>胜利玩家 ID（null 表示平局或未结束）</summary>
        public string? WinnerId { get; private set; } = null;

        // 本回合提交的定策牌（EndRound 时批量结算）
        private readonly List<CommittedPlanCard> _pendingPlanCards = new List<CommittedPlanCard>();

        public RoundManager()
        {
            _settlement = new SettlementEngine();
        }

        // ══════════════════════════════════════════════════════════
        // 战斗初始化
        // ══════════════════════════════════════════════════════════

        /// <summary>
        /// 初始化战斗（BattleContext 创建完成后调用一次）。
        /// </summary>
        public void InitBattle(BattleContext ctx)
        {
            CurrentRound = 0;
            IsBattleOver = false;
            WinnerId     = null;
            _pendingPlanCards.Clear();

            ctx.RoundLog.Add("[RoundManager] 战斗初始化完成。");
            ctx.EventBus.Publish(new BattleStartEvent
            {
                Round = 0,
            });
        }

        // ══════════════════════════════════════════════════════════
        // 回合开始
        // ══════════════════════════════════════════════════════════

        /// <summary>
        /// 开始新的一回合（发牌、触发 OnRoundStart 事件链）。
        /// </summary>
        public void BeginRound(BattleContext ctx)
        {
            if (IsBattleOver) return;

            CurrentRound++;
            _pendingPlanCards.Clear();

            // 同步到 BattleContext（供 ConditionChecker / DynamicParamResolver 等读取）
            ctx.CurrentRound = CurrentRound;
            ctx.CurrentPhase = BattleContext.BattlePhase.RoundStart;

            ctx.RoundLog.Add($"[RoundManager] ══ 第 {CurrentRound} 回合开始 ══");

            // 发布回合开始事件
            ctx.EventBus.Publish(new RoundStartEvent { Round = CurrentRound });

            // 触发 OnRoundStart 触发器（回血、充能、Buff 激活等）
            ctx.TriggerManager.Fire(ctx, TriggerTiming.OnRoundStart, new TriggerContext
            {
                Round = CurrentRound,
            });
            _settlement.DrainPendingQueue(ctx);

            // 通知各管理器处理回合开始逻辑（抽牌等）
            ctx.CardManager.OnRoundStart(ctx, CurrentRound);

            ctx.RoundLog.Add($"[RoundManager] 第 {CurrentRound} 回合开始处理完毕。");
        }

        // ══════════════════════════════════════════════════════════
        // 出牌（瞬策牌）
        // ══════════════════════════════════════════════════════════

        /// <summary>
        /// 玩家打出一张瞬策牌（立即结算）。
        /// </summary>
        /// <param name="ctx">战斗上下文</param>
        /// <param name="playerId">出牌玩家 ID</param>
        /// <param name="cardInstanceId">卡牌实例 ID</param>
        /// <param name="effects">效果列表（从配置读取）</param>
        public List<EffectResult> PlayInstantCard(
            BattleContext ctx,
            string playerId,
            string cardInstanceId,
            List<EffectUnit> effects)
        {
            if (IsBattleOver) return new List<EffectResult>();

            ctx.RoundLog.Add($"[RoundManager] 玩家 {playerId} 打出瞬策牌 {cardInstanceId}。");
            var results = _settlement.ResolveInstant(ctx, playerId, cardInstanceId, effects);

            // 检查死亡
            CheckDeathAndBattleOver(ctx);

            return results;
        }

        // ══════════════════════════════════════════════════════════
        // 定策牌提交
        // ══════════════════════════════════════════════════════════

        /// <summary>
        /// 玩家提交一张定策牌（不立即结算，等待 EndRound 统一结算）。
        /// 内部先通过 CardManager 校验卡牌合法性（必须在手牌区），校验失败则拒绝。
        /// </summary>
        public bool CommitPlanCard(BattleContext ctx, CommittedPlanCard planCard)
        {
            if (IsBattleOver) return false;

            // ── 强约束：校验卡牌合法性（Hand → StrategyZone）────────
            bool valid = ctx.CardManager.CommitPlanCard(ctx, planCard.CardInstanceId);
            if (!valid)
            {
                ctx.RoundLog.Add($"[RoundManager] ⚠️ 玩家 {planCard.PlayerId} 定策牌 {planCard.CardInstanceId} 校验失败，拒绝提交。");
                return false;
            }

            planCard.SubmitOrder = _pendingPlanCards.Count;
            _pendingPlanCards.Add(planCard);

            ctx.RoundLog.Add($"[RoundManager] 玩家 {planCard.PlayerId} 提交定策牌 {planCard.CardInstanceId}（顺序={planCard.SubmitOrder}）。");
            return true;
        }

        // ══════════════════════════════════════════════════════════
        // 回合结束
        // ══════════════════════════════════════════════════════════

        /// <summary>
        /// 结束当前回合，执行定策五层结算、Buff 衰减、死亡检查。
        /// </summary>
        public void EndRound(BattleContext ctx)
        {
            if (IsBattleOver) return;

            ctx.RoundLog.Add($"[RoundManager] ══ 第 {CurrentRound} 回合结算开始 ══");
            ctx.CurrentPhase = BattleContext.BattlePhase.Settlement;

            // ── 定策五层结算 ─────────────────────────────────────
            if (_pendingPlanCards.Count > 0)
            {
                _settlement.ResolvePlanCards(ctx, _pendingPlanCards);
            }
            else
            {
                ctx.RoundLog.Add("[RoundManager] 本回合无定策牌，跳过定策结算。");
            }
            _pendingPlanCards.Clear();

            // ── 死亡检查（结算后） ────────────────────────────────
            if (CheckDeathAndBattleOver(ctx)) return;

            // ── OnRoundEnd 触发器 ─────────────────────────────────
            ctx.TriggerManager.Fire(ctx, TriggerTiming.OnRoundEnd, new TriggerContext
            {
                Round = CurrentRound,
            });
            _settlement.DrainPendingQueue(ctx);

            // ── Buff 衰减 ─────────────────────────────────────────
            ctx.BuffManager.OnRoundEnd(ctx, CurrentRound);

            // ── 再次死亡检查（Buff 衰减后可能 DOT 触发） ────────────
            if (CheckDeathAndBattleOver(ctx)) return;

            // ── 护盾清零（回合结束后护盾不保留到下回合） ─────────────
            foreach (var kv in ctx.AllPlayers)
            {
                var shield = kv.Value.HeroEntity.Shield;
                if (shield > 0)
                {
                    kv.Value.HeroEntity.Shield = 0;
                    ctx.RoundLog.Add($"[RoundManager] {kv.Key} 回合结束护盾清零（{shield} → 0）。");
                }
            }

            // ── TriggerManager 回合衰减（非 Buff 托管的触发器 RemainingRounds 递减）──
            ctx.TriggerManager.TickDecay(ctx);

            // ── 弃手牌（Hand + StrategyZone → Discard） ───────────
            ctx.CardManager.OnRoundEnd(ctx, CurrentRound);

            // ── 回合结束事件 ──────────────────────────────────────
            ctx.CurrentPhase = BattleContext.BattlePhase.RoundEnd;
            ctx.EventBus.Publish(new RoundEndEvent { Round = CurrentRound });
            ctx.RoundLog.Add($"[RoundManager] ══ 第 {CurrentRound} 回合结束 ══");
        }

        // ══════════════════════════════════════════════════════════
        // 死亡与战斗结束检查
        // ══════════════════════════════════════════════════════════

        /// <summary>
        /// 检查所有玩家的存活状态，触发死亡事件并判断战斗是否结束。
        /// </summary>
        /// <returns>如果战斗已结束则返回 true</returns>
        private bool CheckDeathAndBattleOver(BattleContext ctx)
        {
            var deadPlayers   = new List<string>();
            var alivePlayers  = new List<string>();

            foreach (var kv in ctx.AllPlayers)
            {
                var player = kv.Value;
                if (!player.HeroEntity.IsAlive)
                {
                    deadPlayers.Add(player.PlayerId);
                }
                else
                {
                    alivePlayers.Add(player.PlayerId);
                }
            }

            foreach (var deadId in deadPlayers)
            {
                if (!ctx.AllPlayers[deadId].HeroEntity.DeathEventFired)
                {
                    ctx.AllPlayers[deadId].HeroEntity.DeathEventFired = true;
                    ctx.RoundLog.Add($"[RoundManager] 玩家 {deadId} 死亡！");

                    // OnNearDeath 触发（可能有复活效果）
                    ctx.TriggerManager.Fire(ctx, TriggerTiming.OnNearDeath, new TriggerContext
                    {
                        SourceEntityId = ctx.AllPlayers[deadId].HeroEntity.EntityId,
                        Extra          = new Dictionary<string, object> { ["playerId"] = deadId },
                    });
                    _settlement.DrainPendingQueue(ctx);

                    // 复活检查：若复活成功，重置标记并跳过 OnDeath
                    if (ctx.AllPlayers[deadId].HeroEntity.IsAlive)
                    {
                        ctx.AllPlayers[deadId].HeroEntity.DeathEventFired = false;
                        ctx.RoundLog.Add($"[RoundManager] 玩家 {deadId} 被复活！");
                        continue;
                    }

                    ctx.TriggerManager.Fire(ctx, TriggerTiming.OnDeath, new TriggerContext
                    {
                        SourceEntityId = ctx.AllPlayers[deadId].HeroEntity.EntityId,
                        Extra          = new Dictionary<string, object> { ["playerId"] = deadId },
                    });
                    _settlement.DrainPendingQueue(ctx);

                    // 发布 EntityDeathEvent（供动画/统计等外部消费）
                    var deadEntity = ctx.AllPlayers[deadId].HeroEntity;
                    ctx.EventBus.Publish(new EntityDeathEvent
                    {
                        EntityId       = deadEntity.EntityId,
                        KillerEntityId = string.Empty,   // 当前架构不追踪击杀者，留空
                    });
                    ctx.EventBus.Publish(new PlayerDeathEvent { PlayerId = deadId });
                }
            }

            // 重新统计存活玩家
            var finalAlivePlayers = new List<string>();
            foreach (var kv in ctx.AllPlayers)
            {
                if (kv.Value.HeroEntity.IsAlive)
                    finalAlivePlayers.Add(kv.Key);
            }

            if (finalAlivePlayers.Count == 0)
            {
                // 平局
                IsBattleOver = true;
                WinnerId     = null;
                ctx.RoundLog.Add("[RoundManager] 战斗结束：平局！");
                ctx.EventBus.Publish(new BattleEndEvent { WinnerId = null, IsDraw = true });
                return true;
            }

            if (finalAlivePlayers.Count == 1)
            {
                // 唯一存活者获胜
                IsBattleOver = true;
                WinnerId     = finalAlivePlayers[0];
                ctx.RoundLog.Add($"[RoundManager] 战斗结束：玩家 {WinnerId} 获胜！");
                ctx.EventBus.Publish(new BattleEndEvent { WinnerId = WinnerId, IsDraw = false });
                return true;
            }

            return false;
        }
    }
}
