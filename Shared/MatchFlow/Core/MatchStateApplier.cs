using System;
using CardMoba.BattleCore.Results;
using CardMoba.MatchFlow.Context;

namespace CardMoba.MatchFlow.Core
{
    public sealed class MatchStateApplier
    {
        public void ApplyBattleResult(MatchContext context, BattleSummary summary)
        {
            if (context == null)
                throw new ArgumentNullException(nameof(context));
            if (summary == null)
                throw new ArgumentNullException(nameof(summary));
            if (context.ActiveBattleContext == null)
                throw new InvalidOperationException("ActiveBattleContext is required to apply a battle result.");

            foreach (var player in context.ActiveBattleContext.AllPlayers.Values)
            {
                if (!context.Players.TryGetValue(player.PlayerId, out var matchPlayer))
                    continue;

                matchPlayer.PersistentHp = player.HeroEntity.Hp < 0 ? 0 : player.HeroEntity.Hp;
                matchPlayer.WasDefeatedInLastBattle = summary.DeadPlayerIds.Contains(player.PlayerId)
                    || !player.HeroEntity.IsAlive;
            }

            foreach (var playerId in summary.ExtraBuildPickPlayerIds)
            {
                if (context.Players.TryGetValue(playerId, out var matchPlayer))
                    matchPlayer.BonusBuildPickCount++;
            }

            if (!string.IsNullOrWhiteSpace(summary.DestroyedObjectiveEntityId))
            {
                var entity = context.ActiveBattleContext.GetEntity(summary.DestroyedObjectiveEntityId);
                if (entity != null && context.Teams.TryGetValue(entity.TeamId, out var team))
                    team.ObjectiveDestroyed = true;
            }

            if (summary.MatchTerminated)
            {
                context.IsMatchOver = true;
                context.WinnerTeamId = summary.WinningTeamId;
            }
        }
    }
}
