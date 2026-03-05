
#pragma warning disable CS8632

using System.Collections.Generic;
using CardMoba.BattleCore.Foundation;

namespace CardMoba.BattleCore.Context
{
    /// <summary>
    /// 分路状态（LaneData）—— 记录一条分路的战场状态。
    /// 3v3 模式下每条分路有独立的 LaneData。
    /// </summary>
    public class LaneData
    {
        /// <summary>分路 ID（如 "top", "mid", "bot"）</summary>
        public string LaneId { get; set; } = string.Empty;

        /// <summary>此分路上的我方玩家 ID 列表</summary>
        public List<string> FriendlyPlayerIds { get; set; } = new List<string>();

        /// <summary>此分路上的敌方玩家 ID 列表</summary>
        public List<string> EnemyPlayerIds { get; set; } = new List<string>();

        /// <summary>分路特定的回合日志</summary>
        public List<string> LaneLog { get; set; } = new List<string>();
    }
}
