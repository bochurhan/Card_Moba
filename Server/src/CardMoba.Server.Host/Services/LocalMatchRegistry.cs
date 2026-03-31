using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using CardMoba.Protocol.Messages.Common;

namespace CardMoba.Server.Host.Services
{
    public sealed class PendingLocalMatchParticipant
    {
        public string PlayerId { get; set; } = string.Empty;
        public string TeamId { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public string ConnectionId { get; set; } = string.Empty;
        public bool IsReady { get; set; }

        public MatchParticipantDto ToDto()
        {
            return new MatchParticipantDto
            {
                PlayerId = PlayerId,
                TeamId = TeamId,
                DisplayName = DisplayName,
                IsReady = IsReady,
            };
        }
    }

    public sealed class PendingLocalMatchRoom
    {
        public string MatchId { get; set; } = string.Empty;
        public PendingLocalMatchParticipant Host { get; set; } = new PendingLocalMatchParticipant();
        public PendingLocalMatchParticipant? Guest { get; set; }

        public IEnumerable<PendingLocalMatchParticipant> Participants
        {
            get
            {
                yield return Host;
                if (Guest != null)
                    yield return Guest;
            }
        }

        public bool IsFull => Guest != null;
        public bool IsReadyToStart => IsFull && Participants.All(participant => participant.IsReady);
    }

    /// <summary>
    /// 管理尚未开始的 localhost 测试房间。
    /// </summary>
    public sealed class LocalMatchRegistry
    {
        public const string HostPlayerId = "player1";
        public const string GuestPlayerId = "player2";
        public const string HostTeamId = "team_player1";
        public const string GuestTeamId = "team_player2";

        private readonly ConcurrentDictionary<string, PendingLocalMatchRoom> _rooms = new ConcurrentDictionary<string, PendingLocalMatchRoom>(StringComparer.Ordinal);

        public PendingLocalMatchRoom Create(string displayName, string connectionId)
        {
            string matchId = $"match_{Guid.NewGuid():N}";
            var room = new PendingLocalMatchRoom
            {
                MatchId = matchId,
                Host = new PendingLocalMatchParticipant
                {
                    PlayerId = HostPlayerId,
                    TeamId = HostTeamId,
                    DisplayName = string.IsNullOrWhiteSpace(displayName) ? "玩家1" : displayName,
                    ConnectionId = connectionId,
                },
            };
            _rooms[matchId] = room;
            return room;
        }

        public PendingLocalMatchRoom Join(string matchId, string displayName, string connectionId)
        {
            if (!_rooms.TryGetValue(matchId, out var room))
                throw new InvalidOperationException($"未找到房间：{matchId}");
            if (room.Guest != null)
                throw new InvalidOperationException("房间已满。");

            room.Guest = new PendingLocalMatchParticipant
            {
                PlayerId = GuestPlayerId,
                TeamId = GuestTeamId,
                DisplayName = string.IsNullOrWhiteSpace(displayName) ? "玩家2" : displayName,
                ConnectionId = connectionId,
            };
            return room;
        }

        public PendingLocalMatchRoom MarkReady(string matchId, string playerId)
        {
            if (!_rooms.TryGetValue(matchId, out var room))
                throw new InvalidOperationException($"未找到房间：{matchId}");

            var participant = room.Participants.FirstOrDefault(item => string.Equals(item.PlayerId, playerId, StringComparison.Ordinal));
            if (participant == null)
                throw new InvalidOperationException($"房间 {matchId} 中不存在玩家：{playerId}");

            participant.IsReady = true;
            return room;
        }

        public PendingLocalMatchRoom? Get(string matchId)
        {
            return _rooms.TryGetValue(matchId, out var room) ? room : null;
        }

        public PendingLocalMatchRoom Take(string matchId)
        {
            if (!_rooms.TryRemove(matchId, out var room))
                throw new InvalidOperationException($"未找到待开始房间：{matchId}");
            return room;
        }

        public void RemoveConnection(string connectionId)
        {
            foreach (var entry in _rooms.Values)
            {
                if (entry.Host.ConnectionId == connectionId)
                {
                    _rooms.TryRemove(entry.MatchId, out _);
                    return;
                }

                if (entry.Guest?.ConnectionId == connectionId)
                {
                    entry.Guest = null;
                    return;
                }
            }
        }
    }
}
