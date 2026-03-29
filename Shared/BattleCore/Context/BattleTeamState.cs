using System.Collections.Generic;

namespace CardMoba.BattleCore.Context
{
    /// <summary>
    /// 单场战斗内的队伍运行时状态。
    /// 当前只保留最小共享信息：成员列表与一个共享目标。
    /// </summary>
    public sealed class BattleTeamState
    {
        public string TeamId { get; set; } = string.Empty;
        public List<string> PlayerIds { get; } = new List<string>();
        public string? ObjectiveEntityId { get; set; }
    }
}
