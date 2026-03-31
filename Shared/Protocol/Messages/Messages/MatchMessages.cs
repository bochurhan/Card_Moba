using System.Collections.Generic;
using CardMoba.Protocol.Messages.Common;

namespace CardMoba.Protocol.Messages.Messages
{
    public sealed class MatchCreatedMessage
    {
        public string MatchId { get; set; } = string.Empty;
        public string LocalPlayerId { get; set; } = string.Empty;
        public List<MatchParticipantDto> Participants { get; } = new List<MatchParticipantDto>();
    }

    public sealed class MatchJoinedMessage
    {
        public string MatchId { get; set; } = string.Empty;
        public string LocalPlayerId { get; set; } = string.Empty;
        public List<MatchParticipantDto> Participants { get; } = new List<MatchParticipantDto>();
    }

    public sealed class MatchStartedMessage
    {
        public string MatchId { get; set; } = string.Empty;
        public int BattleIndex { get; set; }
        public int TotalBattleCount { get; set; }
    }

    public sealed class PhaseChangedMessage
    {
        public string MatchId { get; set; } = string.Empty;
        public ServerPhaseKind PhaseKind { get; set; }
        public int BattleIndex { get; set; }
        public int TotalBattleCount { get; set; }
        public long DeadlineUnixMs { get; set; }
    }

    public sealed class BattleSnapshotMessage
    {
        public BattleSnapshotDto Snapshot { get; set; } = new BattleSnapshotDto();
    }

    public sealed class BuildWindowOpenedMessage
    {
        public BuildWindowDto BuildWindow { get; set; } = new BuildWindowDto();
    }

    public sealed class BuildWindowUpdatedMessage
    {
        public BuildWindowDto BuildWindow { get; set; } = new BuildWindowDto();
    }

    public sealed class BuildWindowClosedMessage
    {
        public string MatchId { get; set; } = string.Empty;
    }

    public sealed class BattleEndedMessage
    {
        public BattleResultDto Result { get; set; } = new BattleResultDto();
    }

    public sealed class MatchEndedMessage
    {
        public string MatchId { get; set; } = string.Empty;
        public string? WinnerTeamId { get; set; }
        public bool IsDraw => string.IsNullOrWhiteSpace(WinnerTeamId);
    }

    public sealed class ActionRejectedMessage
    {
        public string MatchId { get; set; } = string.Empty;
        public string ActionName { get; set; } = string.Empty;
        public ProtocolActionErrorCode ErrorCode { get; set; }
        public string Reason { get; set; } = string.Empty;
    }
}
