using CardMoba.Protocol.Enums;

namespace CardMoba.BattleCore.Buff
{
    /// <summary>
    /// Buff 配置 —— 定义一个 Buff 的静态属性。
    /// 通常从配置文件读取，用于创建 BuffInstance。
    /// </summary>
    public class BuffConfig
    {
        /// <summary>Buff 唯一标识</summary>
        public string BuffId { get; set; } = string.Empty;

        /// <summary>Buff 显示名称</summary>
        public string BuffName { get; set; } = string.Empty;

        /// <summary>Buff 描述</summary>
        public string Description { get; set; } = string.Empty;

        /// <summary>Buff 类型</summary>
        public BuffType BuffType { get; set; } = BuffType.Unknown;

        /// <summary>是否为增益（false 表示减益/Debuff）</summary>
        public bool IsBuff { get; set; } = true;

        /// <summary>叠加规则</summary>
        public BuffStackRule StackRule { get; set; } = BuffStackRule.RefreshDuration;

        /// <summary>最大叠加层数（仅当 StackRule 为 StackValue 时有效）</summary>
        public int MaxStacks { get; set; } = 99;

        /// <summary>默认持续回合数（0 表示永久）</summary>
        public int DefaultDuration { get; set; } = 1;

        /// <summary>默认数值（如护甲值、伤害值等）</summary>
        public int DefaultValue { get; set; } = 0;

        /// <summary>是否可被驱散</summary>
        public bool IsDispellable { get; set; } = true;

        /// <summary>是否可被净化（针对减益）</summary>
        public bool IsPurgeable { get; set; } = true;

        /// <summary>是否隐藏（不在 UI 中显示）</summary>
        public bool IsHidden { get; set; } = false;

        /// <summary>触发时机（用于触发类 Buff）</summary>
        public BuffTriggerTiming TriggerTiming { get; set; } = BuffTriggerTiming.None;

        /// <summary>图标路径（用于 UI 显示）</summary>
        public string IconPath { get; set; } = string.Empty;

        /// <summary>
        /// 创建一个 BuffInstance。
        /// </summary>
        /// <param name="sourcePlayerId">施加此 Buff 的玩家 ID</param>
        /// <param name="value">效果数值（可覆盖默认值）</param>
        /// <param name="duration">持续回合数（可覆盖默认值）</param>
        /// <returns>新的 BuffInstance</returns>
        public BuffInstance CreateInstance(string sourcePlayerId, int? value = null, int? duration = null)
        {
            return new BuffInstance
            {
                BuffId = this.BuffId,
                BuffName = this.BuffName,
                BuffType = this.BuffType,
                IsBuff = this.IsBuff,
                StackRule = this.StackRule,
                MaxStacks = this.MaxStacks,
                Value = value ?? this.DefaultValue,
                Stacks = 1,
                RemainingRounds = duration ?? this.DefaultDuration,
                SourcePlayerId = sourcePlayerId,
                TriggerTiming = this.TriggerTiming,
                IsDispellable = this.IsDispellable,
                IsPurgeable = this.IsPurgeable,
            };
        }
    }
}
