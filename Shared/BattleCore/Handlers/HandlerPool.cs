
#pragma warning disable CS8632

using System.Collections.Generic;
using CardMoba.BattleCore.Foundation;
using CardMoba.BattleCore.Context;
using CardMoba.BattleCore.Resolvers;
using CardMoba.Protocol.Enums;

namespace CardMoba.BattleCore.Handlers
{
    /// <summary>
    /// Handler 注册表与分发池（HandlerPool）—— 单例，全局唯一。
    /// 持有 EffectType → IEffectHandler 的映射，由 SettlementEngine 调用分发。
    ///
    /// Execute() 统一完成两项前置工作，再委托给具体 Handler：
    ///   1. 效果级条件检查（EffectUnit.Conditions → ConditionChecker）
    ///   2. 效果级数值解析（EffectUnit.ValueExpression → DynamicParamResolver）
    ///      注意：解析后的数值缓存在 EffectUnit.ResolvedValue 供 Handler 读取。
    /// </summary>
    public class HandlerPool
    {
        private static readonly ConditionChecker     _conditionChecker = new ConditionChecker();
        private static readonly DynamicParamResolver _dynParamResolver = new DynamicParamResolver();

        private readonly Dictionary<EffectType, IEffectHandler> _handlers
            = new Dictionary<EffectType, IEffectHandler>();

        private static HandlerPool? _instance;

        /// <summary>全局单例（懒加载）</summary>
        public static HandlerPool Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = new HandlerPool();
                    _instance.RegisterDefaults();
                }
                return _instance;
            }
        }

        private HandlerPool() { }

        // ══════════════════════════════════════════════════════════
        // 注册
        // ══════════════════════════════════════════════════════════

        /// <summary>注册一个 Handler（已存在时覆盖）</summary>
        public void Register(EffectType type, IEffectHandler handler)
        {
            _handlers[type] = handler;
        }

        /// <summary>注册所有默认 Handler</summary>
        private void RegisterDefaults()
        {
            Register(EffectType.Damage,       new DamageHandler());
            Register(EffectType.Pierce,       new DamageHandler());   // 复用，穿透标记在 effect.Params
            Register(EffectType.Heal,         new HealHandler());
            Register(EffectType.Shield,       new ShieldHandler());
            Register(EffectType.AddBuff,      new AddBuffHandler());
            Register(EffectType.Draw,         new DrawCardHandler());
            Register(EffectType.GainEnergy,   new GainEnergyHandler());
            Register(EffectType.GenerateCard, new GenerateCardHandler());
            Register(EffectType.MoveSelectedCardToDeckTop, new MoveSelectedCardToDeckTopHandler());
            Register(EffectType.ReturnSourceCardToHandAtRoundEnd, new ReturnSourceCardToHandAtRoundEndHandler());
            Register(EffectType.UpgradeCardsInHand, new UpgradeCardsInHandHandler());
            Register(EffectType.Lifesteal,    new LifestealHandler());
        }

        // ══════════════════════════════════════════════════════════
        // 分发执行
        // ══════════════════════════════════════════════════════════

        /// <summary>
        /// 分发并执行效果。若对应 Handler 未注册，写入日志并返回失败结果。
        ///
        /// 执行前统一完成：
        ///   1. 效果级条件检查（ConditionChecker），不满足时返回跳过结果
        ///   2. ValueExpression 动态解析（DynamicParamResolver），结果写入 effect.ResolvedValue
        /// </summary>
        public EffectResult Execute(
            BattleContext ctx,
            EffectUnit effect,
            Entity source,
            List<Entity> targets,
            List<EffectResult> priorResults,
            TriggerContext? triggerContext = null)
        {
            if (!_handlers.TryGetValue(effect.Type, out var handler))
            {
                ctx.RoundLog.Add($"[HandlerPool] ⚠️ 未找到 EffectType={effect.Type} 的 Handler，跳过。");
                return new EffectResult { EffectId = effect.EffectId, Type = effect.Type, Success = false };
            }

            // ── 前置 1：效果级条件检查 ───────────────────────────
            if (effect.Conditions != null && effect.Conditions.Count > 0)
            {
                bool passed = _conditionChecker.Check(effect.Conditions, ctx, source, triggerContext);
                effect.ConditionPassed = passed;
                if (!passed)
                {
                    ctx.RoundLog.Add($"[HandlerPool] 效果 '{effect.EffectId}'（{effect.Type}）条件未满足，跳过。");
                    return new EffectResult { EffectId = effect.EffectId, Type = effect.Type, Success = false };
                }
            }

            // ── 前置 2：动态数值解析 ─────────────────────────────
            effect.ResolvedValue = _dynParamResolver.Resolve(
                effect.ValueExpression,
                ctx,
                effect,
                source,
                priorResults,
                triggerContext);

            return handler.Execute(ctx, effect, source, targets, priorResults, triggerContext);
        }
    }
}
