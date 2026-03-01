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

        /// <summary>触发源玩家 ID</summary>
        public string SourcePlayerId { get; set; }

        /// <summary>触发目标玩家 ID</summary>
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
    /// 触发器实例 —— 表示一个已注册的触发效果。
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

        /// <summary>优先级（数值越大越先执行）</summary>
        public int Priority { get; set; } = 0;

        /// <summary>剩余触发次数（-1 表示无限）</summary>
        public int RemainingTriggers { get; set; } = -1;

        /// <summary>剩余持续回合数（-1 表示永久）</summary>
        public int RemainingRounds { get; set; } = -1;

        /// <summary>触发条件（返回 true 时才触发）</summary>
        public Func<TriggerContext, bool> Condition { get; set; }

        /// <summary>触发效果</summary>
        public Action<TriggerContext> Effect { get; set; }

        /// <summary>来源（如卡牌 ID、Buff ID、英雄技能 ID）</summary>
        public string SourceId { get; set; } = string.Empty;

        /// <summary>是否已被标记为移除</summary>
        public bool IsMarkedForRemoval { get; set; } = false;

        /// <summary>
        /// 检查是否应该移除此触发器。
        /// </summary>
        public bool ShouldRemove => IsMarkedForRemoval || RemainingTriggers == 0 || RemainingRounds == 0;
    }
}
