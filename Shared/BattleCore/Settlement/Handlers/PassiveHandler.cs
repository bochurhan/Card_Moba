#pragma warning disable CS8632

using CardMoba.BattleCore.Buff;
using CardMoba.BattleCore.Context;
using CardMoba.BattleCore.Trigger;
using CardMoba.ConfigModels.Card;
using CardMoba.Protocol.Enums;

namespace CardMoba.BattleCore.Settlement.Handlers
{
    /// <summary>
    /// PassiveHandler —— 处理 ExecutionMode = Passive 的卡牌效果。
    ///
    /// 职责：
    ///   将一个 Passive 效果包装为"本回合临时 Buff"注册到 BuffManager，
    ///   由 BuffManager 自动管理触发器的注册/注销，回合结束后自动清理。
    ///
    /// 适用场景举例：
    ///   - 反制牌：打出时注册 BeforePlayCard 触发器，下回合拦截敌方牌
    ///   - 反伤牌：打出时注册 AfterTakeDamage 触发器，受伤后反弹伤害
    ///
    /// 注意：
    ///   - PassiveHandler 本身是无状态单例，不存储私有字段
    ///   - 生命周期由 BuffManager + TriggerManager 联合管理
    ///   - 此 Handler 在 SettlementEngine.ExecuteEffect 的"Passive 模式跳过"之前不会被调用；
    ///     它只在 SettlementEngine 将 Passive 效果路由到此处时执行（未来扩展点）
    ///
    /// 当前实现策略：
    ///   被 SettlementEngine.ExecuteEffect 在 Passive 模式时调用（目前 SettlementEngine 直接 return，
    ///   调用方应在卡牌打出时（PlayCard/CommitPlanCard）显式调用 RegisterPassiveEffect，
    ///   而非通过结算引擎的 Handler 路由。
    /// </summary>
    public static class PassiveHandler
    {
        // ══════════════════════════════════════════════════════════
        // 核心入口：将 Passive 效果注册为临时 Buff
        // ══════════════════════════════════════════════════════════

        /// <summary>
        /// 将卡牌中所有 ExecutionMode = Passive 的效果注册为临时 Buff。
        ///
        /// 调用时机：
        ///   - 瞬策牌：在 ResolveInstantCard 的效果执行阶段（BeforePlayCard 之后）
        ///   - 定策牌：在卡牌加入 PendingPlanCards 时，或在 Layer0-3 各层处理前统一注册
        ///
        /// 注意：此方法幂等，重复调用同一张牌的效果时，
        ///       BuffManager 会根据 BuffStackRule 决定是叠加还是刷新。
        /// </summary>
        /// <param name="card">打出的卡牌</param>
        /// <param name="ctx">战斗上下文</param>
        public static void RegisterPassiveEffects(PlayedCard card, BattleContext ctx)
        {
            foreach (var effect in card.Config.Effects)
            {
                if (effect.ExecutionMode != EffectExecutionMode.Passive)
                    continue;

                RegisterSinglePassiveEffect(card, effect, ctx);
            }
        }

        // ══════════════════════════════════════════════════════════
        // 内部注册逻辑
        // ══════════════════════════════════════════════════════════

        /// <summary>
        /// 将单个 Passive 效果注册为触发器，绕过 BuffManager（因为 Passive 效果是"卡牌级"的，
        /// 没有对应的 BuffConfig，直接通过 TriggerManager 注册临时触发器）。
        ///
        /// 生命周期：
        ///   - RemainingRounds = effect.PassiveDuration（默认 1 = 本回合结束时清除）
        ///   - SourceType = TriggerSourceType.Card（与 Buff 来源区分，便于清理）
        ///   - SourceId = card.RuntimeId（触发器归属到卡牌实例，回合清理时可精准注销）
        /// </summary>
        private static void RegisterSinglePassiveEffect(PlayedCard card, CardEffect effect, BattleContext ctx)
        {
            TriggerTiming timing = (TriggerTiming)effect.PassiveTriggerTiming;

            // PassiveTriggerTiming == 0 时无法映射有效时机，记录警告并跳过
            if (effect.PassiveTriggerTiming == 0)
            {
                ctx.RoundLog.Add(
                    $"[PassiveHandler] 「{card.Config.CardName}」的 Passive 效果 {effect.EffectType} " +
                    $"未设置 PassiveTriggerTiming，跳过注册");
                return;
            }

            string sourceId   = card.SourcePlayerId;
            string triggerId  = $"passive_{card.RuntimeId}_{effect.EffectType}";

            // 捕获循环变量，避免闭包问题
            CardEffect capturedEffect = effect;
            PlayedCard capturedCard   = card;

            var trigger = new TriggerInstance
            {
                TriggerName       = $"[Passive]{card.Config.CardName}-{effect.EffectType}",
                Timing            = timing,
                OwnerPlayerId     = sourceId,
                SourceId          = triggerId,
                SourceType        = TriggerSourceType.Card,
                Priority          = capturedEffect.Priority,
                RemainingTriggers = -1,                              // 在有效期内可无限次触发
                RemainingRounds   = capturedEffect.PassiveDuration,  // 默认 1 = 本回合

                // Condition：只对自身（出牌方）触发的事件响应
                // 注意：TriggerContext 中 SourcePlayerId 是触发事件的来源玩家
                Condition = tCtx => tCtx.SourcePlayerId == sourceId,

                // Effect：根据 EffectType 路由到对应逻辑
                Effect = tCtx => ExecutePassiveTrigger(capturedCard, capturedEffect, tCtx, ctx)
            };

            ctx.TriggerManager?.RegisterTrigger(trigger);
            ctx.RoundLog.Add(
                $"[PassiveHandler] 玩家{sourceId}的「{card.Config.CardName}」" +
                $"注册了 Passive 触发器（{timing}，持续{capturedEffect.PassiveDuration}回合）");
        }

        /// <summary>
        /// Passive 触发器触发时的执行入口。
        /// 根据 EffectType 路由到对应的逻辑处理。
        /// </summary>
        private static void ExecutePassiveTrigger(
            PlayedCard card,
            CardEffect effect,
            TriggerContext tCtx,
            BattleContext ctx)
        {
            var handler = HandlerRegistry.GetHandler(effect.EffectType);
            if (handler == null)
            {
                ctx.RoundLog.Add(
                    $"[PassiveHandler] 未找到 Passive 效果 {effect.EffectType} 对应的 Handler，跳过");
                return;
            }

            var source = ctx.GetPlayer(tCtx.SourcePlayerId);
            var target = string.IsNullOrEmpty(tCtx.TargetPlayerId)
                ? null
                : ctx.GetPlayer(tCtx.TargetPlayerId);

            if (source == null) return;

            ctx.RoundLog.Add(
                $"[PassiveHandler] 「{card.Config.CardName}」Passive 效果 {effect.EffectType} 触发！");

            handler.Execute(card, effect, source, target, ctx);
        }
    }
}
