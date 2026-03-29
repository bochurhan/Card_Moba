namespace CardMoba.BattleCore.Core
{
    /// <summary>单场战斗模式。</summary>
    public enum BattleMode
    {
        Duel1v1 = 1,
        Team2v2 = 2,
    }

    /// <summary>单场战斗的本地结束策略。</summary>
    public enum BattleLocalEndPolicy
    {
        TeamElimination = 1,
        RoundLimit = 2,
    }

    /// <summary>整局对局的终止策略。</summary>
    public enum MatchTerminalPolicy
    {
        None = 0,
        ObjectiveDestroyed = 1,
    }

    /// <summary>
    /// 单个 BattleCore 实例使用的规则集。
    /// 这里只描述单场战斗，不负责整局多场对局流程。
    /// </summary>
    public sealed class BattleRuleset
    {
        public BattleMode Mode { get; set; } = BattleMode.Duel1v1;
        public int MaxRounds { get; set; } = 99;
        public BattleLocalEndPolicy LocalEndPolicy { get; set; } = BattleLocalEndPolicy.TeamElimination;
        public MatchTerminalPolicy MatchTerminalPolicy { get; set; } = MatchTerminalPolicy.None;
        public bool EnableObjectives { get; set; } = false;
        public bool DeadPlayersCanAct { get; set; } = false;
        public bool DeadPlayersDrawCards { get; set; } = false;
    }
}
