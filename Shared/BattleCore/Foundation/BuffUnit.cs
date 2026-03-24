
#pragma warning disable CS8632

using System.Collections.Generic;
using CardMoba.Protocol.Enums;

namespace CardMoba.BattleCore.Foundation
{
    /// <summary>
    /// Buff 纯数据（BuffUnit）—— 描述一个运行时 Buff 实例的状态。
    /// BuffUnit 只存储数据，不含任何逻辑。
    /// 逻辑由 BuffManager（CRUD）和 TriggerManager（触发器响应）负责。
    /// </summary>
    public class BuffUnit
    {
        // ══════════════════════════════════════════════════════════
        // 身份
        // ══════════════════════════════════════════════════════════

        /// <summary>运行时唯一 ID（格式 "buff_xxxx"，由 BuffManager.AddBuff 生成）</summary>
        public string RuntimeId { get; set; } = string.Empty;

        /// <summary>对应 Buff 配置的 ID（如 "lifesteal", "burn"）</summary>
        public string ConfigId { get; set; } = string.Empty;

        /// <summary>Buff 显示名称（来自 BuffConfig，仅供日志/UI）</summary>
        public string DisplayName { get; set; } = string.Empty;

        // ══════════════════════════════════════════════════════════
        // 归属
        // ══════════════════════════════════════════════════════════

        /// <summary>归属实体 ID（持有此 Buff 的实体 EntityId）</summary>
        public string OwnerEntityId { get; set; } = string.Empty;

        /// <summary>归属玩家 ID（与 OwnerEntityId 通常一致，玩家单位时相同）</summary>
        public string OwnerPlayerId { get; set; } = string.Empty;

        /// <summary>遗留命名。当前运行时存的是施加来源实体 ID，用于 DoT/反伤等归因。</summary>
        public string SourcePlayerId { get; set; } = string.Empty;

        // ══════════════════════════════════════════════════════════
        // 数值
        // ══════════════════════════════════════════════════════════

        /// <summary>
        /// Buff 数值（含义由具体 Buff 定义）。
        /// 示例：吸血 Buff 的 Value=30 表示吸血 30%；灼烧 Buff 的 Value=5 表示每回合造成 5 伤害。
        /// </summary>
        public int Value { get; set; }

        /// <summary>叠加规则（从 BuffConfig 复制过来，决定同类 Buff 叠加方式）</summary>
        public BuffStackRule StackRule { get; set; } = BuffStackRule.RefreshDuration;

        /// <summary>最大叠加层数（从 BuffConfig 复制，仅 StackValue 模式有效）</summary>
        public int MaxStacks { get; set; } = 99;

        /// <summary>叠加层数（-1 = 不可叠加）</summary>
        public int Stacks { get; set; } = 1;

        // ══════════════════════════════════════════════════════════
        // 生命周期
        // ══════════════════════════════════════════════════════════

        /// <summary>
        /// 剩余持续回合数（-1 = 永久）。
        /// 每回合结束由 BuffManager.TickDecay 自动 -1，归零后调用 RemoveBuff。
        /// </summary>
        public int RemainingRounds { get; set; } = -1;

        /// <summary>
        /// 由此 Buff 注册的触发器 ID 列表。
        /// Buff 移除时，TriggerManager 按此列表注销对应触发器，保证生命周期同步。
        /// </summary>
        public List<string> RegisteredTriggerIds { get; set; } = new List<string>();

        /// <summary>
        /// 由此 Buff 注册的 ValueModifier ID 列表。
        /// Buff 移除时，ValueModifierManager 按此列表注销对应修正器。
        /// </summary>
        public List<string> RegisteredModifierIds { get; set; } = new List<string>();
    }
}
