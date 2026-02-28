namespace CardMoba.Protocol.Enums
{
    /// <summary>
    /// Buff 类型枚举 —— 定义所有可能的增益/减益类型。
    /// 此枚举定义在 Protocol 层，BattleCore 和 ConfigModels 均可引用。
    /// </summary>
    public enum BuffType
    {
        /// <summary>未知类型</summary>
        Unknown = 0,

        // ══════════════════════════════════════════════════════════
        // 数值修正类（直接修改属性值）
        // ══════════════════════════════════════════════════════════

        /// <summary>护甲增加（固定值减免伤害）</summary>
        Armor = 101,

        /// <summary>力量增加（增加造成的伤害）</summary>
        Strength = 102,

        /// <summary>护盾（吸收伤害）</summary>
        Shield = 103,

        /// <summary>生命恢复（每回合恢复生命）</summary>
        Regeneration = 104,

        /// <summary>能量增加</summary>
        EnergyGain = 105,

        /// <summary>最大生命值增加</summary>
        MaxHpBonus = 106,

        // ══════════════════════════════════════════════════════════
        // 增益状态类（有利效果）
        // ══════════════════════════════════════════════════════════

        /// <summary>无敌（完全免疫伤害）</summary>
        Invincible = 201,

        /// <summary>吸血（造成伤害时恢复生命）</summary>
        Lifesteal = 202,

        /// <summary>反伤（受到伤害时反弹部分伤害）</summary>
        Thorns = 203,

        /// <summary>伤害减免（百分比减少受到的伤害）</summary>
        DamageReduction = 204,

        /// <summary>伤害增幅（百分比增加造成的伤害）</summary>
        DamageAmplify = 205,

        /// <summary>闪避（概率完全躲避伤害）</summary>
        Evasion = 206,

        /// <summary>格挡（下次受到伤害减半）</summary>
        Block = 207,

        // ══════════════════════════════════════════════════════════
        // 减益状态类（不利效果）
        // ══════════════════════════════════════════════════════════

        /// <summary>易伤（增加受到的伤害）</summary>
        Vulnerable = 301,

        /// <summary>虚弱（减少造成的伤害）</summary>
        Weak = 302,

        /// <summary>中毒（每回合受到伤害）</summary>
        Poison = 303,

        /// <summary>灼烧（每回合受到伤害，可叠加）</summary>
        Burn = 304,

        /// <summary>流血（每回合受到伤害，移动加剧）</summary>
        Bleed = 305,

        /// <summary>诅咒（死亡时传染给友方）</summary>
        Curse = 306,

        // ══════════════════════════════════════════════════════════
        // 控制类（限制行动）
        // ══════════════════════════════════════════════════════════

        /// <summary>眩晕（无法行动）</summary>
        Stun = 401,

        /// <summary>沉默（无法使用技能）</summary>
        Silence = 402,

        /// <summary>缴械（无法使用攻击牌）</summary>
        Disarm = 403,

        /// <summary>冻结（无法行动且易伤）</summary>
        Freeze = 404,

        /// <summary>魅惑（攻击可能打向友方）</summary>
        Charm = 405,

        /// <summary>禁锢（无法换路/支援）</summary>
        Root = 406,

        // ══════════════════════════════════════════════════════════
        // 特殊效果类
        // ══════════════════════════════════════════════════════════

        /// <summary>标记（被标记的目标受到额外效果）</summary>
        Mark = 501,

        /// <summary>隐身（无法被选为目标）</summary>
        Stealth = 502,

        /// <summary>嘲讽（强制敌方攻击自己）</summary>
        Taunt = 503,

        /// <summary>灵魂链接（与另一目标分担伤害）</summary>
        SoulLink = 504,

        /// <summary>复活（死亡时恢复生命）</summary>
        Resurrection = 505,

        /// <summary>禁止抽牌（本回合无法通过任何方式抽牌）</summary>
        NoDrawThisTurn = 506,
    }

    /// <summary>
    /// Buff 叠加规则。
    /// </summary>
    public enum BuffStackRule
    {
        /// <summary>不可叠加（同类型只能存在一个）</summary>
        None = 0,

        /// <summary>刷新持续时间（新的覆盖旧的持续时间）</summary>
        RefreshDuration = 1,

        /// <summary>叠加层数（数值累加）</summary>
        StackValue = 2,

        /// <summary>独立存在（每个来源独立计算）</summary>
        Independent = 3,

        /// <summary>取最高值（保留数值最高的）</summary>
        KeepHighest = 4,
    }

    /// <summary>
    /// Buff 触发时机。
    /// </summary>
    public enum BuffTriggerTiming
    {
        /// <summary>无触发效果（纯属性修正）</summary>
        None = 0,

        /// <summary>回合开始时触发</summary>
        OnRoundStart = 1,

        /// <summary>回合结束时触发</summary>
        OnRoundEnd = 2,

        /// <summary>受到伤害时触发</summary>
        OnDamageTaken = 3,

        /// <summary>造成伤害时触发</summary>
        OnDamageDealt = 4,

        /// <summary>出牌时触发</summary>
        OnCardPlayed = 5,

        /// <summary>抽牌时触发</summary>
        OnCardDrawn = 6,

        /// <summary>濒死时触发</summary>
        OnNearDeath = 7,

        /// <summary>击杀敌人时触发</summary>
        OnKill = 8,

        /// <summary>被治疗时触发</summary>
        OnHealed = 9,

        /// <summary>Buff 移除时触发</summary>
        OnBuffRemoved = 10,
    }
}
