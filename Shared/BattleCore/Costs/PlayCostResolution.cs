namespace CardMoba.BattleCore.Costs
{
    /// <summary>
    /// 出牌前统一费用解析结果。
    /// BaseCost 表示当前有效配置的基础费用；
    /// FinalCost 表示应用规则后的最终费用。
    /// </summary>
    public sealed class PlayCostResolution
    {
        public int BaseCost { get; set; }
        public int FinalCost { get; set; }

        /// <summary>
        /// 本次是否命中腐化名额。
        /// 命中后费用变为 0，且结算后进入 Consume。
        /// </summary>
        public bool HitCorruption { get; set; }

        public bool ConsumesCorruptionCharge { get; set; }
        public bool ForceConsumeAfterResolve { get; set; }
    }
}
