namespace CardMoba.Protocol.Enums
{
    /// <summary>
    /// 卡牌效果类型 —— 定义单个效果的具体行为类型。
    /// Card effect type — Defines the specific behavior of a single effect.
    ///
    /// 效果按所属结算堆叠层分类：
    ///   Layer 0 (反制层)：Counter
    ///   Layer 1 (防御/修正层)：Shield, Armor, AttackBuff, AttackDebuff, Reflect, DamageReduction, Invincible
    ///   Layer 2 (伤害层)：Damage, Lifesteal, Thorns, ArmorOnHit
    ///   Layer 3 (功能层)：Heal, Stun, Vulnerable, Weak, Draw, Discard, GainEnergy, Silence, Slow
    /// </summary>
    public enum EffectType
    {
        /// <summary>无效果/占位</summary>
        None = 0,

        // ═══════════════════════════════════════════════════════════
        // Layer 0 — 反制层
        // ═══════════════════════════════════════════════════════════

        /// <summary>反制（使目标卡牌无效）</summary>
        Counter = 1,

        // ═══════════════════════════════════════════════════════════
        // Layer 1 — 防御与数值修正层
        // ═══════════════════════════════════════════════════════════

        /// <summary>护盾（吸收固定伤害值）</summary>
        Shield = 2,

        /// <summary>护甲（减少受到的伤害，百分比或固定值）</summary>
        Armor = 3,

        /// <summary>力量增益（增加造成的伤害）</summary>
        AttackBuff = 4,

        /// <summary>力量削减（降低目标造成的伤害）</summary>
        AttackDebuff = 5,

        /// <summary>反伤（受到伤害时将等量伤害反弹给攻击者）</summary>
        Reflect = 6,

        /// <summary>伤害减免（百分比减少受到的伤害）</summary>
        DamageReduction = 7,

        /// <summary>无敌（本回合完全免疫伤害）</summary>
        Invincible = 8,

        // ═══════════════════════════════════════════════════════════
        // Layer 2 — 主动伤害与触发式效果层
        // ═══════════════════════════════════════════════════════════

        /// <summary>造成伤害（直接扣减目标生命值，经过护甲/护盾计算）</summary>
        Damage = 10,

        /// <summary>吸血（造成伤害后回复等比例生命值，百分比由 Params.Percent 指定）</summary>
        Lifesteal = 11,

        /// <summary>荆棘（受到伤害后对攻击者造成固定伤害，忽略护甲）</summary>
        Thorns = 12,

        /// <summary>受击获甲（受到伤害时获得护甲）</summary>
        ArmorOnHit = 13,

        // ═══════════════════════════════════════════════════════════
        // Layer 3 — 全局功能收尾层
        // ═══════════════════════════════════════════════════════════

        /// <summary>治疗（恢复目标生命值，不超过最大生命）</summary>
        Heal = 20,

        /// <summary>眩晕（目标跳过下 N 个操作期）</summary>
        Stun = 21,

        /// <summary>易伤（目标受到的伤害增加，叠层计算）</summary>
        Vulnerable = 22,

        /// <summary>虚弱（目标造成的伤害减少，叠层计算）</summary>
        Weak = 23,

        /// <summary>抽牌（从牌库摸取指定数量的牌）</summary>
        Draw = 24,

        /// <summary>弃牌（弃置手牌中指定数量的牌）</summary>
        Discard = 25,

        /// <summary>回复能量</summary>
        GainEnergy = 26,

        /// <summary>沉默（禁止目标使用技能牌 N 回合）</summary>
        Silence = 27,

        /// <summary>减速（降低目标的行动顺序优先级）</summary>
        Slow = 28,
    }
}