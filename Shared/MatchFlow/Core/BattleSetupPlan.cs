using System.Collections.Generic;
using CardMoba.BattleCore.Core;

namespace CardMoba.MatchFlow.Core
{
    public sealed class BattleSetupPlan
    {
        public string BattleId { get; set; } = string.Empty;
        public int BattleSeed { get; set; }
        public BattleRuleset BattleRuleset { get; set; } = new BattleRuleset();
        public List<PlayerSetupData> Players { get; } = new List<PlayerSetupData>();
        public List<ObjectiveSetupData> Objectives { get; } = new List<ObjectiveSetupData>();
    }
}