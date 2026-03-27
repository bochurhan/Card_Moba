#pragma warning disable CS8632

using System.Collections.Generic;

namespace CardMoba.BattleCore.Foundation
{
    /// <summary>
    /// BattleCore 运行时卡牌定义。
    /// 由上层配置系统通过 CardDefinitionProvider 注入。
    /// </summary>
    public class BattleCardDefinition
    {
        public string ConfigId { get; set; } = string.Empty;
        public bool IsExhaust { get; set; }
        public bool IsStatCard { get; set; }
        public int EnergyCost { get; set; }

        /// <summary>
        /// 当前配置的升级目标。
        /// 为空表示无升级版。
        /// </summary>
        public string UpgradedConfigId { get; set; } = string.Empty;

        public List<EffectUnit> Effects { get; set; } = new List<EffectUnit>();
    }
}
