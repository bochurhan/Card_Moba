#pragma warning disable CS8632

using CardMoba.Protocol.Enums;

namespace CardMoba.BattleCore.Modifiers
{
    /// <summary>运行时数值修正器。</summary>
    public class ValueModifier
    {
        public string ModifierId { get; set; } = string.Empty;
        public ModifierType Type { get; set; }
        public int Value { get; set; }
        public string OwnerPlayerId { get; set; } = string.Empty;
        public EffectType TargetEffectType { get; set; }
        public ModifierScope Scope { get; set; }
    }
}
