#pragma warning disable CS8632

namespace CardMoba.BattleCore.Foundation
{
    /// <summary>
    /// 触发时机枚举 —— 定义触发器在何时被激活。
    ///
    /// 段落划分：
    ///   100 段：回合生命周期
    ///   200 段：出伤 / 受伤流程
    ///   300 段：打牌动作
    ///   400 段：死亡相关
    ///   500 段：状态变化（Buff / 卡牌 / 治疗 / 护盾）
    ///
    /// ⚠️ Protocol/Enums/TriggerTiming.cs 已删除独立定义，统一以此文件为权威来源。
    /// </summary>
    public enum TriggerTiming
    {
        // ── 回合生命周期 ────────────────────────────────────────────
        /// <summary>回合开始时（所有玩家）</summary>
        OnRoundStart = 101,

        /// <summary>回合结束时（主结算完成后，Buff 衰减前）</summary>
        OnRoundEnd = 102,

        // ── 出伤流程 ─────────────────────────────────────────────────
        /// <summary>造成伤害前（可修正或取消）</summary>
        BeforeDealDamage = 201,

        /// <summary>造成伤害后（实际伤害已确定，可触发吸血/连击）</summary>
        AfterDealDamage = 202,

        /// <summary>受到伤害前（可触发无敌判定/减伤）</summary>
        BeforeTakeDamage = 203,

        /// <summary>
        /// 受到伤害后（可触发荆棘/受击获甲）。
        /// ⚠️ 方向约定：SourceEntityId = 受伤方，TargetEntityId = 攻击方（与其余时机相反）。
        /// </summary>
        AfterTakeDamage = 204,

        /// <summary>护盾被完全击破时</summary>
        OnShieldBroken = 205,

        // ── 打牌动作 ─────────────────────────────────────────────────
        /// <summary>即将打出一张牌前（可触发沉默拦截）</summary>
        BeforePlayCard = 301,

        /// <summary>打出一张牌后</summary>
        AfterPlayCard = 302,

        // ── 死亡相关 ─────────────────────────────────────────────────
        /// <summary>HP 降至 ≤ 0 但尚未确认死亡时（复活技能触发点）</summary>
        OnNearDeath = 401,

        /// <summary>死亡确认后</summary>
        OnDeath = 402,

        // ── 状态变化 ─────────────────────────────────────────────────
        /// <summary>Buff 被添加时</summary>
        OnBuffAdded = 501,

        /// <summary>Buff 被移除时</summary>
        OnBuffRemoved = 502,

        /// <summary>抽到一张牌时</summary>
        OnCardDrawn = 503,

        /// <summary>状态牌被持有时（ScanStatCards 扫描触发）</summary>
        // 由 CardManager 在回合末扫描手牌中的状态牌时触发。
        OnStatCardHeld = 504,

        /// <summary>受到治疗时</summary>
        OnHealed = 505,

        /// <summary>获得护盾时</summary>
        OnGainShield = 506,
    }
}
