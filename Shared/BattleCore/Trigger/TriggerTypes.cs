using System;
using System.Collections.Generic;
using CardMoba.BattleCore.Context;

namespace CardMoba.BattleCore.Trigger
{
    /// <summary>
    /// 触发时机枚举 —— 定义所有可能的触发点。
    /// </summary>
    public enum TriggerTiming
    {
        /// <summary>无触发</summary>
        None = 0,

        // ══════════════════════════════════════════════════════════
        // 回合相关
        // ══════════════════════════════════════════════════════════

        /// <summary>回合开始时（所有玩家）</summary>
        OnRoundStart = 101,

        /// <summary>回合结束时（所有玩家）</summary>
        OnRoundEnd = 102,

        /// <summary>操作窗口开始时</summary>
        OnOperationWindowStart = 103,

        /// <summary>操作窗口结束时</summary>
        OnOperationWindowEnd = 104,

        /// <summary>结算开始时</summary>
        OnSettlementStart = 105,

        /// <summary>结算结束时</summary>
        OnSettlementEnd = 106,

        // ══════════════════════════════════════════════════════════
        // 伤害相关
        // ══════════════════════════════════════════════════════════

        /// <summary>造成伤害前</summary>
        BeforeDealDamage = 201,

        /// <summary>造成伤害后</summary>
        AfterDealDamage = 202,

        /// <summary>受到伤害前</summary>
        BeforeTakeDamage = 203,

        /// <summary>受到伤害后</summary>
        AfterTakeDamage = 204,

        /// <summary>护盾被破时</summary>
        OnShieldBroken = 205,

        /// <summary>生命值首次低于阈值时</summary>
        OnHpBelowThreshold = 206,

        // ══════════════════════════════════════════════════════════
        // 卡牌相关
        // ══════════════════════════════════════════════════════════

        /// <summary>出牌前</summary>
        BeforePlayCard = 301,

        /// <summary>出牌后</summary>
        AfterPlayCard = 302,

        /// <summary>抽牌时</summary>
        OnDrawCard = 303,

        /// <summary>弃牌时</summary>
        OnDiscardCard = 304,

        /// <summary>卡牌被反制时</summary>
        OnCardCountered = 305,

        /// <summary>手牌满时</summary>
        OnHandFull = 306,

        // ══════════════════════════════════════════════════════════
        // 生死相关
        // ══════════════════════════════════════════════════════════

        /// <summary>濒死时</summary>
        OnNearDeath = 401,

        /// <summary>死亡时</summary>
        OnDeath = 402,

        /// <summary>击杀敌人时</summary>
        OnKill = 403,

        /// <summary>复活时</summary>
        OnResurrect = 404,

        // ══════════════════════════════════════════════════════════
        // 治疗/增益相关
        // ══════════════════════════════════════════════════════════

        /// <summary>被治疗时</summary>
        OnHealed = 501,

        /// <summary>获得护盾时</summary>
        OnGainShield = 502,

        /// <summary>获得增益 Buff 时</summary>
        OnGainBuff = 503,

        /// <summary>获得减益 Debuff 时</summary>
        OnGainDebuff = 504,

        /// <summary>Buff 被驱散时</summary>
        OnBuffDispelled = 505,

        // ══════════════════════════════════════════════════════════
        // 分路/团队相关
        // ══════════════════════════════════════════════════════════

        /// <summary>分路胜利时</summary>
        OnLaneVictory = 601,

        /// <summary>分路失败时</summary>
        OnLaneDefeat = 602,

        /// <summary>队友死亡时</summary>
        OnTeammateDeath = 603,

        /// <summary>换路时</summary>
        OnLaneSwap = 604,

        /// <summary>支援时</summary>
        OnSupport = 605,
    }

    /// <summary>
    /// 伤害来源类型 —— 用于触发器区分伤害性质，决定反弹/反伤是否响应。
    /// </summary>
    public enum DamageSourceType
    {
        /// <summary>来自卡牌打出的伤害（瞬策牌/定策牌），默认值，可触发 Reflect/Thorns</summary>
        CardDamage = 0,

        /// <summary>持续伤害（中毒/燃烧/流血），不触发 Reflect/Thorns</summary>
        DotDamage = 1,

        /// <summary>自伤（来源与目标相同），不触发 Reflect/Thorns</summary>
        SelfDamage = 2,
    }

    /// <summary>
    /// 触发器执行上下文 —— 传递给触发器回调的信息。
    /// </summary>
    public class TriggerContext
    {
        /// <summary>战斗上下文</summary>
        public BattleContext BattleContext { get; set; }

        /// <summary>触发时机</summary>
        public TriggerTiming Timing { get; set; }

        /// <summary>
        /// 触发源玩家 ID —— 含义随触发时机不同而不同：
        ///
        /// ┌──────────────────────┬──────────────────┬──────────────────┐
        /// │ 触发时机              │ SourcePlayerId   │ TargetPlayerId   │
        /// ├──────────────────────┼──────────────────┼──────────────────┤
        /// │ BeforeDealDamage     │ 攻击方           │ 被攻击方         │
        /// │ AfterDealDamage      │ 攻击方           │ 被攻击方         │
        /// │ BeforeTakeDamage     │ 攻击方           │ 受伤方           │
        /// │ AfterTakeDamage      │ 受伤方（被打方） │ 攻击方（打人方） │ ← 注意！与上方相反
        /// │ OnNearDeath          │ 攻击方           │ 濒死方           │
        /// │ OnShieldBroken       │ 攻击方           │ 护盾破碎方       │
        /// │ BeforePlayCard       │ 出牌方           │ 牌的目标         │
        /// │ AfterPlayCard        │ 出牌方           │ 牌的目标         │
        /// │ OnRoundStart/End     │ 触发 Buff 归属方  │ (通常为空)       │
        /// └──────────────────────┴──────────────────┴──────────────────┘
        ///
        /// 特别注意 AfterTakeDamage：
        ///   SourcePlayerId = 受伤方（己方）—— Thorns/Reflect 用此判断"是我被打了"
        ///   TargetPlayerId = 攻击方（对方）—— Thorns/Reflect 用此作为反伤目标
        ///   这一约定在 BuffManager.RegisterBuffTriggers(Thorns)、SettlementEngine.ResolveLayer2_Step1
        ///   和 DamageHelper.ApplyDamage 三处均保持一致。
        /// </summary>
        public string SourcePlayerId { get; set; }

        /// <summary>触发目标玩家 ID —— 含义请参考 SourcePlayerId 的注释表格。</summary>
        public string TargetPlayerId { get; set; }

        /// <summary>相关数值（如伤害值、治疗量等）</summary>
        public int Value { get; set; }

        /// <summary>相关卡牌（如被打出的卡、被反制的卡）</summary>
        public PlayedCard RelatedCard { get; set; }

        /// <summary>额外数据（用于特殊情况）</summary>
        public Dictionary<string, object> ExtraData { get; set; } = new Dictionary<string, object>();

        /// <summary>是否应该阻止后续处理（如伤害被完全抵消）</summary>
        public bool ShouldCancel { get; set; } = false;

        /// <summary>修改后的数值（触发器可以修改）</summary>
        public int ModifiedValue { get; set; }

        /// <summary>
        /// 伤害来源类型 —— 仅在伤害相关触发时机（AfterTakeDamage 等）有效。
        /// 用于 Thorns/Reflect 等触发器判断是否响应：
        /// 仅 CardDamage（来自卡牌）才触发反弹/反伤；DOT 和自伤不触发。
        /// </summary>
        public DamageSourceType DamageSource { get; set; } = DamageSourceType.CardDamage;
    }

    /// <summary>
    /// 触发器来源类型 —— 标记触发器的归属系统，决定生命周期管理方式。
    /// 
    /// 生命周期规则：
    ///   Card  → 由 TriggerManager.OnRoundEnd 自行衰减（无 SourceId 关联）
    ///   Buff  → 由 BuffManager 控制，TriggerManager 跳过衰减（单一所有权 R-03）
    ///   Relic → 永久存在于对局内，不受 BuffManager/TriggerManager 衰减影响，
    ///           仅在对局结束（EndBattle）时由 RelicManager 统一清理
    /// </summary>
    public enum TriggerSourceType
    {
        /// <summary>来自卡牌效果（一次性或有限回合）</summary>
        Card = 0,

        /// <summary>来自 Buff（受 BuffManager 生命周期管控，可被清除）</summary>
        Buff = 1,

        /// <summary>来自遗物（对局内永久，不受清除 Buff 影响）</summary>
        Relic = 2,

        /// <summary>来自英雄被动技能（对局内永久）</summary>
        HeroPassive = 3,
    }

    /// <summary>
    /// 触发器实例 —— 表示一个已注册的触发效果。
    /// 
    /// 优先级约定（Priority 数值越小越先执行）：
    ///   0   - 99  : 系统级（反制、无敌判断）
    ///   100 - 199 : 增益效果（力量+、护甲+、护盾+、回血）
    ///   200 - 299 : 己方遗物增益
    ///   300 - 399 : 削弱效果（力量-、易伤、虚弱）
    ///   400 - 499 : 敌方遗物削弱
    ///   500 - 599 : 伤害效果（默认值）
    ///   900 - 999 : 传说特殊牌
    /// </summary>
    public class TriggerInstance
    {
        /// <summary>触发器唯一 ID</summary>
        public string TriggerId { get; set; } = string.Empty;

        /// <summary>显示名称</summary>
        public string TriggerName { get; set; } = string.Empty;

        /// <summary>触发时机</summary>
        public TriggerTiming Timing { get; set; }

        /// <summary>所属玩家 ID</summary>
        public string OwnerPlayerId { get; set; }

        /// <summary>
        /// 主优先级（数值越小越先执行，默认 500）。
        /// 见类注释中的分段约定。
        /// </summary>
        public int Priority { get; set; } = 500;

        /// <summary>
        /// 次级优先级（主优先级相同时的打破平局字段，策划配表控制，默认 0）。
        /// 数值越小越先执行。
        /// </summary>
        public int SubPriority { get; set; } = 0;

        /// <summary>剩余触发次数（-1 表示无限）</summary>
        public int RemainingTriggers { get; set; } = -1;

        /// <summary>剩余持续回合数（-1 表示永久）</summary>
        public int RemainingRounds { get; set; } = -1;

        /// <summary>触发条件（返回 true 时才触发）</summary>
        public Func<TriggerContext, bool> Condition { get; set; }

        /// <summary>触发效果</summary>
        public Action<TriggerContext> Effect { get; set; }

        /// <summary>
        /// 来源 ID（如卡牌 RuntimeId、Buff RuntimeId、遗物 RelicId）。
        /// 与 SourceType 配合使用决定生命周期管理方式。
        /// </summary>
        public string SourceId { get; set; } = string.Empty;

        /// <summary>
        /// 来源类型 —— 决定生命周期管理方式（见 TriggerSourceType 注释）。
        /// </summary>
        public TriggerSourceType SourceType { get; set; } = TriggerSourceType.Card;

        /// <summary>是否已被标记为移除</summary>
        public bool IsMarkedForRemoval { get; set; } = false;

        /// <summary>
        /// 检查是否应该移除此触发器。
        /// </summary>
        public bool ShouldRemove => IsMarkedForRemoval || RemainingTriggers == 0 || RemainingRounds == 0;
    }
}
