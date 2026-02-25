using System.Collections.Generic;

namespace CardMoba.BattleCore.Context
{
    /// <summary>
    /// 分路状态 —— 一条战斗分路的所有可变数据。
    /// 
    /// 3v3 对战中有三条分路（上/中/下），每条路上有两个对位玩家。
    /// 分路独立结算，直到某路分出胜负或进入中枢塔/决战阶段。
    /// </summary>
    public class LaneState
    {
        /// <summary>
        /// 分路索引（0=上路, 1=中路, 2=下路）
        /// </summary>
        public int LaneIndex { get; set; }

        /// <summary>
        /// 分路名称（用于显示和日志）
        /// </summary>
        public string LaneName => LaneIndex switch
        {
            0 => "上路",
            1 => "中路",
            2 => "下路",
            _ => $"未知路{LaneIndex}"
        };

        /// <summary>
        /// 该分路上的两个玩家ID（一方一个）
        /// Players[0] = 队伍A的玩家, Players[1] = 队伍B的玩家
        /// </summary>
        public string[] PlayerIds { get; set; } = new string[2];

        /// <summary>
        /// 分路是否已结束（有一方玩家死亡或分路被放弃）
        /// </summary>
        public bool IsLaneEnded { get; set; }

        /// <summary>
        /// 分路获胜者的玩家ID（空字符串表示未决出胜负）
        /// </summary>
        public string WinnerPlayerId { get; set; } = string.Empty;

        /// <summary>
        /// 本回合在此分路打出的卡牌（用于分路内结算）
        /// </summary>
        public List<PlayedCard> PendingCards { get; set; } = new();

        /// <summary>
        /// 本分路的回合日志
        /// </summary>
        public List<string> LaneLog { get; set; } = new();

        // ── 便捷方法 ──

        /// <summary>
        /// 获取对位玩家（给定一个玩家ID，返回同路对手ID）
        /// </summary>
        public string GetOpponentId(string playerId)
        {
            if (PlayerIds[0] == playerId) return PlayerIds[1];
            if (PlayerIds[1] == playerId) return PlayerIds[0];
            return string.Empty;
        }

        /// <summary>
        /// 检查玩家是否在此分路
        /// </summary>
        public bool HasPlayer(string playerId)
        {
            return PlayerIds[0] == playerId || PlayerIds[1] == playerId;
        }

        /// <summary>
        /// 获取该分路上属于指定队伍的玩家ID
        /// </summary>
        public string GetTeamPlayerId(int teamIndex)
        {
            if (teamIndex < 0 || teamIndex > 1) return string.Empty;
            return PlayerIds[teamIndex];
        }

        /// <summary>
        /// 清理本回合临时数据
        /// </summary>
        public void ClearRoundData()
        {
            PendingCards.Clear();
            LaneLog.Clear();
        }
    }
}
