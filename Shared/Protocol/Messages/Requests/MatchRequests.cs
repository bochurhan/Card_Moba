using System.Collections.Generic;
using CardMoba.Protocol.Messages.Common;

namespace CardMoba.Protocol.Messages.Requests
{
    public sealed class CreateLocalMatchRequest
    {
        public string DisplayName { get; set; } = string.Empty;
    }

    public sealed class JoinLocalMatchRequest
    {
        public string MatchId { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
    }

    public sealed class ReadyRequest
    {
        public string MatchId { get; set; } = string.Empty;
    }

    public sealed class PlayInstantCardRequest
    {
        public string MatchId { get; set; } = string.Empty;
        public string CardInstanceId { get; set; } = string.Empty;
        public Dictionary<string, string> RuntimeParams { get; } = new Dictionary<string, string>();
    }

    public sealed class CommitPlanCardRequest
    {
        public string MatchId { get; set; } = string.Empty;
        public string CardInstanceId { get; set; } = string.Empty;
        public Dictionary<string, string> RuntimeParams { get; } = new Dictionary<string, string>();
    }

    public sealed class EndTurnRequest
    {
        public string MatchId { get; set; } = string.Empty;
    }

    public sealed class SetBattleTurnLockRequest
    {
        public string MatchId { get; set; } = string.Empty;
        public bool IsLocked { get; set; }
    }

    public sealed class SubmitBuildChoiceRequest
    {
        public string MatchId { get; set; } = string.Empty;
        public BuildChoiceDto Choice { get; set; } = new BuildChoiceDto();
    }

    public sealed class LockBuildWindowRequest
    {
        public string MatchId { get; set; } = string.Empty;
    }
}
