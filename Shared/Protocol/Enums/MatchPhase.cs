namespace CardMoba.Protocol.Enums
{
    /// <summary>
    /// 比赛阶段枚举 —— 对应设计文档的对局流程。
    /// 
    /// 3v3 模式完整流程：
    ///   分路对线期1 → 中枢塔1 → 分路对线期2 → 中枢塔2 → 全局决战期
    /// </summary>
    public enum MatchPhase
    {
        /// <summary>第一分路对线期（1+2固定分路）</summary>
        LanePhase1 = 1,

        /// <summary>第一中枢塔（PVE Boss战）</summary>
        CentralTower1 = 2,

        /// <summary>第二分路对线期</summary>
        LanePhase2 = 3,

        /// <summary>第二中枢塔（PVE Boss战）</summary>
        CentralTower2 = 4,

        /// <summary>全局决战死斗期（3v3 同屏）</summary>
        FinalBattle = 5,
    }
}
