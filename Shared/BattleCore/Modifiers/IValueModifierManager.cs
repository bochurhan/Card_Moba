#pragma warning disable CS8632

using CardMoba.Protocol.Enums;

namespace CardMoba.BattleCore.Modifiers
{
    /// <summary>
    /// 管理运行时数值修正器，在结算阶段按固定顺序应用到基础值上。
    /// </summary>
    public interface IValueModifierManager
    {
        string AddModifier(ValueModifier modifier);

        bool RemoveModifier(string modifierId);

        int Apply(EffectType effectType, string ownerPlayerId, ModifierScope scope, int baseValue);
    }
}
