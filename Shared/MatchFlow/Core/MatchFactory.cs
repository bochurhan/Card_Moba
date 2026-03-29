using System;
using System.Collections.Generic;
using CardMoba.MatchFlow.Context;
using CardMoba.MatchFlow.Rules;

namespace CardMoba.MatchFlow.Core
{
    public sealed class MatchFactory
    {
        public MatchContext CreateMatch(
            string matchId,
            MatchRuleset ruleset,
            IEnumerable<PlayerMatchState> players,
            IEnumerable<TeamMatchState>? teams = null,
            int baseRandomSeed = 0)
        {
            if (string.IsNullOrWhiteSpace(matchId))
                throw new ArgumentException("matchId cannot be empty.", nameof(matchId));
            if (ruleset == null)
                throw new ArgumentNullException(nameof(ruleset));

            var context = new MatchContext
            {
                MatchId = matchId,
                BaseRandomSeed = baseRandomSeed,
                Ruleset = ruleset,
                CurrentPhase = MatchPhase.NotStarted,
            };

            foreach (var player in players)
            {
                if (string.IsNullOrWhiteSpace(player.PlayerId))
                    throw new ArgumentException("player.PlayerId cannot be empty.", nameof(players));
                if (string.IsNullOrWhiteSpace(player.TeamId))
                    throw new ArgumentException("player.TeamId cannot be empty.", nameof(players));

                var clone = player.Clone();
                context.Players[clone.PlayerId] = clone;

                if (!context.Teams.TryGetValue(clone.TeamId, out var team))
                {
                    team = new TeamMatchState { TeamId = clone.TeamId };
                    context.Teams[clone.TeamId] = team;
                }

                if (!team.PlayerIds.Contains(clone.PlayerId))
                    team.PlayerIds.Add(clone.PlayerId);
            }

            if (teams != null)
            {
                foreach (var sourceTeam in teams)
                {
                    if (!context.Teams.TryGetValue(sourceTeam.TeamId, out var targetTeam))
                    {
                        targetTeam = new TeamMatchState { TeamId = sourceTeam.TeamId };
                        context.Teams[targetTeam.TeamId] = targetTeam;
                    }

                    targetTeam.TeamScore = sourceTeam.TeamScore;
                    targetTeam.ObjectiveDestroyed = sourceTeam.ObjectiveDestroyed;
                    foreach (var playerId in sourceTeam.PlayerIds)
                    {
                        if (!targetTeam.PlayerIds.Contains(playerId))
                            targetTeam.PlayerIds.Add(playerId);
                    }
                }
            }

            context.MatchLog.Add($"[MatchFactory] created match {matchId} with {context.Players.Count} players and {context.Teams.Count} teams.");
            return context;
        }
    }
}