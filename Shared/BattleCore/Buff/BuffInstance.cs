using System.Collections.Generic;
using CardMoba.Protocol.Enums;

namespace CardMoba.BattleCore.Buff
{
    /// <summary>
    /// Buff 实例 —— 表示一个正在生效的增益/减益效果。
    /// 替代原 PlayerBattleState 中的简化版 BuffInstance。
    /// </summary>
    public class BuffInstance
    {
        /// <summary>运行时唯一 ID（用于精确定位）</summary>
        public string RuntimeId { get; set; } = string.Empty;

        /// <summary>Buff 配置 ID（对应 BuffConfig.BuffId）</summary>
        public string BuffId { get; set; } = string.Empty;

        /// <summary>Buff 显示名称</summary>
        public string BuffName { get; set; } = string.Empty;

        /// <summary>Buff 类型</summary>
        public BuffType BuffType { get; set; } = BuffType.Unknown;

        /// <summary>是否为增益（false 表示减益）</summary>
        public bool IsBuff { get; set; } = true;

        /// <summary>叠加规则</summary>
        public BuffStackRule StackRule { get; set; } = BuffStackRule.RefreshDuration;

        /// <summary>最大叠加层数</summary>
        public int MaxStacks { get; set; } = 99;

        /// <summary>当前层数（用于可叠加 Buff）</summary>
        public int Stacks { get; set; } = 1;

        /// <summary>效果数值（如护甲值、伤害值等）</summary>
        public int Value { get; set; }

        /// <summary>剩余回合数（0 表示永久，-1 表示本回合结束时移除）</summary>
        public int RemainingRounds { get; set; }

        /// <summary>来源玩家 ID</summary>
        public string SourcePlayerId { get; set; } = string.Empty;

        /// <summary>触发时机</summary>
        public BuffTriggerTiming TriggerTiming { get; set; } = BuffTriggerTiming.None;

        /// <summary>是否可被驱散</summary>
        public bool IsDispellable { get; set; } = true;

        /// <summary>是否可被净化</summary>
        public bool IsPurgeable { get; set; } = true;

        /// <summary>已触发次数（用于限制触发次数的 Buff）</summary>
        public int TriggerCount { get; set; } = 0;

        /// <summary>最大触发次数（0 表示无限制）</summary>
        public int MaxTriggerCount { get; set; } = 0;

        /// <summary>
        /// 此 Buff 在 TriggerManager 中注册的触发器 ID 列表。
        /// Buff 移除时，BuffManager 会用此列表批量注销对应触发器。
        /// </summary>
        public List<string> RegisteredTriggerIds { get; } = new List<string>();

        /// <summary>
        /// 获取当前 Buff 的总效果值（考虑层数）。
        /// </summary>
        public int TotalValue => Value * Stacks;

        /// <summary>
        /// 检查是否已过期。
        /// </summary>
        public bool IsExpired => RemainingRounds < 0 || (RemainingRounds == 0 && !IsPermanent);

        /// <summary>
        /// 是否为永久 Buff（RemainingRounds 初始为 0）。
        /// </summary>
        public bool IsPermanent { get; set; } = false;

        /// <summary>
        /// 尝试叠加另一个同类 Buff。
        /// </summary>
        /// <param name="other">待叠加的 Buff</param>
        /// <returns>是否成功叠加</returns>
        public bool TryStack(BuffInstance other)
        {
            if (BuffId != other.BuffId) return false;

            switch (StackRule)
            {
                case BuffStackRule.None:
                    // 不可叠加，保持原样
                    return true;

                case BuffStackRule.RefreshDuration:
                    // 刷新持续时间
                    if (other.RemainingRounds > RemainingRounds)
                        RemainingRounds = other.RemainingRounds;
                    if (other.Value > Value)
                        Value = other.Value;
                    return true;

                case BuffStackRule.StackValue:
                    // 叠加层数
                    Stacks += other.Stacks;
                    if (Stacks > MaxStacks)
                        Stacks = MaxStacks;
                    // 同时刷新持续时间
                    if (other.RemainingRounds > RemainingRounds)
                        RemainingRounds = other.RemainingRounds;
                    return true;

                case BuffStackRule.KeepHighest:
                    // 保留最高值
                    if (other.Value > Value)
                    {
                        Value = other.Value;
                        RemainingRounds = other.RemainingRounds;
                    }
                    return true;

                case BuffStackRule.Independent:
                    // 独立存在，不叠加
                    return false;

                default:
                    return false;
            }
        }

        /// <summary>
        /// 减少一层叠加。
        /// </summary>
        /// <returns>移除后是否应该删除整个 Buff</returns>
        public bool RemoveStack()
        {
            if (Stacks <= 1)
                return true;

            Stacks--;
            return false;
        }

        /// <summary>
        /// 克隆此 Buff 实例。
        /// </summary>
        public BuffInstance Clone()
        {
            return new BuffInstance
            {
                RuntimeId = this.RuntimeId,
                BuffId = this.BuffId,
                BuffName = this.BuffName,
                BuffType = this.BuffType,
                IsBuff = this.IsBuff,
                StackRule = this.StackRule,
                MaxStacks = this.MaxStacks,
                Stacks = this.Stacks,
                Value = this.Value,
                RemainingRounds = this.RemainingRounds,
                SourcePlayerId = this.SourcePlayerId,
                TriggerTiming = this.TriggerTiming,
                IsDispellable = this.IsDispellable,
                IsPurgeable = this.IsPurgeable,
                TriggerCount = this.TriggerCount,
                MaxTriggerCount = this.MaxTriggerCount,
                IsPermanent = this.IsPermanent,
            };
        }

        public override string ToString()
        {
            return $"[{BuffName}] 类型:{BuffType} 值:{Value} 层数:{Stacks} 剩余:{RemainingRounds}回合";
        }
    }
}
