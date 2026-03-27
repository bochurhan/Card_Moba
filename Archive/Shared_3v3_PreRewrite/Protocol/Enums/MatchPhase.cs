namespace CardMoba.Protocol.Enums
{
    /// <summary>
    /// 3v3 比赛阶段的历史预留枚举。
    /// 当前 1v1 BattleCore 主流程已不再使用；保留于 Archive，等待后续阶段系统重写时参考。
    /// </summary>
    public enum MatchPhase
    {
        LanePhase1 = 1,
        CentralTower1 = 2,
        LanePhase2 = 3,
        CentralTower2 = 4,
        FinalBattle = 5,
    }
}
