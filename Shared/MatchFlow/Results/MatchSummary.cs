namespace CardMoba.MatchFlow.Results
{
    public sealed class MatchSummary
    {
        public string MatchId { get; set; } = string.Empty;
        public string? WinnerTeamId { get; set; }
        public int CompletedStepCount { get; set; }
        public bool IsDraw { get; set; }
    }
}