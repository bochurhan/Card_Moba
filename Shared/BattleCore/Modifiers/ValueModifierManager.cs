#pragma warning disable CS8632

using System.Collections.Generic;
using System.Linq;
using CardMoba.Protocol.Enums;

namespace CardMoba.BattleCore.Modifiers
{
    /// <summary>
    /// 运行时数值修正器管理器。
    /// 应用顺序固定为 Add -> Mul -> Set。
    /// </summary>
    public class ValueModifierManager : IValueModifierManager
    {
        private readonly List<ValueModifier> _modifiers = new List<ValueModifier>();
        private int _idCounter;

        public string AddModifier(ValueModifier modifier)
        {
            if (string.IsNullOrEmpty(modifier.ModifierId))
                modifier.ModifierId = $"mod_{++_idCounter:D6}";

            _modifiers.Add(modifier);
            return modifier.ModifierId;
        }

        public bool RemoveModifier(string modifierId)
        {
            int removed = _modifiers.RemoveAll(m => m.ModifierId == modifierId);
            return removed > 0;
        }

        public int Apply(EffectType effectType, string ownerPlayerId, ModifierScope scope, int baseValue)
        {
            var applicable = _modifiers
                .Where(m =>
                    m.TargetEffectType == effectType &&
                    m.OwnerPlayerId == ownerPlayerId &&
                    m.Scope == scope)
                .ToList();

            int value = baseValue;

            foreach (var modifier in applicable.Where(m => m.Type == ModifierType.Add))
                value += modifier.Value;

            foreach (var modifier in applicable.Where(m => m.Type == ModifierType.Mul))
                value = value * modifier.Value / 100;

            var setModifiers = applicable.Where(m => m.Type == ModifierType.Set).ToList();
            if (setModifiers.Count > 0)
                value = setModifiers.Last().Value;

            return value < 0 ? 0 : value;
        }
    }
}
