namespace CardMoba.MatchFlow.Deck
{
    public sealed class PersistentDeckCard
    {
        public string PersistentCardId { get; set; } = string.Empty;
        public string BaseConfigId { get; set; } = string.Empty;
        public string CurrentConfigId { get; set; } = string.Empty;
        public int UpgradeLevel { get; set; }

        public string GetEffectiveConfigId()
        {
            return string.IsNullOrWhiteSpace(CurrentConfigId) ? BaseConfigId : CurrentConfigId;
        }

        public PersistentDeckCard Clone()
        {
            return new PersistentDeckCard
            {
                PersistentCardId = PersistentCardId,
                BaseConfigId = BaseConfigId,
                CurrentConfigId = CurrentConfigId,
                UpgradeLevel = UpgradeLevel,
            };
        }
    }
}