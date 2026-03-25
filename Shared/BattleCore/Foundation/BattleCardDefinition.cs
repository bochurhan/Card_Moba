#pragma warning disable CS8632

using System.Collections.Generic;

namespace CardMoba.BattleCore.Foundation
{
    /// <summary>
    /// BattleCore 运行时卡牌定义。
    /// BattleCore 只依赖可执行效果列表和少量生命周期标记，
    /// 不依赖外部配置系统的完整结构。
    /// 通过 BattleFactory.CardDefinitionProvider 委托注入，实现与上层配置解耦。
    /// </summary>
    public class BattleCardDefinition
    {
        /// <summary>卡牌配置 ID（与 BattleCard.ConfigId 对应）</summary>
        public string ConfigId { get; set; } = string.Empty;

        /// <summary>是否为消耗牌（使用后进 Consume 区，不进弃牌堆）</summary>
        public bool IsExhaust { get; set; }

        /// <summary>是否为状态牌（不可主动打出，由 ScanStatCards 在回合结束时扫描触发）</summary>
        public bool IsStatCard { get; set; }

        /// <summary>效果列表（Layer 归属、目标类型、数值表达式等）</summary>
        public List<EffectUnit> Effects { get; set; } = new List<EffectUnit>();
    }
}