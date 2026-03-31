using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CardMoba.MatchFlow.Context;
using CardMoba.MatchFlow.Core;
using CardMoba.Protocol.Messages.Requests;
using CardMoba.Server.Host.Config;
using CardMoba.Server.Host.Hubs;
using CardMoba.Server.Host.Services;
using CardMoba.Server.Host.Snapshots;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;

namespace CardMoba.Server.Host.Sessions
{
    /// <summary>
    /// 单个 localhost 对局的权威会话宿主。
    /// 负责持有运行时状态、串行化命令，并协调广播器与命令分发器。
    /// </summary>
    public sealed class MatchSession
    {
        private readonly PendingLocalMatchRoom _room;
        private readonly SemaphoreSlim _gate = new SemaphoreSlim(1, 1);
        private readonly HashSet<string> _battleLockedPlayerIds = new HashSet<string>(StringComparer.Ordinal);
        private readonly Dictionary<string, long> _battleLockCooldownUntilUnixMsByPlayer = new Dictionary<string, long>(StringComparer.Ordinal);
        private readonly HashSet<string> _disconnectedPlayerIds = new HashSet<string>(StringComparer.Ordinal);
        private readonly ILogger<MatchSession> _logger;

        private readonly MatchContext _context;
        private readonly MatchManager _matchManager;
        private readonly MatchBroadcaster _broadcaster;
        private readonly MatchCommandDispatcher _commandDispatcher;

        public MatchSession(
            PendingLocalMatchRoom room,
            IHubContext<MatchHub> hubContext,
            LocalMatchTemplateFactory templateFactory,
            ServerBattleFactoryFactory battleFactoryFactory,
            ServerBuildCatalogFactory buildCatalogFactory,
            BattleSnapshotBuilder battleSnapshotBuilder,
            BuildWindowSnapshotBuilder buildWindowSnapshotBuilder,
            ILogger<MatchSession> logger,
            ILogger<MatchBroadcaster> broadcasterLogger,
            ILogger<MatchCommandDispatcher> commandDispatcherLogger)
        {
            if (room == null) throw new ArgumentNullException(nameof(room));
            if (hubContext == null) throw new ArgumentNullException(nameof(hubContext));
            if (templateFactory == null) throw new ArgumentNullException(nameof(templateFactory));
            if (battleFactoryFactory == null) throw new ArgumentNullException(nameof(battleFactoryFactory));
            if (buildCatalogFactory == null) throw new ArgumentNullException(nameof(buildCatalogFactory));
            if (battleSnapshotBuilder == null) throw new ArgumentNullException(nameof(battleSnapshotBuilder));
            if (buildWindowSnapshotBuilder == null) throw new ArgumentNullException(nameof(buildWindowSnapshotBuilder));

            _room = room;
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));

            var battleFactory = battleFactoryFactory.Create();
            var buildCatalog = buildCatalogFactory.Create();

            _context = templateFactory.CreateLocalMatch(room.MatchId, room);
            _matchManager = new MatchManager(battleFactory, buildCatalog: buildCatalog);
            _broadcaster = new MatchBroadcaster(
                room,
                _context,
                hubContext,
                battleSnapshotBuilder,
                buildWindowSnapshotBuilder,
                _battleLockedPlayerIds,
                _battleLockCooldownUntilUnixMsByPlayer,
                broadcasterLogger);
            _commandDispatcher = new MatchCommandDispatcher(
                _context,
                _matchManager,
                _broadcaster,
                _battleLockedPlayerIds,
                _battleLockCooldownUntilUnixMsByPlayer,
                _disconnectedPlayerIds,
                commandDispatcherLogger);
        }

        public string MatchId => _context.MatchId;

        public async Task StartAsync()
        {
            await ExecuteAsync(async () =>
            {
                _logger.LogInformation("对局 {MatchId} 初始化完成，准备开始。", MatchId);
                _matchManager.StartMatch(_context);
                _commandDispatcher.BeginActiveBattleRoundIfNeeded();
                await _broadcaster.BroadcastMatchStartedAsync();
                await _broadcaster.BroadcastBattlePhaseAndSnapshotAsync();
            });
        }

        public Task HandlePlayInstantCardAsync(string playerId, PlayInstantCardRequest request)
        {
            return ExecuteAsync(() => _commandDispatcher.HandlePlayInstantCardAsync(playerId, request));
        }

        public Task HandleCommitPlanCardAsync(string playerId, CommitPlanCardRequest request)
        {
            return ExecuteAsync(() => _commandDispatcher.HandleCommitPlanCardAsync(playerId, request));
        }

        public Task HandleEndTurnAsync(string playerId)
        {
            return ExecuteAsync(() => _commandDispatcher.HandleEndTurnAsync(playerId));
        }

        public Task HandleSetBattleTurnLockAsync(string playerId, SetBattleTurnLockRequest request)
        {
            return ExecuteAsync(() => _commandDispatcher.HandleSetBattleTurnLockAsync(playerId, request.IsLocked));
        }

        public Task HandleSubmitBuildChoiceAsync(string playerId, SubmitBuildChoiceRequest request)
        {
            return ExecuteAsync(() => _commandDispatcher.HandleSubmitBuildChoiceAsync(playerId, request));
        }

        public Task HandleLockBuildWindowAsync(string playerId)
        {
            return ExecuteAsync(() => _commandDispatcher.HandleLockBuildWindowAsync(playerId));
        }

        public Task<bool> HandleDisconnectAsync(string playerId)
        {
            return ExecuteAsync(async () =>
            {
                _logger.LogInformation("对局 {MatchId} 玩家 {PlayerId} 断开连接。", MatchId, playerId);
                MarkPlayerDisconnected(playerId);
                if (!HasConnectedParticipants())
                {
                    _logger.LogInformation("对局 {MatchId} 所有玩家都已离线，将回收会话。", MatchId);
                    return true;
                }

                await _commandDispatcher.HandlePlayerDisconnectedAsync(playerId);
                return false;
            });
        }

        private async Task ExecuteAsync(Func<Task> action)
        {
            await _gate.WaitAsync();
            try
            {
                await action();
            }
            finally
            {
                _gate.Release();
            }
        }

        private async Task<T> ExecuteAsync<T>(Func<Task<T>> action)
        {
            await _gate.WaitAsync();
            try
            {
                return await action();
            }
            finally
            {
                _gate.Release();
            }
        }

        private void MarkPlayerDisconnected(string playerId)
        {
            _disconnectedPlayerIds.Add(playerId);
            var participant = _room.Participants.FirstOrDefault(item => string.Equals(item.PlayerId, playerId, StringComparison.Ordinal));
            if (participant != null)
                participant.ConnectionId = string.Empty;
        }

        private bool HasConnectedParticipants()
        {
            return _room.Participants.Any(item => !string.IsNullOrWhiteSpace(item.ConnectionId));
        }
    }
}
