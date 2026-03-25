#pragma warning disable CS8632

using System.Collections.Generic;

namespace CardMoba.BattleCore.Foundation
{
    /// <summary>
    /// 触发器纯数据定义。
    /// TriggerManager 负责持有、过滤、排序，并把 Effects 推入 PendingQueue。
    /// </summary>
    public class TriggerUnit
    {
        public string TriggerId { get; set; } = string.Empty;
        public string TriggerName { get; set; } = string.Empty;

        public string OwnerPlayerId { get; set; } = string.Empty;
        public string SourceId { get; set; } = string.Empty;

        public TriggerTiming Timing { get; set; }
        public int Priority { get; set; } = 500;
        public int RemainingTriggers { get; set; } = -1;
        public int RemainingRounds { get; set; } = -1;

        public List<string> Conditions { get; set; } = new List<string>();

        // 当前主路径只支持通过 Effects 入队执行，不再支持内联回调。
        public List<EffectUnit> Effects { get; set; } = new List<EffectUnit>();
    }
}
