#pragma warning disable CS8632

using System.Collections.Generic;
using CardMoba.Protocol.Enums;

namespace CardMoba.BattleCore.Foundation
{
    /// <summary>
    /// 效果执行结果 —— 记录单个 EffectUnit 执行后的关键数值。
    /// 供同一张卡后续效果通过 DynamicParamResolver 引用（如死亡收割吸取实际伤害量）。
    /// </summary>
    public class EffectResult
    {
        /// <summary>对应的效果 ID（与 EffectUnit.EffectId 一致）</summary>
        public string EffectId { get; set; } = string.Empty;

        /// <summary>效果类型</summary>
        public EffectType Type { get; set; }

        /// <summary>是否执行成功（条件不满足、被拦截等情况下为 false）</summary>
        public bool Success { get; set; } = true;

        /// <summary>
        /// 对所有目标造成的实际 HP 扣减总量（扣除护盾/护甲后的值）。
        /// 用于死亡收割等"基于实际伤害"的计算。
        /// </summary>
        public int TotalRealHpDamage { get; set; }

        /// <summary>对所有目标造成的实际治疗总量</summary>
        public int TotalRealHeal { get; set; }

        /// <summary>对所有目标附加的护盾总量</summary>
        public int TotalRealShield { get; set; }

        /// <summary>每个目标的实际结果明细（key = 目标 EntityId）</summary>
        public Dictionary<string, int> PerTargetValues { get; set; } = new Dictionary<string, int>();

        /// <summary>扩展输出数据（Handler 自定义）</summary>
        public Dictionary<string, object> Extra { get; set; } = new Dictionary<string, object>();
    }
}