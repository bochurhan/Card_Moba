#pragma warning disable CS8632

using System.Collections.Generic;

namespace CardMoba.BattleCore.Foundation
{
    public static class EffectUnitCloner
    {
        public static List<EffectUnit> CloneMany(IEnumerable<EffectUnit> effects)
        {
            var result = new List<EffectUnit>();
            foreach (var effect in effects)
                result.Add(Clone(effect));
            return result;
        }

        public static EffectUnit Clone(EffectUnit source)
        {
            return new EffectUnit
            {
                EffectId        = source.EffectId,
                Type            = source.Type,
                TargetType      = source.TargetType,
                ValueExpression = source.ValueExpression,
                Layer           = source.Layer,
                Conditions      = new List<string>(source.Conditions),
                Params          = new Dictionary<string, string>(source.Params),
            };
        }
    }
}
