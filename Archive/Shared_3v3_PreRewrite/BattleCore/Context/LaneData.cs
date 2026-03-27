#pragma warning disable CS8632

using System.Collections.Generic;

namespace CardMoba.BattleCore.Context
{
    /// <summary>
    /// 3v3 分路系统的历史预留模型。
    /// 当前 1v1 主流程已不再使用；保留于 Archive，等待后续分路系统重写时参考。
    /// </summary>
    public class LaneData
    {
        public string LaneId { get; set; } = string.Empty;
        public List<string> FriendlyPlayerIds { get; set; } = new List<string>();
        public List<string> EnemyPlayerIds { get; set; } = new List<string>();
        public List<string> LaneLog { get; set; } = new List<string>();
    }
}
