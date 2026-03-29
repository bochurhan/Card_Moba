using System.Collections.Generic;

namespace CardMoba.MatchFlow.Context
{
    public sealed class TeamMatchState
    {
        public string TeamId { get; set; } = string.Empty;
        public List<string> PlayerIds { get; } = new List<string>();
        public int TeamScore { get; set; }
        public bool ObjectiveDestroyed { get; set; }

        public TeamMatchState Clone()
        {
            var clone = new TeamMatchState
            {
                TeamId = TeamId,
                TeamScore = TeamScore,
                ObjectiveDestroyed = ObjectiveDestroyed,
            };

            foreach (var playerId in PlayerIds)
                clone.PlayerIds.Add(playerId);

            return clone;
        }
    }
}