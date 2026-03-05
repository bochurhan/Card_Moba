
#pragma warning disable CS8632

using System.Collections.Generic;

namespace CardMoba.BattleCore.Foundation
{
    /// <summary>
    /// 战斗单位类型。
    /// </summary>
    public enum EntityType
    {
        /// <summary>玩家英雄</summary>
        Player = 0,

        /// <summary>召唤物/小兵（预留，当前版本未实装）</summary>
        Minion = 1,

        /// <summary>建筑/结构（预留，当前版本未实装）</summary>
        Structure = 2,
    }

    /// <summary>
    /// 战斗单位（Entity）—— 战场上所有可被效果目标的对象基类。
    /// 当前版本主要实现为 PlayerEntity（玩家英雄）。
    /// </summary>
    public class Entity
    {
        // ══════════════════════════════════════════════════════════
        // 身份
        // ══════════════════════════════════════════════════════════

        /// <summary>战斗内唯一 ID（对于玩家，与 PlayerData.PlayerId 一致）</summary>
        public string EntityId { get; set; } = string.Empty;

        /// <summary>单位类型</summary>
        public EntityType Type { get; set; } = EntityType.Player;

        /// <summary>归属玩家 ID（对于玩家单位，等于 EntityId）</summary>
        public string OwnerPlayerId { get; set; } = string.Empty;

        // ══════════════════════════════════════════════════════════
        // 生命值
        // ══════════════════════════════════════════════════════════

        /// <summary>当前 HP</summary>
        public int Hp { get; set; }

        /// <summary>最大 HP</summary>
        public int MaxHp { get; set; }

        /// <summary>当前护盾值（回合结束前完全消散）</summary>
        public int Shield { get; set; }

        /// <summary>当前护甲值（固定减伤，每次受伤后先扣护甲）</summary>
        public int Armor { get; set; }

        // ══════════════════════════════════════════════════════════
        // 状态标志
        // ══════════════════════════════════════════════════════════

        /// <summary>是否存活</summary>
        public bool IsAlive => Hp > 0;

        /// <summary>是否处于无敌状态（本回合免疫所有伤害）</summary>
        public bool IsInvincible { get; set; }

        /// <summary>是否处于眩晕状态（下回合无法打出任何牌）</summary>
        public bool IsStunned { get; set; }

        /// <summary>是否处于沉默状态（本回合无法打出非伤害牌）</summary>
        public bool IsSilenced { get; set; }

        /// <summary>
        /// 死亡事件是否已触发（防止同一回合内重复触发 OnDeath）。
        /// 由 RoundManager 在 CheckDeathAndBattleOver 中维护。
        /// </summary>
        public bool DeathEventFired { get; set; } = false;

        // ══════════════════════════════════════════════════════════
        // 活跃 Buff 列表
        // ══════════════════════════════════════════════════════════

        /// <summary>当前持有的所有 Buff 实例（key = RuntimeId）</summary>
        public Dictionary<string, BuffUnit> ActiveBuffs { get; set; } = new Dictionary<string, BuffUnit>();
    }
}
