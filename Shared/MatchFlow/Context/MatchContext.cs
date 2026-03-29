using System.Collections.Generic;
using CardMoba.BattleCore.Context;
using CardMoba.BattleCore.Core;
using CardMoba.MatchFlow.Core;
using CardMoba.MatchFlow.Rules;

namespace CardMoba.MatchFlow.Context
{
    public sealed class MatchContext
    {
        public string MatchId { get; set; } = string.Empty;
        public int BaseRandomSeed { get; set; }
        public MatchPhase CurrentPhase { get; set; } = MatchPhase.NotStarted;
        public int CurrentStepIndex { get; set; }
        public MatchRuleset Ruleset { get; set; } = new MatchRuleset();
        public Dictionary<string, PlayerMatchState> Players { get; } = new Dictionary<string, PlayerMatchState>();
        public Dictionary<string, TeamMatchState> Teams { get; } = new Dictionary<string, TeamMatchState>();
        public BattleContext? ActiveBattleContext { get; set; }
        public RoundManager? ActiveRoundManager { get; set; }
        public BuildWindowState? ActiveBuildWindow { get; set; }
        public List<IEquipmentBattleRuntime> ActiveEquipmentRuntimes { get; } = new List<IEquipmentBattleRuntime>();
        public bool IsMatchOver { get; set; }
        public string? WinnerTeamId { get; set; }
        public List<string> MatchLog { get; } = new List<string>();
    }
}
