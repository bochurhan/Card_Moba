using System.Collections.Generic;
using CardMoba.ConfigModels.Card;

namespace CardMoba.BattleCore.Context
{
    /// <summary>
    /// 玩家对局状态 —— 一个玩家在一局对战中的所有可变数据。
    /// 注意：这是运行时状态，不是配置数据。
    /// </summary>
    public class PlayerBattleState
    {
        /// <summary>玩家ID（用于区分不同玩家）</summary>
        public int PlayerId { get; set; }

        /// <summary>玩家显示名称</summary>
        public string PlayerName { get; set; } = string.Empty;

        // ── 生存属性 ──

        /// <summary>当前生命值</summary>
        public int Hp { get; set; }

        /// <summary>最大生命值</summary>
        public int MaxHp { get; set; }

        /// <summary>当前护盾值（受到伤害时优先扣护盾）</summary>
        public int Shield { get; set; }

        // ── 资源属性 ──

        /// <summary>当前能量（每回合恢复，出牌消耗）</summary>
        public int Energy { get; set; }

        /// <summary>每回合能量恢复量</summary>
        public int EnergyPerRound { get; set; }

        // ── 卡牌区域 ──

        /// <summary>手牌（玩家当前持有的卡牌）</summary>
        public List<CardConfig> Hand { get; set; } = new List<CardConfig>();

        /// <summary>牌库（还没摸到的牌，摸牌时从这里抽）</summary>
        public List<CardConfig> Deck { get; set; } = new List<CardConfig>();

        /// <summary>弃牌堆（已打出/已丢弃的牌）</summary>
        public List<CardConfig> DiscardPile { get; set; } = new List<CardConfig>();

        // ── 状态标记 ──

        /// <summary>本回合是否已锁定操作（锁定后不能再出牌）</summary>
        public bool IsLocked { get; set; }

        /// <summary>玩家是否存活</summary>
        public bool IsAlive => Hp > 0;
    }
}
