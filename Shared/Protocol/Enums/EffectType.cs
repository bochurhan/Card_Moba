namespace CardMoba.Protocol.Enums
{
    /// <summary>
    /// 卡牌效果类型 —— 定义单个效果的具体行为类型。
    /// Card effect type — Defines the specific behavior of a single effect.
    ///
    /// 效果按所属结算堆叠层分类（对应 SettlementLayer 枚举）：
    ///   Layer 0 (Counter)   ：Counter
    ///   Layer 1 (Defense)   ：Shield, Armor, AttackBuff, AttackDebuff, Reflect, DamageReduction, Invincible
    ///   Layer 2 (Damage)    ：Damage, Lifesteal, Thorns, ArmorOnHit, Pierce, DOT
    ///   Layer 3 (Resource)  ：Draw, Discard, GainEnergy, GenerateCard
    ///   Layer 4 (BuffSpecial)：Heal, Stun, Vulnerable, Weak, Silence, Slow, DoubleStrength, BanDraw, AddBuff
    ///
    /// ⚠️ ID 规则：已分配 ID 永不修改，新增效果追加于当前最大 ID(33) 之后。
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

        /// <summary>穿透（伤害无视目标护甲/护盾，直接扣血）</summary>
        Pierce = 14,

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

        /// <summary>力量翻倍（将施法者当前力量值×2，消耗型）</summary>
        DoubleStrength = 29,

        /// <summary>施加"禁止抽牌"Debuff（本回合剩余时间内无法抽牌）</summary>
        BanDraw = 30,

        // ═══════════════════════════════════════════════════════════
        // V2 新增
        // ═══════════════════════════════════════════════════════════

        /// <summary>添加 Buff（通过 BuffManager 注册，配合 BuffConfigId 使用）</summary>
        AddBuff = 31,

        /// <summary>生成临时卡牌（放入指定区域，配合 GenerateCardConfigId 使用）</summary>
        GenerateCard = 32,

        /// <summary>持续伤害（DOT，每回合结束时触发一次伤害，配合 Duration 使用）</summary>
        DOT = 33,
    }
}
