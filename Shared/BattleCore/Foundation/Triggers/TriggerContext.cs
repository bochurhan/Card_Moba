#pragma warning disable CS8632

using System.Collections.Generic;

namespace CardMoba.BattleCore.Foundation
{
    /// <summary>
    /// 触发器上下文 —— 触发时携带的附带信息。
    /// 定义在 Foundation 层，供 Core / Managers / Context 共同访问，避免循环依赖。
    /// </summary>
    public class TriggerContext
    {
        /// <summary>触发来源实体 ID（玩家英雄或单位）</summary>
        public string SourceEntityId { get; set; } = string.Empty;

        /// <summary>触发目标实体 ID</summary>
        public string TargetEntityId { get; set; } = string.Empty;

        /// <summary>触发携带的数值（如伤害值、治疗量）</summary>
        public int Value { get; set; }

        /// <summary>当前回合号（OnRoundStart / OnRoundEnd 时使用）</summary>
        public int Round { get; set; }

        /// <summary>扩展数据（任意键值对，供具体触发时机传递额外信息）</summary>
        public Dictionary<string, object> Extra { get; set; } = new Dictionary<string, object>();
    }
}
