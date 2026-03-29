using System.Collections.Generic;

namespace CardMoba.BattleCore.Results
{
    public enum BattleEndReason
    {
        None = 0,
        RoundLimitReached = 1,
        TeamEliminated = 2,
        ObjectiveDestroyed = 3,
    }

    public enum MatchEndReason
    {
        None = 0,
        ObjectiveDestroyed = 1,
    }

    public sealed class BattleSummary
    {
        public string BattleId { get; set; } = string.Empty;
        public int RoundsPlayed { get; set; }
        public BattleEndReason BattleEndReason { get; set; }
        public bool MatchTerminated { get; set; }
        public MatchEndReason MatchEndReason { get; set; }
        public string? WinningTeamId { get; set; }
        public string? DestroyedObjectiveEntityId { get; set; }
        public List<string> DeadPlayerIds { get; } = new List<string>();
        public List<string> ExtraBuildPickPlayerIds { get; } = new List<string>();
    }
}