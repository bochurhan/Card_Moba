using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using CardMoba.Protocol.Messages.Messages;

namespace CardMoba.Client.Network.Connection
{
    /// <summary>
    /// 客户端联机连接抽象。
    /// 定义运行时与传输层之间的边界。
    /// </summary>
    public interface IMatchHubConnection : IDisposable
    {
        event Action<MatchCreatedMessage> MatchCreated;
        event Action<MatchJoinedMessage> MatchJoined;
        event Action<MatchStartedMessage> MatchStarted;
        event Action<PhaseChangedMessage> PhaseChanged;
        event Action<BattleSnapshotMessage> BattleSnapshotReceived;
        event Action<BuildWindowOpenedMessage> BuildWindowOpened;
        event Action<BuildWindowUpdatedMessage> BuildWindowUpdated;
        event Action<BuildWindowClosedMessage> BuildWindowClosed;
        event Action<BattleEndedMessage> BattleEnded;
        event Action<MatchEndedMessage> MatchEnded;
        event Action<ActionRejectedMessage> ActionRejected;

        bool IsConnected { get; }
        string CurrentMatchId { get; }
        string LocalPlayerId { get; }

        Task ConnectAsync(string hubUrl);
        Task DisconnectAsync();
        Task CreateLocalMatchAsync(string displayName);
        Task JoinLocalMatchAsync(string matchId, string displayName);
        Task ReadyAsync();
        Task PlayInstantCardAsync(string cardInstanceId, IReadOnlyDictionary<string, string> runtimeParams = null);
        Task CommitPlanCardAsync(string cardInstanceId, IReadOnlyDictionary<string, string> runtimeParams = null);
        Task EndTurnAsync();
        Task SetBattleTurnLockAsync(bool isLocked);
        Task SubmitBuildChoiceAsync(CardMoba.Protocol.Messages.Common.BuildChoiceDto choice);
        Task LockBuildWindowAsync();
    }
}
