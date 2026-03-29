using System;
using System.Collections.Generic;
using System.Linq;
using CardMoba.ConfigModels.Card;
using CardMoba.MatchFlow.Definitions;
using CardMoba.Protocol.Enums;

namespace CardMoba.MatchFlow.Catalog
{
    public sealed class BuildCatalogAssemblerOptions
    {
        public bool RegisterDefaultClassPools { get; set; } = true;
        public Func<CardConfig, bool>? RewardFilter { get; set; }
        public Func<CardConfig, bool>? RemoveFilter { get; set; }
    }

    public sealed class BuildCatalogAssembler
    {
        public InMemoryBuildCatalog Create(
            IEnumerable<CardConfig> cardConfigs,
            IEnumerable<EquipmentDefinition>? equipmentDefinitions = null,
            BuildCatalogAssemblerOptions? options = null)
        {
            if (cardConfigs == null)
                throw new ArgumentNullException(nameof(cardConfigs));

            options ??= new BuildCatalogAssemblerOptions();
            var cards = cardConfigs.ToList();
            var upgradedTargets = new HashSet<string>(
                cards
                    .Where(card => !string.IsNullOrWhiteSpace(card.UpgradedCardConfigId))
                    .Select(card => card.UpgradedCardConfigId),
                StringComparer.Ordinal);

            var catalog = new InMemoryBuildCatalog();
            foreach (var card in cards)
                catalog.AddCardDefinition(ToDefinition(card, upgradedTargets, options));

            if (equipmentDefinitions != null)
            {
                foreach (var equipment in equipmentDefinitions)
                    catalog.AddEquipmentDefinition(equipment);
            }

            if (options.RegisterDefaultClassPools)
            {
                foreach (var card in cards)
                {
                    if (!ShouldAppearInRewards(card, upgradedTargets, options))
                        continue;

                    var definition = catalog.GetCardDefinition(card.CardId.ToString());
                    if (definition == null)
                        continue;

                    if (definition.ClassId == HeroClass.Universal)
                    {
                        foreach (var heroClass in GetDraftableHeroClasses())
                            catalog.AddPoolCard(GetDefaultPoolId(heroClass), definition.ConfigId);
                    }
                    else
                    {
                        catalog.AddPoolCard(GetDefaultPoolId(definition.ClassId), definition.ConfigId);
                    }
                }
            }

            return catalog;
        }

        public static string GetDefaultPoolId(HeroClass heroClass)
        {
            return $"class:{heroClass}";
        }

        private static BuildCardDefinition ToDefinition(
            CardConfig card,
            ISet<string> upgradedTargets,
            BuildCatalogAssemblerOptions options)
        {
            return new BuildCardDefinition
            {
                ConfigId = card.CardId.ToString(),
                ClassId = card.HeroClass,
                Rarity = MapRarity(card),
                CanAppearInBuildReward = ShouldAppearInRewards(card, upgradedTargets, options),
                CanRemove = options.RemoveFilter?.Invoke(card) ?? !card.HasTag(CardTag.Status),
                UpgradedConfigId = card.UpgradedCardConfigId,
            };
        }

        private static bool ShouldAppearInRewards(
            CardConfig card,
            ISet<string> upgradedTargets,
            BuildCatalogAssemblerOptions options)
        {
            if (options.RewardFilter != null)
                return options.RewardFilter(card);

            if (card.IsLegendary || card.HasTag(CardTag.Status))
                return false;
            if (upgradedTargets.Contains(card.CardId.ToString()))
                return false;

            return true;
        }

        private static BuildCardRarity MapRarity(CardConfig card)
        {
            if (card.IsLegendary || card.Rarity >= 4)
                return BuildCardRarity.Legendary;
            if (card.Rarity >= 3)
                return BuildCardRarity.Rare;
            if (card.Rarity >= 2)
                return BuildCardRarity.Uncommon;
            return BuildCardRarity.Common;
        }

        private static IEnumerable<HeroClass> GetDraftableHeroClasses()
        {
            return Enum.GetValues(typeof(HeroClass))
                .Cast<HeroClass>()
                .Where(heroClass => heroClass != HeroClass.Universal);
        }
    }
}
