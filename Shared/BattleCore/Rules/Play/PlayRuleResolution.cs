namespace CardMoba.BattleCore.Rules.Play
{
    public sealed class PlayRuleResolution
    {
        public bool Allowed { get; set; } = true;
        public string BlockReason { get; set; } = string.Empty;
        public int? CostSetTo { get; set; }
        public bool ForceConsumeAfterResolve { get; set; }
        public bool ConsumeCorruptionChargeOnSuccess { get; set; }
        public bool HitCorruption { get; set; }
    }
}
