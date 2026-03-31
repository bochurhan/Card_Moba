using System.Collections.Concurrent;

namespace CardMoba.Server.Host.Services
{
    public sealed class MatchConnectionBinding
    {
        public string ConnectionId { get; set; } = string.Empty;
        public string MatchId { get; set; } = string.Empty;
        public string PlayerId { get; set; } = string.Empty;
    }

    /// <summary>
    /// 维护 SignalR 连接和玩家/房间的绑定关系。
    /// MVP 阶段仅支持单连接单玩家。
    /// </summary>
    public sealed class MatchConnectionRegistry
    {
        private readonly ConcurrentDictionary<string, MatchConnectionBinding> _bindings = new ConcurrentDictionary<string, MatchConnectionBinding>();

        public void Bind(string connectionId, string matchId, string playerId)
        {
            _bindings[connectionId] = new MatchConnectionBinding
            {
                ConnectionId = connectionId,
                MatchId = matchId,
                PlayerId = playerId,
            };
        }

        public MatchConnectionBinding? Remove(string connectionId)
        {
            return _bindings.TryRemove(connectionId, out var binding) ? binding : null;
        }

        public MatchConnectionBinding? Get(string connectionId)
        {
            return _bindings.TryGetValue(connectionId, out var binding) ? binding : null;
        }
    }
}
