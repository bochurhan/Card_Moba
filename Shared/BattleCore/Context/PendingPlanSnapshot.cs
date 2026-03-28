#pragma warning disable CS8632

using System.Collections.Generic;
using CardMoba.BattleCore.Foundation;

namespace CardMoba.BattleCore.Context
{
    /// <summary>
    /// 定策牌提交后的执行快照。
    /// 它不是 BattleCard 实例本身，而是回合末结算所需的冻结记录。
    /// </summary>
    public sealed class PendingPlanSnapshot
    {
        public string SnapshotId { get; set; } = string.Empty;
        public string PlayerId { get; set; } = string.Empty;
        public string SourceCardInstanceId { get; set; } = string.Empty;
        public string CommittedBaseConfigId { get; set; } = string.Empty;
        public string CommittedEffectiveConfigId { get; set; } = string.Empty;
        public int CommittedCost { get; set; }
        public int SubmitOrder { get; set; }
        public Dictionary<string, string> RuntimeParams { get; set; } = new Dictionary<string, string>();
        public Dictionary<string, string> FrozenInputs { get; set; } = new Dictionary<string, string>();
        public List<EffectUnit> Effects { get; set; } = new List<EffectUnit>();
        public List<EffectResult> PriorResults { get; set; } = new List<EffectResult>();
        public bool IsCountered { get; set; }
    }
}
