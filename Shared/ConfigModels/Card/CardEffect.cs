using System.Collections.Generic;
using CardMoba.Protocol.Enums;
// BuffType / BuffStackRule / BuffTriggerTiming 已迁移至 CardMoba.Protocol.Enums（BuffEnums.cs）

namespace CardMoba.ConfigModels.Card
{
    /// <summary>
    /// 卡牌单效果配置 —— 描述卡牌的一个独立效果。
    ///
    /// 每个效果对应一个 Handler（由 EffectType 决定），
    /// 自动归属到 4 层结算栈（0=反制, 1=防御, 2=伤害, 3=功能）。
    ///
    /// 多效果拆分铁律：单效果被反制不影响同卡牌其他效果正常结算。
    ///
    /// 执行模式（ExecutionMode）决定效果如何运行：
    ///   Immediate   —— 默认，直接执行 Handler（Strike/Defend 等）
    ///   Conditional —— 结算时检查 EffectConditions，满足才执行（观察弱点等）
    ///   Passive     —— 打出时注册临时 Buff，由触发器驱动（反制等被动效果）
    /// </summary>
    public class CardEffect
    {
        // ═══════════════════════════════════════════════════════════
        // 基础属性
        // ═══════════════════════════════════════════════════════════

        /// <summary>效果类型（决定具体行为和所属堆叠层）</summary>
        public EffectType EffectType { get; set; }

        /// <summary>效果数值（伤害量、护甲量、抽牌数等）</summary>
        public int Value { get; set; }

        /// <summary>效果持续回合数（0=即时生效，>0=持续N回合）</summary>
        public int Duration { get; set; }

        /// <summary>效果目标类型（覆盖卡牌默认目标类型）</summary>
        public CardTargetType? TargetOverride { get; set; }

        /// <summary>
        /// [已废弃] 触发条件魔法字符串（无实际检查逻辑，被 EffectConditions 替代）。
        /// 保留为兼容旧数据，新卡牌请使用 EffectConditions。
        /// </summary>
        [System.Obsolete("请使用 EffectConditions 替代，TriggerCondition 字段无实际检查逻辑")]
        public string TriggerCondition { get; set; } = string.Empty;

        /// <summary>是否为跨回合效果（本回合锁定，下回合生效）</summary>
        public bool IsDelayed { get; set; }

        // ═══════════════════════════════════════════════════════════
        // 执行模式与条件（新架构核心字段）
        // ═══════════════════════════════════════════════════════════

        /// <summary>
        /// 效果执行模式（默认 Immediate）。
        ///   Immediate   —— 直接执行，无条件检查
        ///   Conditional —— 结算时评估 EffectConditions，全部满足才执行
        ///   Passive     —— 打出时通过 BuffManager 注册本回合临时 Buff
        /// </summary>
        public EffectExecutionMode ExecutionMode { get; set; } = EffectExecutionMode.Immediate;

        /// <summary>
        /// 效果执行条件列表（ExecutionMode = Conditional 时使用）。
        /// 所有条件必须同时满足，效果才会执行（AND 语义）。
        /// 空列表 = 无条件，等效于 Immediate。
        /// </summary>
        public List<EffectCondition> EffectConditions { get; set; } = new List<EffectCondition>();

        /// <summary>
        /// Passive 模式下触发效果的时机（ExecutionMode = Passive 时必填）。
        /// 对应 TriggerTiming 枚举值（以 int 存储，避免跨程序集依赖）。
        /// 常用值：BeforePlayCard=301（反制）、AfterDealDamage=202（被动增益）
        /// </summary>
        public int PassiveTriggerTiming { get; set; } = 0;

        /// <summary>
        /// Passive 模式持续回合数（默认 1 = 本回合，-1 = 永久直到移除）。
        /// </summary>
        public int PassiveDuration { get; set; } = 1;

        // ═══════════════════════════════════════════════════════════
        // Buff 附加声明（显式配置，Handler 依据此决定执行路径）
        // ═══════════════════════════════════════════════════════════

        /// <summary>
        /// 是否附加 Buff 到玩家状态栏。
        ///
        /// true  → Handler 通过 BuffManager.AddBuff() 施加，
        ///         Buff 具有生命周期（Duration 回合后自动移除），UI 显示图标。
        /// false → Handler 直接执行瞬时效果（如抽牌、扣血），不产生 Buff 条目。
        /// </summary>
        public bool AppliesBuff { get; set; }

        /// <summary>
        /// 附加的 Buff 类型（AppliesBuff = true 时必填）。
        /// 对应 BuffManager 中 BuffType 枚举，决定属性修正方式和衰减行为。
        /// </summary>
        public BuffType BuffType { get; set; }

        /// <summary>
        /// Buff 叠加规则（AppliesBuff = true 时有效）。
        /// 默认 RefreshDuration：同类 Buff 刷新持续时间而非叠加层数。
        /// </summary>
        public BuffStackRule BuffStackRule { get; set; }
            = BuffStackRule.RefreshDuration;

        /// <summary>
        /// Buff 是否可被驱散（AppliesBuff = true 时有效）。
        /// 默认可驱散。传说/固有效果可设为 false。
        /// </summary>
        public bool IsBuffDispellable { get; set; } = true;

        // ═══════════════════════════════════════════════════════════
        // 结算优先级控制
        // ═══════════════════════════════════════════════════════════

        /// <summary>
        /// 主优先级 —— 控制在同堆叠层内的结算顺序（数值越小越先执行）。
        ///
        /// 分段约定（与 TriggerInstance.Priority 保持一致）：
        ///   0   - 99  : 系统级（反制、无敌）
        ///   100 - 199 : 增益效果（力量+、护甲+、护盾+）← 默认增益类填 150
        ///   200 - 299 : 己方遗物增益
        ///   300 - 399 : 削弱效果（力量-、易伤、虚弱） ← 默认削弱类填 350
        ///   400 - 499 : 敌方遗物削弱
        ///   500 - 599 : 伤害效果（默认值）
        ///   900 - 999 : 传说特殊牌
        ///
        /// 策划在配表时按效果语义填写；不填则保持默认值 500（伤害区间）。
        /// </summary>
        public int Priority { get; set; } = 500;

        /// <summary>
        /// 次级优先级 —— 主优先级相同时的打破平局字段（数值越小越先执行，默认 0）。
        /// 同类效果若需要固定顺序（如先护盾后护甲），通过此字段区分。
        /// </summary>
        public int SubPriority { get; set; } = 0;

        // ═══════════════════════════════════════════════════════════
        // Handler 架构支持
        // ═══════════════════════════════════════════════════════════

        /// <summary>效果参数（用于复杂效果的额外配置）</summary>
        public EffectParams Params { get; set; }

        /// <summary>子效果列表（用于组合效果）</summary>
        public List<SubEffect> SubEffects { get; set; }

        // ═══════════════════════════════════════════════════════════
        // 方法
        // ═══════════════════════════════════════════════════════════

        /// <summary>
        /// 获取该效果所属的结算堆叠层。
        ///   Layer 0: 反制 (Counter)
        ///   Layer 1: 防御/修正 (Shield, Armor, AttackBuff/Debuff, Reflect, DamageReduction, Invincible)
        ///   Layer 2: 伤害 (Damage, Lifesteal, Thorns, ArmorOnHit)
        ///   Layer 3: 功能 (Heal, Stun, Vulnerable, Weak, Draw, Discard, GainEnergy, Silence, Slow)
        /// </summary>
        public int GetSettlementLayer()
        {
            return EffectType switch
            {
                // Layer 0: 反制
                EffectType.Counter => 0,

                // Layer 1: 防御/修正
                EffectType.Shield         => 1,
                EffectType.Armor          => 1,
                EffectType.AttackBuff     => 1,
                EffectType.AttackDebuff   => 1,
                EffectType.Reflect        => 1,
                EffectType.DamageReduction=> 1,
                EffectType.Invincible     => 1,

                // Layer 2: 伤害
                EffectType.Damage         => 2,
                EffectType.Lifesteal      => 2,
                EffectType.Thorns         => 2,
                EffectType.ArmorOnHit     => 2,
                EffectType.Pierce         => 2,

                // Layer 3: 功能（默认）
                _ => 3
            };
        }

        /// <summary>
        /// 判断该效果是否为触发式效果（在堆叠2层步骤2处理）。
        /// 触发式效果：Lifesteal、Thorns、ArmorOnHit。
        /// </summary>
        public bool IsTriggerEffect()
        {
            return EffectType == EffectType.Lifesteal
                || EffectType == EffectType.Thorns
                || EffectType == EffectType.ArmorOnHit;
        }
    }

    // ═══════════════════════════════════════════════════════════════
    // 辅助类型
    // ═══════════════════════════════════════════════════════════════

    /// <summary>
    /// 效果条件 —— 描述一个运行时可检查的谓词。
    ///
    /// 用于 CardEffect.EffectConditions（Conditional 模式）和 CardConfig.PlayConditions（打出限制）。
    /// 所有条件由 EffectConditionChecker.Evaluate() 统一求值。
    /// </summary>
    public class EffectCondition
    {
        /// <summary>条件类型（决定检查哪种运行时状态）</summary>
        public EffectConditionType ConditionType { get; set; }

        /// <summary>
        /// 数值参数（配合数量/百分比类型使用）。
        /// 例如：MyHandCardCountAtMost = 1 → 手牌 &lt;= 1 张。
        /// </summary>
        public int ConditionValue { get; set; }

        /// <summary>
        /// Buff 类型参数（配合 MyHasBuffType / EnemyHasBuffType 使用）。
        /// </summary>
        public BuffType ConditionBuffType { get; set; }

        /// <summary>
        /// 是否对结果取反（NOT 语义）。
        /// true → 条件不满足时才通过（如"敌方 未出 伤害牌"）。
        /// </summary>
        public bool Negate { get; set; } = false;

        /// <summary>返回条件的可读描述（用于编辑器 / 日志）</summary>
        public override string ToString()
        {
            string desc = ConditionType switch
            {
                EffectConditionType.EnemyPlayedDamageCard   => "敌方出了伤害牌",
                EffectConditionType.EnemyPlayedDefenseCard  => "敌方出了防御牌",
                EffectConditionType.EnemyPlayedCounterCard  => "敌方出了反制牌",
                EffectConditionType.MyDeckIsEmpty            => "我方牌库为空",
                EffectConditionType.MyHandCardCountAtMost   => $"手牌 ≤ {ConditionValue} 张",
                EffectConditionType.MyHandCardCountAtLeast  => $"手牌 ≥ {ConditionValue} 张",
                EffectConditionType.MyHpPercentAtMost       => $"我方血量 ≤ {ConditionValue}%",
                EffectConditionType.EnemyHpPercentAtMost    => $"敌方血量 ≤ {ConditionValue}%",
                EffectConditionType.EnemyIsStunned          => "敌方处于眩晕",
                EffectConditionType.MyHasBuffType           => $"我方有 {ConditionBuffType} Buff",
                EffectConditionType.EnemyHasBuffType        => $"敌方有 {ConditionBuffType} Buff",
                _                                           => ConditionType.ToString()
            };
            return Negate ? $"非({desc})" : desc;
        }
    }

    /// <summary>
    /// 效果参数 —— 用于复杂效果的额外配置。
    /// </summary>
    public class EffectParams
    {
        /// <summary>百分比值（用于反伤、吸血等）</summary>
        public int Percent { get; set; }

        /// <summary>次要数值（如额外护甲、额外伤害）</summary>
        public int SecondaryValue { get; set; }

        /// <summary>可反制的效果类型列表（用于反制牌）</summary>
        public List<EffectType> CounterableTypes { get; set; }

        /// <summary>触发器类型（用于被动效果）</summary>
        public string TriggerType { get; set; }

        /// <summary>触发器参数</summary>
        public string TriggerParam { get; set; }
    }

    /// <summary>
    /// 子效果 —— 用于组合效果（如"造成伤害并治疗自身"）。
    /// </summary>
    public class SubEffect
    {
        /// <summary>子效果类型</summary>
        public EffectType EffectType { get; set; }

        /// <summary>子效果数值</summary>
        public int Value { get; set; }

        /// <summary>子效果目标（可选，不填则沿用父效果目标）</summary>
        public CardTargetType? TargetOverride { get; set; }

        /// <summary>子效果持续回合</summary>
        public int Duration { get; set; }
    }
}