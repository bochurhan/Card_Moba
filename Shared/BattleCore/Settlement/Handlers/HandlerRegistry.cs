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
    /// - 依赖关系更清晰
    /// - 便于调试
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

            // ══════════════════════════════════════════════════════════
            // V3.0 核心效果类型 (1-10)
            // ══════════════════════════════════════════════════════════
            
            // Layer 0: 反制
            var counterHandler = new CounterHandler();
            Register(EffectType.Counter, counterHandler);           // V3.0: 1
            Register(EffectType.CounterCard, counterHandler);       // Legacy: 401
            Register(EffectType.CounterFirstDamage, counterHandler);// Legacy: 402
            Register(EffectType.CounterAndReflect, counterHandler); // Legacy: 403

            // Layer 1: 防御/修正
            var shieldHandler = new ShieldHandler();
            Register(EffectType.Shield, shieldHandler);            // V3.0: 3
            Register(EffectType.GainShield, shieldHandler);        // Legacy: 102
            
            var armorHandler = new ArmorHandler();
            Register(EffectType.Armor, armorHandler);              // V3.0: 6
            Register(EffectType.GainArmor, armorHandler);          // Legacy: 101
            
            var strengthHandler = new StrengthHandler();
            Register(EffectType.AttackBuff, strengthHandler);      // V3.0: 7
            Register(EffectType.GainStrength, strengthHandler);    // Legacy: 111
            Register(EffectType.ReduceStrength, strengthHandler);  // Legacy: 112
            
            Register(EffectType.Reflect, new ThornsHandler());     // V3.0: 8
            Register(EffectType.Thorns, new ThornsHandler());      // Legacy: 211
            
            Register(EffectType.Vulnerable, new VulnerableHandler());  // V3.0: 9
            Register(EffectType.Weak, new WeakHandler());          // Legacy: 116
            Register(EffectType.DamageReduction, new DamageReductionHandler());
            Register(EffectType.Invincible, new InvincibleHandler());

            // Layer 2: 伤害
            var damageHandler = new DamageHandler();
            Register(EffectType.Damage, damageHandler);            // V3.0: 2
            Register(EffectType.DealDamage, damageHandler);        // Legacy: 201
            Register(EffectType.Lifesteal, new LifestealHandler());// Legacy: 212

            // Layer 3: 功能
            var stunHandler = new StunHandler();
            Register(EffectType.Stun, stunHandler);                // V3.0: 5
            
            var healHandler = new HealHandler();
            Register(EffectType.Heal, healHandler);                // V3.0: 4
            
            var drawHandler = new DrawHandler();
            Register(EffectType.Draw, drawHandler);                // V3.0: 10
            
            Register(EffectType.Silence, new SilenceHandler());    // Legacy: 311
            Register(EffectType.GainEnergy, new EnergyHandler());  // Legacy: 303

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
