
#pragma warning disable CS8632

using System.Collections.Generic;
using CardMoba.BattleCore.Foundation;

namespace CardMoba.BattleCore.Context
{
    /// <summary>
    /// Layer 2 伤害结算前的防御快照。
    /// 记录某玩家在 Layer 2 开始时的防御数值，供对方计算伤害时使用（实现双方快照隔离语义）。
    /// </summary>
    public class DefenseSnapshot
    {
        /// <summary>快照归属玩家 ID</summary>
        public string PlayerId { get; set; } = string.Empty;

        /// <summary>快照时刻的 HP</summary>
        public int Hp { get; set; }

        /// <summary>快照时刻的护盾值</summary>
        public int Shield { get; set; }

        /// <summary>快照时刻的护甲值</summary>
        public int Armor { get; set; }

        /// <summary>快照时刻是否处于无敌状态</summary>
        public bool IsInvincible { get; set; }
    }

    /// <summary>
    /// 玩家战斗数据（PlayerData）—— 记录单个玩家在一局战斗中的完整运行时状态。
    /// 包含英雄实体引用、卡牌集合、能量等所有运行时数据。
    /// </summary>
    public class PlayerData
    {
        // ══════════════════════════════════════════════════════════
        // 身份
        // ══════════════════════════════════════════════════════════

        /// <summary>玩家唯一 ID</summary>
        public string PlayerId { get; set; } = string.Empty;

        /// <summary>英雄配置 ID（如 "warrior_01"）</summary>
        public string HeroConfigId { get; set; } = string.Empty;

        /// <summary>玩家英雄实体（战场上实际存在的单位）</summary>
        public Entity HeroEntity { get; set; } = new Entity();

        // ══════════════════════════════════════════════════════════
        // 资源
        // ══════════════════════════════════════════════════════════

        /// <summary>当前能量值</summary>
        public int Energy { get; set; }

        /// <summary>最大能量值（每回合回满）</summary>
        public int MaxEnergy { get; set; } = 3;

        // ══════════════════════════════════════════════════════════
        // 卡牌区域
        // ══════════════════════════════════════════════════════════

        /// <summary>卡组（所有 BattleCard 实例，不区分区域；区域通过 BattleCard.Zone 判断）</summary>
        public List<BattleCard> AllCards { get; set; } = new List<BattleCard>();

        /// <summary>获取指定区域的所有牌（只读视图）</summary>
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

        // ══════════════════════════════════════════════════════════
        // 统计
        // ══════════════════════════════════════════════════════════

        /// <summary>本局累计造成的伤害总量（由 StatManager 更新）</summary>
        public int TotalDamageDealt { get; set; }

        /// <summary>本局累计受到的伤害总量（由 StatManager 更新）</summary>
        public int TotalDamageTaken { get; set; }

        /// <summary>本局累计治疗总量（由 StatManager 更新）</summary>
        public int TotalHealDone { get; set; }

        /// <summary>本回合已打出的卡牌总数。</summary>
        public int PlayedCardCountThisRound { get; set; }

        /// <summary>本回合已打出的伤害牌数量。</summary>
        public int PlayedDamageCardCountThisRound { get; set; }

        /// <summary>本回合已打出的防御牌数量。</summary>
        public int PlayedDefenseCardCountThisRound { get; set; }

        /// <summary>本回合已打出的反制牌数量。</summary>
        public int PlayedCounterCardCountThisRound { get; set; }

        // ══════════════════════════════════════════════════════════
        // Layer 2 防御快照（由 SettlementEngine 在 Pre-Layer 2 阶段设置）
        // ══════════════════════════════════════════════════════════

        /// <summary>
        /// 本回合 Layer 2 开始时的防御快照。
        /// 对方计算对我方的伤害时，以此快照为基准（双方快照隔离语义）。
        /// 每回合 Layer 2 开始前由 SettlementEngine 更新，结算完成后清空。
        /// </summary>
        public DefenseSnapshot? CurrentDefenseSnapshot { get; set; }
    }
}
