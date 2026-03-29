using System.Collections.Generic;
using CardMoba.BattleCore.Core;

namespace CardMoba.MatchFlow.Rules
{
    public sealed class MatchBattleStep
    {
        public string StepId { get; set; } = string.Empty;
        public BattleStepMode Mode { get; set; } = BattleStepMode.Duel1v1;
        public BattleRuleset BattleRuleset { get; set; } = new BattleRuleset();
        public bool OpensBuildWindowAfter { get; set; }
        public string? BuildPoolId { get; set; }
        public List<string> ParticipantPlayerIds { get; } = new List<string>();
        public List<ObjectiveSetupData> Objectives { get; } = new List<ObjectiveSetupData>();
    }
}
