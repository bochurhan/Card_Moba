using System.Collections.Generic;
using CardMoba.MatchFlow.Rules;

namespace CardMoba.MatchFlow.Results
{
    public sealed class BuildWindowSummary
    {
        public int StepIndex { get; set; }
        public Dictionary<string, BuildActionType> ChosenActionsByPlayer { get; } = new Dictionary<string, BuildActionType>();
    }
}