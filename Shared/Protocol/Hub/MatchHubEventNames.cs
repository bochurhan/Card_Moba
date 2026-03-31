namespace CardMoba.Protocol.Hub
{
    public static class MatchHubEventNames
    {
        public const string MatchCreated = nameof(MatchCreated);
        public const string MatchJoined = nameof(MatchJoined);
        public const string MatchStarted = nameof(MatchStarted);
        public const string PhaseChanged = nameof(PhaseChanged);
        public const string BattleSnapshot = nameof(BattleSnapshot);
        public const string BuildWindowOpened = nameof(BuildWindowOpened);
        public const string BuildWindowUpdated = nameof(BuildWindowUpdated);
        public const string BuildWindowClosed = nameof(BuildWindowClosed);
        public const string BattleEnded = nameof(BattleEnded);
        public const string MatchEnded = nameof(MatchEnded);
        public const string ActionRejected = nameof(ActionRejected);
    }
}
