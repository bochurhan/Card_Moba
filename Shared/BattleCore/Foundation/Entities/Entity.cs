#pragma warning disable CS8632

using System.Collections.Generic;

namespace CardMoba.BattleCore.Foundation
{
    public enum EntityType
    {
        Player = 0,
        Minion = 1,
        Structure = 2,
    }

    /// <summary>
    /// 战斗实体。
    /// 当前主要用于英雄实体，同时也为共享目标等通用实体预留建模空间。
    /// </summary>
    public class Entity
    {
        public string EntityId { get; set; } = string.Empty;
        public EntityType Type { get; set; } = EntityType.Player;
        public string OwnerPlayerId { get; set; } = string.Empty;
        public string TeamId { get; set; } = string.Empty;

        public int Hp { get; set; }
        public int MaxHp { get; set; }
        public int Shield { get; set; }
        public int Armor { get; set; }

        public bool IsAlive => Hp > 0;
        public bool IsTargetable { get; set; } = true;
        public bool IsInvincible { get; set; }
        public bool IsStunned { get; set; }
        public bool IsSilenced { get; set; }
        public bool EndsMatchWhenDestroyed { get; set; }
        public List<string> RequiredDeadEntityIdsToTarget { get; } = new List<string>();

        // 由 RoundManager 统一维护，防止重复触发死亡链路。
        public bool DeathEventFired { get; set; } = false;
    }
}
