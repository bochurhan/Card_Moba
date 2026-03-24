
#pragma warning disable CS8632

using System;
using System.Collections.Generic;
using CardMoba.BattleCore.Managers;

namespace CardMoba.BattleCore.Foundation
{
    /// <summary>
    /// 触发器数据（TriggerUnit）—— 描述"在何时、满足何种条件、执行什么效果"。
    /// TriggerUnit 是纯数据；由 TriggerManager 持有并在触发时机到来时评估条件、推入 PendingQueue。
    ///
    /// ⚠️ 注意：TriggerUnit 不含任何逻辑代码，所有行为通过 Effects 列表中的 EffectUnit 描述。
    /// </summary>
    public class TriggerUnit
    {
        // ══════════════════════════════════════════════════════════
        // 身份
        // ══════════════════════════════════════════════════════════

        /// <summary>运行时唯一 ID（由 TriggerManager.Register 自动生成，格式 "trig_xxxx"）</summary>
        public string TriggerId { get; set; } = string.Empty;

        /// <summary>描述性名称（仅供调试日志使用，如 "吸血-AfterDealDamage"）</summary>
        public string TriggerName { get; set; } = string.Empty;

        // ══════════════════════════════════════════════════════════
        // 归属
        // ══════════════════════════════════════════════════════════

        /// <summary>归属玩家 ID（决定 context.self 的指向）</summary>
        public string OwnerPlayerId { get; set; } = string.Empty;

        /// <summary>
        /// 来源 ID（Buff 的 RuntimeId 或卡牌的 InstanceId）。
        /// 用于生命周期关联：Buff 移除时，TriggerManager 通过此 ID 注销对应触发器。
        /// </summary>
        public string SourceId { get; set; } = string.Empty;

        // ══════════════════════════════════════════════════════════
        // 触发配置
        // ══════════════════════════════════════════════════════════

        /// <summary>触发时机</summary>
        public TriggerTiming Timing { get; set; }

        /// <summary>
        /// 优先级（越小越先触发）。
        /// 约定：0-99 系统级，100-199 增益，200-299 防御，300-399 削弱，400-499 伤害，500-899 普通，900-999 传说
        /// </summary>
        public int Priority { get; set; } = 500;

        /// <summary>
        /// 剩余触发次数（-1 = 无限次）。
        /// 每次触发后自动 -1，归零后由 TriggerManager 自动注销。
        /// </summary>
        public int RemainingTriggers { get; set; } = -1;

        /// <summary>
        /// 剩余回合数（-1 = 永久，跟随 Buff 生命周期）。
        /// 每回合结束由 TriggerManager.TickDecay 自动 -1，归零后注销。
        /// </summary>
        public int RemainingRounds { get; set; } = -1;

        // ══════════════════════════════════════════════════════════
        // 条件与效果
        // ══════════════════════════════════════════════════════════

        /// <summary>
        /// 触发条件列表（全部满足才触发）。
        /// 每个字符串是条件表达式，由 ConditionChecker 解析，
        /// 可访问触发上下文中的数值字段（如 "trigCtx.value > 0"、"trigCtx.round >= 2"、"trigCtx.extra.damage > 3"）。
        /// </summary>
        public List<string> Conditions { get; set; } = new List<string>();

        /// <summary>
        /// 触发后推入 PendingEffectQueue 的效果列表。
        /// 执行时 OwnerPlayerId 作为施法者，目标由各 EffectUnit.TargetType 解析。
        /// 与 InlineExecute 二选一：优先使用 InlineExecute（若不为 null）。
        /// </summary>
        public List<EffectUnit> Effects { get; set; } = new List<EffectUnit>();

        /// <summary>
        /// 内联执行委托（可选）。
        /// 供 BuffManager 注册 DoT / Lifesteal 等逻辑时直接传入 lambda，
        /// 避免将复杂计算序列化为 EffectUnit 字符串。
        ///
        /// 参数：(BattleContext ctx, TriggerContext trigCtx)
        ///
        /// ⚠️ 若设置此委托，TriggerManager.Fire 将直接调用它，而不推入 PendingQueue。
        ///    适用于 Buff 生命周期触发器；常规卡牌触发器应使用 Effects 列表。
        /// </summary>
        public Action<Context.BattleContext, TriggerContext>? InlineExecute { get; set; }
    }
}
