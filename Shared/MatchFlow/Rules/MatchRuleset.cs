using System;
using System.Collections.Generic;

namespace CardMoba.MatchFlow.Rules
{
    public sealed class MatchRuleset
    {
        public List<MatchBattleStep> Steps { get; } = new List<MatchBattleStep>();
        public int BuildWindowTimeoutMs { get; set; } = 30000;
        public BuildActionType DefaultTimeoutAction { get; set; } = BuildActionType.Heal;
        public bool AllowEarlyAdvanceWhenAllLocked { get; set; } = true;
        public BuildWindowRules BuildWindowRules { get; set; } = new BuildWindowRules();

        public MatchBattleStep GetStepOrThrow(int stepIndex)
        {
            if (stepIndex < 0 || stepIndex >= Steps.Count)
                throw new InvalidOperationException($"Match step index out of range: {stepIndex}.");

            return Steps[stepIndex];
        }
    }
}
