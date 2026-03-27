namespace CardMoba.BattleCore.Rules.Draw
{
    public sealed class DrawRuleResolution
    {
        public bool Allowed { get; set; } = true;
        public string BlockReason { get; set; } = string.Empty;
    }
}
