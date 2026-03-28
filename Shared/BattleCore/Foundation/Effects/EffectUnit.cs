#pragma warning disable CS8632

using System.Collections.Generic;
using CardMoba.Protocol.Enums;

namespace CardMoba.BattleCore.Foundation
{
    /// <summary>
    /// 效果原子（EffectUnit）—— BattleCore V2 中最小的可执行效果单元。
    /// 一张卡牌可包含多个 EffectUnit，按 Layer → 出牌顺序依次执行。
    /// EffectUnit 是纯数据，不含逻辑；由 HandlerPool 分发给对应的 IEffectHandler 执行。
    /// </summary>
    public class EffectUnit
    {
        /// <summary>
        /// 效果唯一 ID（在同一张卡内唯一，如 "dmg_01"）。
        /// 供 DynamicParamResolver 在后续效果中引用本效果的结果，例如：
        /// {{preEffect.dmg_01.totalRealHpDamage}}
        /// </summary>
        public string EffectId { get; set; } = string.Empty;

        /// <summary>效果类型（决定调用哪个 Handler）</summary>
        public EffectType Type { get; set; }

        /// <summary>
        /// 目标类型表达式（如 "Enemy", "Self", "AllEnemies", "AllyWithLowestHp"）。
        /// 由 TargetResolver 解析为实际 Entity 列表。
        /// </summary>
        public string TargetType { get; set; } = "Enemy";

        /// <summary>
        /// 数值表达式，支持字面量或动态表达式。
        /// 示例：
        ///   "10"                                           —— 固定值
        ///   "{{(6 - context.self.hand.count) * 3}}"       —— 手牌数相关
        ///   "{{preEffect.dmg_01.totalRealHpDamage}}"      —— 引用前置效果实际结果
        /// </summary>
        public string ValueExpression { get; set; } = "0";

        /// <summary>效果所属结算层（决定在五层结算中的执行阶段）</summary>
        public SettlementLayer Layer { get; set; }

        /// <summary>
        /// 打出条件列表（全部满足时效果才执行）。
        /// 每个字符串是一个条件表达式，由 ConditionChecker 解析。
        /// </summary>
        public List<string> Conditions { get; set; } = new List<string>();

        /// <summary>
        /// 扩展参数字典（各 Handler 自定义读取）。
        /// 示例：
        ///   GenerateCard：{ "configId": "fireball", "targetZone": "Hand" }
        ///   AddBuff：{ "buffConfigId": "burn", "duration": "3" }
        /// </summary>
        public Dictionary<string, string> Params { get; set; } = new Dictionary<string, string>();

        // ══════════════════════════════════════════════════════════
        // 运行时缓存（不序列化，由 HandlerPool.Execute 在执行前填充）
        // ══════════════════════════════════════════════════════════

        /// <summary>
        /// 动态解析后的整数数值（由 HandlerPool 在调用 Handler 前通过 DynamicParamResolver 求值填入）。
        /// Handler 内应优先读取此字段，而非直接解析 ValueExpression。
        /// </summary>
        public int ResolvedValue { get; set; }

        /// <summary>
        /// 此效果的条件检查是否已通过（由 HandlerPool.Execute 前置检查后设置）。
        /// Handler 内不需要再次检查条件，此字段仅供调试/日志使用。
        /// </summary>
        public bool ConditionPassed { get; set; } = true;
    }
}