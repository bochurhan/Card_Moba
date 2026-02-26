#pragma warning disable CS8632 // nullable reference types annotation
using System.Collections.Generic;
using CardMoba.Protocol.Enums;

namespace CardMoba.BattleCore.Settlement.Handlers
{
    /// <summary>
    /// 效果处理器注册中心 —— 管理所有 EffectType 到 IEffectHandler 的映射。
    ///
    /// 使用手动注册方式（而非反射），原因：
    /// - 启动速度更快
    /// - AOT 编译兼容（IL2CPP）
    /// - 依赖关系更清晰，便于调试
    /// </summary>
    public static class HandlerRegistry
    {
        private static readonly Dictionary<EffectType, IEffectHandler> _handlers = new();
        private static bool _initialized = false;

        /// <summary>
        /// 初始化并注册所有 Handler。
        /// 应在战斗开始前调用一次。
        /// </summary>
        public static void Initialize()
        {
            if (_initialized) return;

            _handlers.Clear();

            // ── Layer 0: 反制 ──
            Register(EffectType.Counter, new CounterHandler());

            // ── Layer 1: 防御/修正 ──
            Register(EffectType.Shield,          new ShieldHandler());
            Register(EffectType.Armor,           new ArmorHandler());
            Register(EffectType.AttackBuff,      new StrengthHandler());
            Register(EffectType.AttackDebuff,    new StrengthHandler());
            Register(EffectType.Reflect,         new ThornsHandler());
            Register(EffectType.DamageReduction, new DamageReductionHandler());
            Register(EffectType.Invincible,      new InvincibleHandler());

            // ── Layer 2: 伤害 ──
            Register(EffectType.Damage,    new DamageHandler());
            Register(EffectType.Lifesteal, new LifestealHandler());
            Register(EffectType.Thorns,    new ThornsHandler());
            Register(EffectType.ArmorOnHit,new ArmorOnHitHandler());

            // ── Layer 3: 功能 ──
            Register(EffectType.Heal,       new HealHandler());
            Register(EffectType.Stun,       new StunHandler());
            Register(EffectType.Vulnerable, new VulnerableHandler());
            Register(EffectType.Weak,       new WeakHandler());
            Register(EffectType.Draw,       new DrawHandler());
            Register(EffectType.Discard,    new DiscardHandler());
            Register(EffectType.GainEnergy, new EnergyHandler());
            Register(EffectType.Silence,    new SilenceHandler());
            Register(EffectType.Slow,       new SlowHandler());

            _initialized = true;
        }

        /// <summary>
        /// 注册一个效果处理器。
        /// </summary>
        public static void Register(EffectType effectType, IEffectHandler handler)
        {
            _handlers[effectType] = handler;
        }

        /// <summary>
        /// 获取指定效果类型的处理器。
        /// </summary>
        /// <returns>处理器实例，找不到则返回 null</returns>
        public static IEffectHandler? GetHandler(EffectType effectType)
        {
            return _handlers.TryGetValue(effectType, out var handler) ? handler : null;
        }

        /// <summary>
        /// 检查是否有指定效果类型的处理器。
        /// </summary>
        public static bool HasHandler(EffectType effectType)
        {
            return _handlers.ContainsKey(effectType);
        }

        /// <summary>
        /// 获取已注册的所有效果类型。
        /// </summary>
        public static IEnumerable<EffectType> GetRegisteredTypes()
        {
            return _handlers.Keys;
        }
    }
}