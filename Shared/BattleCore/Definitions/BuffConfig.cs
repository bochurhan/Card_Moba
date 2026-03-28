using CardMoba.Protocol.Enums;

namespace CardMoba.BattleCore.Definitions
{
    /// <summary>
    /// Buff 的静态配置定义。
    /// </summary>
    public class BuffConfig
    {
        /// <summary>Buff 唯一标识。</summary>
        public string BuffId { get; set; } = string.Empty;

        /// <summary>Buff 显示名称。</summary>
        public string BuffName { get; set; } = string.Empty;

        /// <summary>Buff 描述。</summary>
        public string Description { get; set; } = string.Empty;

        /// <summary>Buff 类型。</summary>
        public BuffType BuffType { get; set; } = BuffType.Unknown;

        /// <summary>是否为增益；false 表示减益。</summary>
        public bool IsBuff { get; set; } = true;

        /// <summary>叠加规则。</summary>
        public BuffStackRule StackRule { get; set; } = BuffStackRule.RefreshDuration;

        /// <summary>最大叠加层数，仅 StackValue 模式生效。</summary>
        public int MaxStacks { get; set; } = 99;

        /// <summary>默认持续回合数；0 表示永久。</summary>
        public int DefaultDuration { get; set; } = 1;

        /// <summary>默认数值。</summary>
        public int DefaultValue { get; set; } = 0;

        /// <summary>是否可被驱散。</summary>
        public bool IsDispellable { get; set; } = true;

        /// <summary>是否可被净化。</summary>
        public bool IsPurgeable { get; set; } = true;

        /// <summary>是否隐藏，不在 UI 中显示。</summary>
        public bool IsHidden { get; set; } = false;

        /// <summary>触发时机，用于触发型 Buff。</summary>
        public BuffTriggerTiming TriggerTiming { get; set; } = BuffTriggerTiming.None;

        /// <summary>图标路径。</summary>
        public string IconPath { get; set; } = string.Empty;
    }
}
