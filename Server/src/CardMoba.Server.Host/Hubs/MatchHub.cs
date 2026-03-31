using System.Linq;
using System.Threading.Tasks;
using CardMoba.Protocol.Hub;
using CardMoba.Protocol.Messages.Messages;
using CardMoba.Protocol.Messages.Requests;
using CardMoba.Server.Host.Services;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;

namespace CardMoba.Server.Host.Hubs
{
    /// <summary>
    /// localhost 对局入口 Hub。
    /// 当前只负责房间管理和命令转发，不承载权威状态。
    /// </summary>
    public sealed class MatchHub : Hub
    {
        private readonly LocalMatchRegistry _localMatchRegistry;
        private readonly MatchSessionManager _matchSessionManager;
        private readonly MatchConnectionRegistry _connectionRegistry;
        private readonly ILogger<MatchHub> _logger;

        public MatchHub(
            LocalMatchRegistry localMatchRegistry,
            MatchSessionManager matchSessionManager,
            MatchConnectionRegistry connectionRegistry,
            ILogger<MatchHub> logger)
        {
            _localMatchRegistry = localMatchRegistry;
            _matchSessionManager = matchSessionManager;
            _connectionRegistry = connectionRegistry;
            _logger = logger;
        }

        public async Task<MatchCreatedMessage> CreateLocalMatch(CreateLocalMatchRequest request)
        {
            var room = _localMatchRegistry.Create(request.DisplayName, Context.ConnectionId);
            _connectionRegistry.Bind(Context.ConnectionId, room.MatchId, room.Host.PlayerId);
            await Groups.AddToGroupAsync(Context.ConnectionId, room.MatchId);

            _logger.LogInformation("连接 {ConnectionId} 创建房间 {MatchId}，玩家 {PlayerId}。", Context.ConnectionId, room.MatchId, room.Host.PlayerId);

            return new MatchCreatedMessage
            {
                MatchId = room.MatchId,
                LocalPlayerId = room.Host.PlayerId,
                Participants =
                {
                    room.Host.ToDto(),
                },
            };
        }

        public async Task<MatchJoinedMessage> JoinLocalMatch(JoinLocalMatchRequest request)
        {
            var room = _localMatchRegistry.Join(request.MatchId, request.DisplayName, Context.ConnectionId);
            _connectionRegistry.Bind(Context.ConnectionId, room.MatchId, room.Guest!.PlayerId);
            await Groups.AddToGroupAsync(Context.ConnectionId, room.MatchId);

            var message = BuildJoinedMessage(room, room.Guest!.PlayerId);
            await Clients.Group(room.MatchId).SendAsync(MatchHubEventNames.MatchJoined, message);
            _logger.LogInformation("连接 {ConnectionId} 加入房间 {MatchId}，玩家 {PlayerId}。", Context.ConnectionId, room.MatchId, room.Guest.PlayerId);
            return message;
        }

        public async Task Ready(ReadyRequest request)
        {
            var binding = _connectionRegistry.Get(Context.ConnectionId);
            string playerId = binding?.PlayerId ?? string.Empty;
            var room = _localMatchRegistry.MarkReady(request.MatchId, playerId);
            var message = BuildJoinedMessage(room, playerId);
            await Clients.Group(request.MatchId).SendAsync(MatchHubEventNames.MatchJoined, message);
            _logger.LogInformation("房间 {MatchId} 玩家 {PlayerId} 已准备。", request.MatchId, playerId);

            if (room.IsReadyToStart)
            {
                var readyRoom = _localMatchRegistry.Take(request.MatchId);
                var session = _matchSessionManager.Create(readyRoom);
                _logger.LogInformation("房间 {MatchId} 双方均已准备，创建权威会话。", request.MatchId);
                await session.StartAsync();
            }
        }

        public async Task PlayInstantCard(PlayInstantCardRequest request)
        {
            if (TryResolveSession(request.MatchId, out var session, out var playerId))
                await session.HandlePlayInstantCardAsync(playerId, request);
        }

        public async Task CommitPlanCard(CommitPlanCardRequest request)
        {
            if (TryResolveSession(request.MatchId, out var session, out var playerId))
                await session.HandleCommitPlanCardAsync(playerId, request);
        }

        public async Task EndTurn(EndTurnRequest request)
        {
            if (TryResolveSession(request.MatchId, out var session, out var playerId))
                await session.HandleEndTurnAsync(playerId);
        }

        public async Task SetBattleTurnLock(SetBattleTurnLockRequest request)
        {
            if (TryResolveSession(request.MatchId, out var session, out var playerId))
                await session.HandleSetBattleTurnLockAsync(playerId, request);
        }

        public async Task SubmitBuildChoice(SubmitBuildChoiceRequest request)
        {
            if (TryResolveSession(request.MatchId, out var session, out var playerId))
                await session.HandleSubmitBuildChoiceAsync(playerId, request);
        }

        public async Task LockBuildWindow(LockBuildWindowRequest request)
        {
            if (TryResolveSession(request.MatchId, out var session, out var playerId))
                await session.HandleLockBuildWindowAsync(playerId);
        }

        public override async Task OnDisconnectedAsync(System.Exception? exception)
        {
            var binding = _connectionRegistry.Remove(Context.ConnectionId);
            if (binding != null)
            {
                _logger.LogInformation("连接 {ConnectionId} 已断开，Match={MatchId} Player={PlayerId}", Context.ConnectionId, binding.MatchId, binding.PlayerId);
                var activeSession = _matchSessionManager.Get(binding.MatchId);
                if (activeSession != null)
                {
                    bool shouldRemove = await activeSession.HandleDisconnectAsync(binding.PlayerId);
                    if (shouldRemove)
                        _matchSessionManager.Remove(binding.MatchId);
                }
                else
                {
                    _localMatchRegistry.RemoveConnection(Context.ConnectionId);
                }
            }

            await base.OnDisconnectedAsync(exception);
        }

        private bool TryResolveSession(string matchId, out Sessions.MatchSession session, out string playerId)
        {
            session = null!;
            playerId = string.Empty;

            var binding = _connectionRegistry.Get(Context.ConnectionId);
            if (binding == null || binding.MatchId != matchId)
                return false;

            var activeSession = _matchSessionManager.Get(matchId);
            if (activeSession == null)
                return false;

            session = activeSession;
            playerId = binding.PlayerId;
            return true;
        }

        private static MatchJoinedMessage BuildJoinedMessage(PendingLocalMatchRoom room, string localPlayerId)
        {
            var message = new MatchJoinedMessage
            {
                MatchId = room.MatchId,
                LocalPlayerId = localPlayerId,
            };

            foreach (var participant in room.Participants.OrderBy(item => item.PlayerId))
                message.Participants.Add(participant.ToDto());

            return message;
        }
    }
}
