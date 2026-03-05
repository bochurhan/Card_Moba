
#pragma warning disable CS8632

using System;
using System.Collections.Generic;
using CardMoba.BattleCore.Foundation;

namespace CardMoba.BattleCore.EventBus
{
    // ══════════════════════════════════════════════════════════════
    // 战斗事件基类
    // ══════════════════════════════════════════════════════════════

    /// <summary>战斗事件基类（所有战斗事件继承此类）</summary>
    public abstract class BattleEventBase { }

    // ══════════════════════════════════════════════════════════════
    // 伤害 / 治疗 / 护盾事件
    // ══════════════════════════════════════════════════════════════

    /// <summary>伤害结算完成事件</summary>
    public class DamageDealtEvent : BattleEventBase
    {
        /// <summary>施害者 Entity ID</summary>
        public string SourceEntityId { get; set; } = string.Empty;
        /// <summary>受害者 Entity ID</summary>
        public string TargetEntityId { get; set; } = string.Empty;
        /// <summary>基础伤害（结算前）</summary>
        public int BaseDamage { get; set; }
        /// <summary>实际 HP 扣减量（护盾/护甲吸收后）</summary>
        public int RealHpDamage { get; set; }
        /// <summary>被护盾吸收的量</summary>
        public int ShieldAbsorbed { get; set; }
        /// <summary>被护甲减免的量</summary>
        public int ArmorReduced { get; set; }
        /// <summary>是否触发了破盾事件</summary>
        public bool ShieldBroken { get; set; }
        /// <summary>是否为 DoT 持续伤害（Burn/Poison/Bleed 触发，忽护甲）</summary>
        public bool IsDot { get; set; }
        /// <summary>是否为荆棘反伤（Thorns Buff 触发）</summary>
        public bool IsThorns { get; set; }
        /// <summary>来源卡牌的 InstanceId（可为 null，如 DOT 伤害）</summary>
        public string? SourceCardInstanceId { get; set; }
    }

    /// <summary>治疗结算完成事件</summary>
    public class HealEvent : BattleEventBase
    {
        public string SourceEntityId { get; set; } = string.Empty;
        public string TargetEntityId { get; set; } = string.Empty;
        /// <summary>实际恢复量（不超过最大 HP）</summary>
        public int RealHealAmount { get; set; }
        public string? SourceCardInstanceId { get; set; }
    }

    /// <summary>护盾附加事件</summary>
    public class ShieldGainedEvent : BattleEventBase
    {
        public string TargetEntityId { get; set; } = string.Empty;
        public int ShieldAmount { get; set; }
        public string? SourceCardInstanceId { get; set; }
    }

    // ══════════════════════════════════════════════════════════════
    // Buff 事件
    // ══════════════════════════════════════════════════════════════

    /// <summary>Buff 添加事件</summary>
    public class BuffAddedEvent : BattleEventBase
    {
        public string TargetEntityId { get; set; } = string.Empty;
        public BuffUnit Buff { get; set; } = new BuffUnit();
    }

    /// <summary>Buff 移除事件</summary>
    public class BuffRemovedEvent : BattleEventBase
    {
        public string TargetEntityId { get; set; } = string.Empty;
        public string BuffRuntimeId { get; set; } = string.Empty;
        public string BuffConfigId { get; set; } = string.Empty;
    }

    // ══════════════════════════════════════════════════════════════
    // 卡牌事件
    // ══════════════════════════════════════════════════════════════

    /// <summary>卡牌打出事件</summary>
    public class CardPlayedEvent : BattleEventBase
    {
        public string PlayerId { get; set; } = string.Empty;
        public string CardInstanceId { get; set; } = string.Empty;
        public string CardConfigId { get; set; } = string.Empty;
    }

    /// <summary>卡牌抽取事件</summary>
    public class CardDrawnEvent : BattleEventBase
    {
        public string PlayerId { get; set; } = string.Empty;
        public string CardInstanceId { get; set; } = string.Empty;
        public string CardConfigId { get; set; } = string.Empty;
    }

    // ══════════════════════════════════════════════════════════════
    // 死亡事件
    // ══════════════════════════════════════════════════════════════

    /// <summary>单位死亡事件</summary>
    public class EntityDeathEvent : BattleEventBase
    {
        public string EntityId { get; set; } = string.Empty;
        public string KillerEntityId { get; set; } = string.Empty;
    }

    // ══════════════════════════════════════════════════════════════
    // 回合事件
    // ══════════════════════════════════════════════════════════════

    /// <summary>回合开始事件</summary>
    public class RoundStartEvent : BattleEventBase
    {
        public int Round { get; set; }
    }

    /// <summary>回合结束事件</summary>
    public class RoundEndEvent : BattleEventBase
    {
        public int Round { get; set; }
    }

    // ══════════════════════════════════════════════════════════════
    // 战斗生命周期事件
    // ══════════════════════════════════════════════════════════════

    /// <summary>战斗开始事件（InitBattle 后触发）</summary>
    public class BattleStartEvent : BattleEventBase
    {
        /// <summary>战斗唯一 ID</summary>
        public string BattleId { get; set; } = string.Empty;
        /// <summary>触发时回合号（通常为 0）</summary>
        public int Round { get; set; }
    }

    /// <summary>战斗结束事件（玩家死亡或所有人同时死亡）</summary>
    public class BattleEndEvent : BattleEventBase
    {
        /// <summary>获胜玩家 ID（平局时为 null）</summary>
        public string? WinnerId { get; set; }
        /// <summary>是否为平局</summary>
        public bool IsDraw { get; set; }
    }

    /// <summary>玩家死亡事件（区别于 EntityDeathEvent，专指玩家英雄死亡）</summary>
    public class PlayerDeathEvent : BattleEventBase
    {
        /// <summary>死亡玩家 ID</summary>
        public string PlayerId { get; set; } = string.Empty;
    }
}
