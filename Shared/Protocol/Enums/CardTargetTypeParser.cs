using System;

namespace CardMoba.Protocol.Enums
{
    /// <summary>
    /// 兼容 cards.json 中使用的目标别名，避免字符串枚举名与内部枚举名不一致时静默回落。
    /// </summary>
    public static class CardTargetTypeParser
    {
        public static bool TryParse(string? raw, out CardTargetType targetType)
        {
            targetType = CardTargetType.None;
            if (string.IsNullOrWhiteSpace(raw))
                return false;

            if (Enum.TryParse<CardTargetType>(raw, true, out targetType))
                return true;

            switch (raw.Trim().ToLowerInvariant())
            {
                case "enemy":
                case "opponent":
                case "currentenemy":
                    targetType = CardTargetType.CurrentEnemy;
                    return true;

                case "allenemies":
                case "allopponents":
                    targetType = CardTargetType.AllEnemies;
                    return true;

                case "allallies":
                    targetType = CardTargetType.AllAllies;
                    return true;

                case "anyenemy":
                    targetType = CardTargetType.AnyEnemy;
                    return true;

                case "anyally":
                    targetType = CardTargetType.AnyAlly;
                    return true;

                case "self":
                    targetType = CardTargetType.Self;
                    return true;

                case "all":
                    targetType = CardTargetType.All;
                    return true;

                case "none":
                    targetType = CardTargetType.None;
                    return true;

                default:
                    return false;
            }
        }

        public static CardTargetType ParseOrDefault(string? raw, CardTargetType defaultValue)
        {
            return TryParse(raw, out var targetType) ? targetType : defaultValue;
        }
    }
}
