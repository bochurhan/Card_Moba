namespace CardMoba.MatchFlow.Rules
{
    public enum BuildWindowRestrictionMode
    {
        None = 0,
        ForcedRecovery = 1,
    }

    public sealed class BuildWindowRules
    {
        public int BaseOpportunityCount { get; set; } = 1;
        public float DefaultHealPercent { get; set; } = 0.30f;
        public float ForcedRecoveryHealPercent { get; set; } = 0.40f;
        public int DraftGroupCount { get; set; } = 2;
        public int DraftOptionsPerGroup { get; set; } = 3;
        public float UpgradedDraftChance { get; set; } = 0.10f;
        public int CommonWeight { get; set; } = 6;
        public int UncommonWeight { get; set; } = 3;
        public int RareWeight { get; set; } = 1;
    }
}
