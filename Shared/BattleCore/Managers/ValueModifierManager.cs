
#pragma warning disable CS8632

using System.Collections.Generic;
using System.Linq;
using CardMoba.BattleCore.Foundation;
using CardMoba.BattleCore.Context;
using CardMoba.Protocol.Enums;

namespace CardMoba.BattleCore.Managers
{
    /// <summary>
    /// ValueModifierManager —— IValueModifierManager 的具体实现。
    ///
    /// 所有运行时力量/虚弱/伤害减免等数值修正均以 ValueModifier 存储于此。
    /// 应用顺序：Add（累加） → Mul（乘法，整数 ‰ 精度） → Set（覆盖，极少用）。
    /// </summary>
    public class ValueModifierManager : IValueModifierManager
    {
        private readonly List<ValueModifier> _modifiers = new List<ValueModifier>();
        private int _idCounter = 0;

        /// <inheritdoc/>
        public string AddModifier(ValueModifier modifier)
        {
            if (string.IsNullOrEmpty(modifier.ModifierId))
                modifier.ModifierId = $"mod_{++_idCounter:D6}";
            _modifiers.Add(modifier);
            return modifier.ModifierId;
        }

        /// <inheritdoc/>
        public bool RemoveModifier(string modifierId)
        {
            int removed = _modifiers.RemoveAll(m => m.ModifierId == modifierId);
            return removed > 0;
        }

        /// <inheritdoc/>
        public int Apply(EffectType effectType, string ownerPlayerId, ModifierScope scope, int baseValue)
        {
            // 筛选适用的修正器
            var applicable = _modifiers
                .Where(m =>
                    m.TargetEffectType == effectType &&
                    m.OwnerPlayerId == ownerPlayerId &&
                    m.Scope == scope)
                .ToList();

            int value = baseValue;

            // 1. Add 修正
            foreach (var m in applicable.Where(m => m.Type == ModifierType.Add))
                value += m.Value;

            // 2. Mul 修正（用整数百分比：Value 为百分比值，如 150 = 1.5×）
            //    公式：value = value * factor / 100
            foreach (var m in applicable.Where(m => m.Type == ModifierType.Mul))
                value = value * m.Value / 100;

            // 3. Set 修正（强制覆盖，多个 Set 取最后一个）
            var setModifiers = applicable.Where(m => m.Type == ModifierType.Set).ToList();
            if (setModifiers.Count > 0)
                value = setModifiers.Last().Value;

            // 最终值不低于 0（伤害/治疗最低为 0）
            return value < 0 ? 0 : value;
        }
    }
}
