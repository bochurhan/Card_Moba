#pragma warning disable CS8632

using System.Collections.Generic;
using CardMoba.BattleCore.Foundation;

namespace CardMoba.BattleCore.Context
{
    /// <summary>
    /// Layer 2 伤害结算前的防御快照。
    /// </summary>
    public class DefenseSnapshot
    {
        public string PlayerId { get; set; } = string.Empty;
        public int Hp { get; set; }
        public int Shield { get; set; }
        public int Armor { get; set; }
        public bool IsInvincible { get; set; }
    }

    /// <summary>
    /// 单个玩家在一局战斗中的运行时状态。
    /// </summary>
    public class PlayerData
    {
        public string PlayerId { get; set; } = string.Empty;
        public string TeamId { get; set; } = string.Empty;
        public string HeroConfigId { get; set; } = string.Empty;
        public Entity HeroEntity { get; set; } = new Entity();

        public int Energy { get; set; }
        public int MaxEnergy { get; set; } = 3;

        /// <summary>
        /// 该玩家持有的全部 BattleCard 实例。
        /// 卡牌所在区位由 BattleCard.Zone 表示。
        /// </summary>
        public List<BattleCard> AllCards { get; set; } = new List<BattleCard>();

        public List<BattleCard> GetCardsInZone(CardZone zone)
        {
            var result = new List<BattleCard>();
            foreach (var card in AllCards)
            {
                if (card.Zone == zone)
                    result.Add(card);
            }

            return result;
        }

        public int TotalDamageDealt { get; set; }
        public int TotalDamageTaken { get; set; }
        public int TotalHealDone { get; set; }

        public int PlayedCardCountThisRound { get; set; }
        public int PlayedDamageCardCountThisRound { get; set; }
        public int PlayedDefenseCardCountThisRound { get; set; }
        public int PlayedCounterCardCountThisRound { get; set; }

        /// <summary>
        /// 本场战斗中，按实例统计的打出次数。
        /// 适用于“暴走”这类实例成长牌。
        /// </summary>
        public Dictionary<string, int> PlayedCountByInstanceId { get; } = new Dictionary<string, int>();

        /// <summary>
        /// 本场战斗中，按原始配置 ID 统计的打出次数。
        /// 适用于未来“同名牌打出次数”类效果。
        /// </summary>
        public Dictionary<string, int> PlayedCountByConfigId { get; } = new Dictionary<string, int>();

        /// <summary>
        /// 本回合剩余的腐化免费次数。
        /// 命中后本次打出的牌费用变为 0，且结算后进入 Consume。
        /// </summary>
        public int CorruptionFreePlaysRemainingThisRound { get; set; }

        public DefenseSnapshot? CurrentDefenseSnapshot { get; set; }

        public bool CanAct => HeroEntity != null && HeroEntity.IsAlive;
    }
}
