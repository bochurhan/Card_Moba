using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using CardMoba.BattleCore.Context;
using CardMoba.BattleCore.Results;
using CardMoba.MatchFlow.Context;
using CardMoba.MatchFlow.Rules;
using CardMoba.Protocol.Hub;
using CardMoba.Protocol.Messages.Common;
using CardMoba.Protocol.Messages.Messages;
using CardMoba.Server.Host.Hubs;
using CardMoba.Server.Host.Services;
using CardMoba.Server.Host.Snapshots;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;

namespace CardMoba.Server.Host.Sessions
{
    /// <summary>
    /// 负责将会话运行时状态转换为协议消息并广播给客户端。
    /// </summary>
    public sealed class MatchBroadcaster
    {
        private readonly PendingLocalMatchRoom _room;
        private readonly MatchContext _context;
        private readonly IHubContext<MatchHub> _hubContext;
        private readonly BattleSnapshotBuilder _battleSnapshotBuilder;
        private readonly BuildWindowSnapshotBuilder _buildWindowSnapshotBuilder;
        private readonly ICollection<string> _battleLockedPlayerIds;
        private readonly IDictionary<string, long> _battleLockCooldownUntilUnixMsByPlayer;
        private readonly ILogger<MatchBroadcaster> _logger;

        public MatchBroadcaster(
            PendingLocalMatchRoom room,
            MatchContext context,
            IHubContext<MatchHub> hubContext,
            BattleSnapshotBuilder battleSnapshotBuilder,
            BuildWindowSnapshotBuilder buildWindowSnapshotBuilder,
            ICollection<string> battleLockedPlayerIds,
            IDictionary<string, long> battleLockCooldownUntilUnixMsByPlayer,
            ILogger<MatchBroadcaster> logger)
        {
            _room = room ?? throw new ArgumentNullException(nameof(room));
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _hubContext = hubContext ?? throw new ArgumentNullException(nameof(hubContext));
            _battleSnapshotBuilder = battleSnapshotBuilder ?? throw new ArgumentNullException(nameof(battleSnapshotBuilder));
            _buildWindowSnapshotBuilder = buildWindowSnapshotBuilder ?? throw new ArgumentNullException(nameof(buildWindowSnapshotBuilder));
            _battleLockedPlayerIds = battleLockedPlayerIds ?? throw new ArgumentNullException(nameof(battleLockedPlayerIds));
            _battleLockCooldownUntilUnixMsByPlayer = battleLockCooldownUntilUnixMsByPlayer ?? throw new ArgumentNullException(nameof(battleLockCooldownUntilUnixMsByPlayer));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public string MatchId => _context.MatchId;

        public async Task BroadcastMatchStartedAsync()
        {
            int battleIndex = GetCurrentBattleIndex();
            int totalBattleCount = _context.Ruleset.Steps.Count;

            await BroadcastToGroupAsync(MatchHubEventNames.MatchStarted, new MatchStartedMessage
            {
                MatchId = MatchId,
                BattleIndex = battleIndex,
                TotalBattleCount = totalBattleCount,
            });

            _logger.LogInformation("对局 {MatchId} 开始第 {BattleIndex}/{TotalBattleCount} 场战斗。", MatchId, battleIndex, totalBattleCount);
        }

        public async Task BroadcastBattlePhaseAndSnapshotAsync()
        {
            if (_context.CurrentPhase == MatchPhase.BuildWindow)
            {
                await BroadcastPhaseAsync(ServerPhaseKind.BuildWindow);
                return;
            }

            var phaseKind = _context.ActiveBattleContext?.CurrentPhase == BattleContext.BattlePhase.Settlement
                ? ServerPhaseKind.BattleSettlement
                : ServerPhaseKind.BattleOperation;

            await BroadcastPhaseAsync(phaseKind);

            foreach (var participant in _room.Participants)
            {
                if (string.IsNullOrWhiteSpace(participant.ConnectionId))
                    continue;

                var snapshot = _battleSnapshotBuilder.Build(
                    _context,
                    participant.PlayerId,
                    _battleLockedPlayerIds,
                    _battleLockCooldownUntilUnixMsByPlayer);

                await _hubContext.Clients.Client(participant.ConnectionId)
                    .SendAsync(MatchHubEventNames.BattleSnapshot, new BattleSnapshotMessage
                    {
                        Snapshot = snapshot,
                    });
            }
        }

        public async Task BroadcastBuildWindowOpenedAsync()
        {
            foreach (var participant in _room.Participants)
            {
                if (string.IsNullOrWhiteSpace(participant.ConnectionId))
                    continue;

                var dto = _buildWindowSnapshotBuilder.Build(_context, _room, participant.PlayerId);
                await _hubContext.Clients.Client(participant.ConnectionId)
                    .SendAsync(MatchHubEventNames.BuildWindowOpened, new BuildWindowOpenedMessage
                    {
                        BuildWindow = dto,
                    });
            }

            _logger.LogInformation("对局 {MatchId} 打开构筑窗口。", MatchId);
        }

        public async Task BroadcastBuildWindowUpdatedAsync()
        {
            foreach (var participant in _room.Participants)
            {
                if (string.IsNullOrWhiteSpace(participant.ConnectionId))
                    continue;

                var dto = _buildWindowSnapshotBuilder.Build(_context, _room, participant.PlayerId);
                await _hubContext.Clients.Client(participant.ConnectionId)
                    .SendAsync(MatchHubEventNames.BuildWindowUpdated, new BuildWindowUpdatedMessage
                    {
                        BuildWindow = dto,
                    });
            }
        }

        public async Task BroadcastBuildWindowClosedAsync()
        {
            await BroadcastToGroupAsync(MatchHubEventNames.BuildWindowClosed, new BuildWindowClosedMessage
            {
                MatchId = MatchId,
            });

            _logger.LogInformation("对局 {MatchId} 关闭构筑窗口。", MatchId);
        }

        public async Task BroadcastBattleEndedAsync(BattleSummary summary)
        {
            var result = new BattleResultDto
            {
                MatchId = MatchId,
                BattleId = summary.BattleId,
                BattleIndex = GetCurrentBattleIndex(),
                TotalBattleCount = _context.Ruleset.Steps.Count,
                BattleEndReason = ToProtocolBattleEndReason(summary.BattleEndReason),
                MatchTerminated = summary.MatchTerminated,
                MatchEndReason = ToProtocolMatchEndReason(summary.MatchEndReason),
                WinningTeamId = summary.WinningTeamId,
                DestroyedObjectiveEntityId = summary.DestroyedObjectiveEntityId,
            };

            foreach (var deadPlayerId in summary.DeadPlayerIds)
                result.DeadPlayerIds.Add(deadPlayerId);

            await BroadcastToGroupAsync(MatchHubEventNames.BattleEnded, new BattleEndedMessage
            {
                Result = result,
            });

            _logger.LogInformation(
                "对局 {MatchId} 第 {BattleIndex}/{TotalBattleCount} 场结束。原因={BattleEndReason} MatchTerminated={MatchTerminated} WinnerTeam={WinningTeamId}",
                MatchId,
                result.BattleIndex,
                result.TotalBattleCount,
                result.BattleEndReason,
                result.MatchTerminated,
                result.WinningTeamId ?? "<none>");
        }

        public async Task BroadcastMatchEndedAsync()
        {
            await BroadcastToGroupAsync(MatchHubEventNames.MatchEnded, new MatchEndedMessage
            {
                MatchId = MatchId,
                WinnerTeamId = _context.WinnerTeamId,
            });

            _logger.LogInformation("对局 {MatchId} 已结束。WinnerTeam={WinnerTeamId}", MatchId, _context.WinnerTeamId ?? "<draw>");
        }

        public async Task BroadcastPhaseAsync(ServerPhaseKind phaseKind)
        {
            int battleIndex = GetCurrentBattleIndex();
            int totalBattleCount = _context.Ruleset.Steps.Count;

            await BroadcastToGroupAsync(MatchHubEventNames.PhaseChanged, new PhaseChangedMessage
            {
                MatchId = MatchId,
                PhaseKind = phaseKind,
                BattleIndex = battleIndex,
                TotalBattleCount = totalBattleCount,
                DeadlineUnixMs = phaseKind == ServerPhaseKind.BuildWindow
                    ? _context.ActiveBuildWindow?.DeadlineUnixMs ?? 0
                    : 0,
            });

            _logger.LogInformation("对局 {MatchId} 阶段切换为 {PhaseKind}，当前战斗 {BattleIndex}/{TotalBattleCount}。", MatchId, phaseKind, battleIndex, totalBattleCount);
        }

        public async Task RejectAsync(string playerId, string actionName, ProtocolActionErrorCode errorCode, string reason)
        {
            var participant = _room.Participants.FirstOrDefault(item => string.Equals(item.PlayerId, playerId, StringComparison.Ordinal));
            if (participant == null || string.IsNullOrWhiteSpace(participant.ConnectionId))
                return;

            await _hubContext.Clients.Client(participant.ConnectionId)
                .SendAsync(MatchHubEventNames.ActionRejected, new ActionRejectedMessage
                {
                    MatchId = MatchId,
                    ActionName = actionName,
                    ErrorCode = errorCode,
                    Reason = reason,
                });

            _logger.LogWarning(
                "对局 {MatchId} 拒绝玩家 {PlayerId} 的动作 {ActionName}。ErrorCode={ErrorCode} Reason={Reason}",
                MatchId,
                playerId,
                actionName,
                errorCode,
                reason);
        }

        private Task BroadcastToGroupAsync(string eventName, object payload)
        {
            return _hubContext.Clients.Group(MatchId).SendAsync(eventName, payload);
        }

        private int GetCurrentBattleIndex()
        {
            if (_context.Ruleset.Steps.Count <= 0)
                return 0;

            return Math.Min(_context.CurrentStepIndex + 1, _context.Ruleset.Steps.Count);
        }

        private static ProtocolBattleEndReason ToProtocolBattleEndReason(BattleEndReason reason)
        {
            return reason switch
            {
                BattleEndReason.RoundLimitReached => ProtocolBattleEndReason.RoundLimitReached,
                BattleEndReason.TeamEliminated => ProtocolBattleEndReason.TeamEliminated,
                BattleEndReason.ObjectiveDestroyed => ProtocolBattleEndReason.ObjectiveDestroyed,
                _ => ProtocolBattleEndReason.None,
            };
        }

        private static ProtocolMatchEndReason ToProtocolMatchEndReason(MatchEndReason reason)
        {
            return reason switch
            {
                MatchEndReason.ObjectiveDestroyed => ProtocolMatchEndReason.ObjectiveDestroyed,
                _ => ProtocolMatchEndReason.None,
            };
        }
    }
}
