#pragma warning disable CS8632

namespace CardMoba.BattleCore.Foundation
{
    public enum EntityType
    {
        Player = 0,
        Minion = 1,
        Structure = 2,
    }

    /// <summary>
    /// 战场单位。当前主实现是玩家英雄实体。
    /// </summary>
    public class Entity
    {
        public string EntityId { get; set; } = string.Empty;
        public EntityType Type { get; set; } = EntityType.Player;
        public string OwnerPlayerId { get; set; } = string.Empty;

        public int Hp { get; set; }
        public int MaxHp { get; set; }
        public int Shield { get; set; }
        public int Armor { get; set; }

        public bool IsAlive => Hp > 0;
        public bool IsInvincible { get; set; }
        public bool IsStunned { get; set; }
        public bool IsSilenced { get; set; }

        // 由 RoundManager 统一维护，防止重复触发死亡链路。
        public bool DeathEventFired { get; set; } = false;
    }
}
