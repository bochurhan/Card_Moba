namespace CardMoba.Protocol.Enums
{
    /// <summary>
    /// 卡牌效果类型 —— 定义单个效果的具体行为类型。
    /// Card effect type — Defines the specific behavior of a single effect.
    /// 
    /// V3.0 架构：
    /// - 1-10: 核心效果类型（对应 Handler）
    /// - 100+: 旧版细分类型（兼容保留）
    /// 
    /// 效果按照所属堆叠层分别结算，符合《定策牌结算机制》多子类型拆分铁律。
    /// </summary>
    public enum EffectType
    {
        /// <summary>无效果/占位 (None/Placeholder)</summary>
        None = 0,

        // ═══════════════════════════════════════════════════════════
        // V3.0 核心效果类型 (对应 Handler)
        // ═══════════════════════════════════════════════════════════

        /// <summary>反制 (Counter) - Layer 0</summary>
        Counter = 1,

        /// <summary>伤害 (Damage) - Layer 2</summary>
        Damage = 2,

        /// <summary>护盾 (Shield) - Layer 1</summary>
        Shield = 3,

        /// <summary>治疗 (Heal) - Layer 3</summary>
        Heal = 4,

        /// <summary>眩晕 (Stun) - Layer 3</summary>
        Stun = 5,

        /// <summary>护甲 (Armor) - Layer 1</summary>
        Armor = 6,

        /// <summary>增益攻击力 (Attack Modifier) - Layer 1</summary>
        AttackBuff = 7,

        /// <summary>反伤 (Thorns/Reflect) - Layer 1</summary>
        Reflect = 8,

        /// <summary>易伤 (Vulnerable) - Layer 3</summary>
        Vulnerable = 9,

        /// <summary>抽牌 (Draw) - Layer 3</summary>
        Draw = 10,

        // ═══════════════════════════════════════════════════════════
        // 旧版细分类型 (向后兼容)
        // ═══════════════════════════════════════════════════════════

        // ── 堆叠1层：防御与数值修正 ──

        /// <summary>获得护甲（减少受到的伤害）(Gain armor: reduces incoming damage)</summary>
        GainArmor = 101,

        /// <summary>获得护盾（吸收伤害）(Gain shield: absorbs damage)</summary>
        GainShield = 102,

        /// <summary>伤害减免（百分比减少受到的伤害）(Damage reduction: percentage damage decrease)</summary>
        DamageReduction = 103,

        /// <summary>无敌（完全免疫伤害）(Invincible: immune to all damage)</summary>
        Invincible = 104,

        /// <summary>增加力量（增加造成的伤害）(Gain strength: increases damage dealt)</summary>
        GainStrength = 111,

        /// <summary>降低力量（减少造成的伤害）(Reduce strength: decreases damage dealt)</summary>
        ReduceStrength = 112,

        /// <summary>破甲（降低目标护甲）(Armor break: reduces target's armor)</summary>
        ArmorBreak = 113,

        /// <summary>穿透（无视目标护甲）(Pierce: ignores target's armor)</summary>
        Pierce = 114,


        /// <summary>虚弱（目标造成的伤害减少）(Weak: target deals reduced damage)</summary>
        Weak = 116,

        // ── 堆叠2层-步骤1：主动伤害 ──

        /// <summary>造成伤害（直接扣减目标生命值）(Deal damage: directly reduces target HP)</summary>
        DealDamage = 201,

        // ── 堆叠2层-步骤2：触发式效果 ──

        /// <summary>反伤（受到伤害时反弹伤害）(Thorns: reflects damage when hit)</summary>
        Thorns = 211,

        /// <summary>吸血（造成伤害时回复生命）(Lifesteal: heals when dealing damage)</summary>
        Lifesteal = 212,

        /// <summary>受击回血（受到伤害时回复生命）(Heal on hit: heals when taking damage)</summary>
        HealOnHit = 213,

        /// <summary>受击获得护甲（受到伤害时获得护甲）(Armor on hit: gains armor when taking damage)</summary>
        ArmorOnHit = 214,

        /// <summary>击杀回血（击杀敌人时回复生命）(Heal on kill: heals when killing enemy)</summary>
        HealOnKill = 215,

        // ── 堆叠3层-普通效果 ──

        /// <summary>抽牌（从牌库抽取卡牌）(Draw: draws cards from deck)</summary>
        [System.Obsolete("V3.0: 使用 Draw = 10")]
        DrawCards = 301,

        /// <summary>弃牌（弃置手牌）(Discard: discards cards from hand)</summary>
        Discard = 302,

        /// <summary>回复能量 (Gain energy)</summary>
        GainEnergy = 303,

        /// <summary>回复生命 (Heal HP)</summary>
        [System.Obsolete("V3.0: 使用 Heal = 4")]
        HealHp = 304,

        /// <summary>沉默（禁止使用技能牌）(Silence: prevents using skill cards)</summary>
        Silence = 311,

        /// <summary>眩晕（跳过操作回合）(Stun: skips action phase)</summary>
        [System.Obsolete("V3.0: 使用 Stun = 5")]
        StunTarget = 312,

        /// <summary>减速（行动顺序降低）(Slow: reduces action priority)</summary>
        Slow = 313,

        // ── 堆叠0层：反制效果 ──

        /// <summary>反制卡牌（使目标卡牌无效）(Counter card: negates target card)</summary>
        CounterCard = 401,

        /// <summary>反制首张伤害牌 (Counter first damage: negates first damage card)</summary>
        CounterFirstDamage = 402,

        /// <summary>反制并反弹（使目标卡牌无效并反弹效果）(Counter and reflect: negates and reflects card effect)</summary>
        CounterAndReflect = 403,
    }
}