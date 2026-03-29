using System;
using CardMoba.Protocol.Enums;

namespace CardMoba.MatchFlow.Definitions
{
    public enum BuildCardRarity
    {
        Common = 1,
        Uncommon = 2,
        Rare = 3,
        Legendary = 4,
    }

    public sealed class BuildCardDefinition
    {
        public string ConfigId { get; set; } = string.Empty;
        public HeroClass ClassId { get; set; } = HeroClass.Universal;
        public BuildCardRarity Rarity { get; set; } = BuildCardRarity.Common;
        public bool CanAppearInBuildReward { get; set; } = true;
        public bool CanRemove { get; set; } = true;
        public string UpgradedConfigId { get; set; } = string.Empty;

        public bool CanUpgrade => !string.IsNullOrWhiteSpace(UpgradedConfigId);
        public bool IsLegendary => Rarity == BuildCardRarity.Legendary;
    }

    public enum EquipmentEffectType
    {
        None = 0,
        HealAfterBattleFlat = 1,
    }

    public sealed class EquipmentDefinition
    {
        public string EquipmentId { get; set; } = string.Empty;
        public HeroClass ClassId { get; set; } = HeroClass.Universal;
        public EquipmentEffectType EffectType { get; set; }
        public int EffectValue { get; set; }
    }
}
