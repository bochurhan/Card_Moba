using System;
using System.Collections.Concurrent;
using CardMoba.Server.Host.Config;
using CardMoba.Server.Host.Sessions;
using Microsoft.Extensions.Logging;

namespace CardMoba.Server.Host.Services
{
    /// <summary>
    /// 管理已启动的权威对局会话。
    /// </summary>
    public sealed class MatchSessionManager
    {
        private readonly ConcurrentDictionary<string, MatchSession> _sessions = new ConcurrentDictionary<string, MatchSession>(StringComparer.Ordinal);
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<MatchSessionManager> _logger;

        public MatchSessionManager(IServiceProvider serviceProvider, ILogger<MatchSessionManager> logger)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
        }

        public MatchSession Create(PendingLocalMatchRoom room)
        {
            var session = ActivatorUtilities.CreateInstance<MatchSession>(_serviceProvider, room);
            _sessions[session.MatchId] = session;
            _logger.LogInformation("已创建对局会话 {MatchId}。", session.MatchId);
            return session;
        }

        public MatchSession? Get(string matchId)
        {
            return _sessions.TryGetValue(matchId, out var session) ? session : null;
        }

        public void Remove(string matchId)
        {
            _sessions.TryRemove(matchId, out _);
            _logger.LogInformation("已移除对局会话 {MatchId}。", matchId);
        }
    }
}
