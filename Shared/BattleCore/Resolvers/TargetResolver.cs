#pragma warning disable CS8632

using System.Collections.Generic;
using CardMoba.BattleCore.Foundation;
using CardMoba.BattleCore.Context;

namespace CardMoba.BattleCore.Resolvers
{
    /// <summary>
    /// 目标类型枚举 —— 描述效果的目标指向。
    /// </summary>
    public enum TargetType
    {
        /// <summary>效果施放者本人</summary>
        Self = 0,
        /// <summary>单个对手</summary>
        Opponent = 1,
        /// <summary>所有对手（多人对战时扩展）</summary>
        AllOpponents = 2,
        /// <summary>所有玩家（含自身）</summary>
        All = 3,
        /// <summary>无目标（如抽牌、资源类效果）</summary>
        None = 4,
    }

    /// <summary>
    /// 目标解析器 —— 将 TargetType（枚举或字符串）解析为具体的 Entity 列表。
    /// 无状态单例，可安全共享。
    /// </summary>
    public class TargetResolver
    {
        // ══════════════════════════════════════════════════════════
        // 主入口：接受字符串（来自 EffectUnit.TargetType 配置字段）
        // ══════════════════════════════════════════════════════════

        /// <summary>
        /// 将字符串目标类型解析为 Entity 列表（主入口）。
        /// 支持的字符串值（大小写不敏感）：
        ///   "Self"、"Opponent"/"Enemy"、"AllOpponents"/"AllEnemies"、"All"、"None"
        /// </summary>
        public List<Entity> Resolve(BattleContext ctx, string targetTypeStr, Entity source)
        {
            var type = ParseTargetType(targetTypeStr);
            return Resolve(ctx, type, source);
        }

        // ══════════════════════════════════════════════════════════
        // 枚举重载：供内部或已知枚举值的调用方使用
        // ══════════════════════════════════════════════════════════

        /// <summary>
        /// 根据 TargetType 枚举和施法实体解析目标列表。
        /// </summary>
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

        // ══════════════════════════════════════════════════════════
        // 工具方法：字符串 → TargetType
        // ══════════════════════════════════════════════════════════

        /// <summary>
        /// 将字符串映射为 TargetType 枚举（大小写不敏感）。
        /// 未识别的值默认返回 None 并记录警告（调用方通过 ctx.RoundLog 可见）。
        /// </summary>
        public static TargetType ParseTargetType(string? raw)
        {
            if (string.IsNullOrWhiteSpace(raw))
                return TargetType.None;

            switch (raw.Trim().ToLowerInvariant())
            {
                case "self":                     return TargetType.Self;
                case "opponent":
                case "enemy":                    return TargetType.Opponent;
                case "allopponents":
                case "allenemies":               return TargetType.AllOpponents;
                case "all":                      return TargetType.All;
                case "none":
                default:                         return TargetType.None;
            }
        }
    }
}