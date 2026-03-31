using CardMoba.MatchFlow.Catalog;
using CardMoba.MatchFlow.Definitions;
using CardMoba.Protocol.Enums;

namespace CardMoba.Server.Host.Config
{
    /// <summary>
    /// 服务端构筑目录工厂。
    /// 当前先复用 warrior 的默认池和默认装备定义。
    /// </summary>
    public sealed class ServerBuildCatalogFactory
    {
        private readonly ServerCardCatalog _cardCatalog;

        public ServerBuildCatalogFactory(ServerCardCatalog cardCatalog)
        {
            _cardCatalog = cardCatalog;
        }

        public IBuildCatalog Create()
        {
            var assembler = new BuildCatalogAssembler();
            return assembler.Create(_cardCatalog.AllCards, CreateDefaultEquipmentDefinitions());
        }

        public IReadOnlyList<EquipmentDefinition> CreateDefaultEquipmentDefinitions()
        {
            return new[]
            {
                new EquipmentDefinition
                {
                    EquipmentId = "burning_blood",
                    ClassId = HeroClass.Warrior,
                    EffectType = EquipmentEffectType.HealAfterBattleFlat,
                    EffectValue = 6,
                },
            };
        }
    }
}
