#pragma warning disable CS8632

using System.Collections.Generic;
using CardMoba.BattleCore.Context;
using CardMoba.BattleCore.Foundation;

namespace CardMoba.BattleCore.Resolvers
{
    /// <summary>
    /// 效果目标类型。
    /// </summary>
    public enum TargetType
    {
        Self = 0,
        Opponent = 1,
        AllOpponents = 2,
        All = 3,
        None = 4,
    }

    /// <summary>
    /// 将目标语义解析为具体实体列表。
    /// </summary>
    public class TargetResolver
    {
        public List<Entity> Resolve(BattleContext ctx, string targetTypeStr, Entity source)
        {
            var type = ParseTargetType(targetTypeStr);
            return Resolve(ctx, type, source);
        }

        public List<Entity> Resolve(BattleContext ctx, TargetType targetType, Entity source)
        {
            var result = new List<Entity>();

            switch (targetType)
            {
                case TargetType.Self:
                    result.Add(source);
                    break;

                case TargetType.Opponent:
                case TargetType.AllOpponents:
                    foreach (var kv in ctx.AllPlayers)
                    {
                        if (kv.Key != source.OwnerPlayerId)
                            result.Add(kv.Value.HeroEntity);
                    }
                    break;

                case TargetType.All:
                    foreach (var kv in ctx.AllPlayers)
                        result.Add(kv.Value.HeroEntity);
                    break;

                case TargetType.None:
                default:
                    break;
            }

            return result;
        }

        public static TargetType ParseTargetType(string? raw)
        {
            if (string.IsNullOrWhiteSpace(raw))
                return TargetType.None;

            switch (raw.Trim().ToLowerInvariant())
            {
                case "self":
                    return TargetType.Self;
                case "opponent":
                case "enemy":
                    return TargetType.Opponent;
                case "allopponents":
                case "allenemies":
                    return TargetType.AllOpponents;
                case "all":
                    return TargetType.All;
                case "none":
                default:
                    return TargetType.None;
            }
        }
    }
}
