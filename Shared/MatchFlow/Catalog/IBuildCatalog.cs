using System.Collections.Generic;
using CardMoba.MatchFlow.Definitions;
using CardMoba.Protocol.Enums;

namespace CardMoba.MatchFlow.Catalog
{
    public interface IBuildCatalog
    {
        BuildCardDefinition? GetCardDefinition(string configId);
        IReadOnlyList<BuildCardDefinition> GetDraftPoolCards(string? poolId, HeroClass heroClass);
        EquipmentDefinition? GetEquipmentDefinition(string equipmentId);
    }

    public sealed class InMemoryBuildCatalog : IBuildCatalog
    {
        private readonly Dictionary<string, BuildCardDefinition> _cards = new Dictionary<string, BuildCardDefinition>();
        private readonly Dictionary<string, EquipmentDefinition> _equipments = new Dictionary<string, EquipmentDefinition>();
        private readonly Dictionary<string, HashSet<string>> _pools = new Dictionary<string, HashSet<string>>();

        public BuildCardDefinition? GetCardDefinition(string configId)
        {
            return _cards.TryGetValue(configId, out var definition) ? definition : null;
        }

        public IReadOnlyList<BuildCardDefinition> GetDraftPoolCards(string? poolId, HeroClass heroClass)
        {
            var results = new List<BuildCardDefinition>();
            if (!string.IsNullOrWhiteSpace(poolId) && _pools.TryGetValue(poolId, out var poolCards))
            {
                foreach (var configId in poolCards)
                {
                    if (_cards.TryGetValue(configId, out var definition) && IsClassAllowed(definition, heroClass))
                        results.Add(definition);
                }

                return results;
            }

            foreach (var definition in _cards.Values)
            {
                if (IsClassAllowed(definition, heroClass))
                    results.Add(definition);
            }

            return results;
        }

        public EquipmentDefinition? GetEquipmentDefinition(string equipmentId)
        {
            return _equipments.TryGetValue(equipmentId, out var definition) ? definition : null;
        }

        public InMemoryBuildCatalog AddCardDefinition(BuildCardDefinition definition)
        {
            _cards[definition.ConfigId] = definition;
            return this;
        }

        public InMemoryBuildCatalog AddEquipmentDefinition(EquipmentDefinition definition)
        {
            _equipments[definition.EquipmentId] = definition;
            return this;
        }

        public InMemoryBuildCatalog AddPoolCard(string poolId, string configId)
        {
            if (!_pools.TryGetValue(poolId, out var pool))
            {
                pool = new HashSet<string>(System.StringComparer.Ordinal);
                _pools[poolId] = pool;
            }

            pool.Add(configId);
            return this;
        }

        private static bool IsClassAllowed(BuildCardDefinition definition, HeroClass heroClass)
        {
            return definition.ClassId == HeroClass.Universal || definition.ClassId == heroClass;
        }
    }
}
