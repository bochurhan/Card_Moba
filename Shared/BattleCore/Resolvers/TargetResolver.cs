#pragma warning disable CS8632

using System;
using System.Collections.Generic;
using CardMoba.BattleCore.Context;
using CardMoba.BattleCore.Foundation;

namespace CardMoba.BattleCore.Resolvers
{
    public enum TargetType
    {
        Self = 0,
        Opponent = 1,
        AllOpponents = 2,
        All = 3,
        None = 4,
        Teammate = 5,
        AllAllies = 6,
        EnemyObjective = 7,
    }

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
            var sourceTeamId = ResolveSourceTeamId(ctx, source);

            switch (targetType)
            {
                case TargetType.Self:
                    result.Add(source);
                    break;

                case TargetType.Teammate:
                    AddTeamHeroes(ctx, result, sourceTeamId, source.OwnerPlayerId, includeSource: false);
                    break;

                case TargetType.AllAllies:
                    AddTeamHeroes(ctx, result, sourceTeamId, source.OwnerPlayerId, includeSource: true);
                    break;

                case TargetType.Opponent:
                case TargetType.AllOpponents:
                    foreach (var team in ctx.AllTeams.Values)
                    {
                        if (string.Equals(team.TeamId, sourceTeamId, StringComparison.Ordinal))
                            continue;

                        AddTeamHeroes(ctx, result, team.TeamId, source.OwnerPlayerId, includeSource: true);
                    }
                    break;

                case TargetType.EnemyObjective:
                    foreach (var team in ctx.AllTeams.Values)
                    {
                        if (string.Equals(team.TeamId, sourceTeamId, StringComparison.Ordinal))
                            continue;

                        var objective = ctx.GetObjectiveForTeam(team.TeamId);
                        if (objective != null && IsEligibleTarget(ctx, objective))
                            result.Add(objective);
                    }
                    break;

                case TargetType.All:
                    foreach (var player in ctx.AllPlayers.Values)
                    {
                        if (IsEligibleTarget(ctx, player.HeroEntity))
                            result.Add(player.HeroEntity);
                    }
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
                case "teammate":
                case "ally":
                    return TargetType.Teammate;
                case "allallies":
                case "allies":
                    return TargetType.AllAllies;
                case "enemyobjective":
                case "objective":
                    return TargetType.EnemyObjective;
                case "all":
                    return TargetType.All;
                case "none":
                default:
                    return TargetType.None;
            }
        }

        private static void AddTeamHeroes(
            BattleContext ctx,
            List<Entity> result,
            string teamId,
            string sourceOwnerPlayerId,
            bool includeSource)
        {
            foreach (var player in ctx.GetPlayersByTeam(teamId))
            {
                if (!includeSource && string.Equals(player.PlayerId, sourceOwnerPlayerId, StringComparison.Ordinal))
                    continue;

                if (IsEligibleTarget(ctx, player.HeroEntity))
                    result.Add(player.HeroEntity);
            }
        }

        private static string ResolveSourceTeamId(BattleContext ctx, Entity source)
        {
            if (!string.IsNullOrWhiteSpace(source.TeamId))
                return source.TeamId;

            if (!string.IsNullOrWhiteSpace(source.OwnerPlayerId))
                return ctx.GetPlayer(source.OwnerPlayerId)?.TeamId ?? string.Empty;

            return string.Empty;
        }

        private static bool IsEligibleTarget(BattleContext ctx, Entity entity)
        {
            if (entity == null || !entity.IsAlive || !entity.IsTargetable)
                return false;

            foreach (var requiredDeadEntityId in entity.RequiredDeadEntityIdsToTarget)
            {
                var blocker = ctx.GetEntity(requiredDeadEntityId);
                if (blocker != null && blocker.IsAlive)
                    return false;
            }

            return true;
        }
    }
}