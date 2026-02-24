using System;

namespace CardMoba.Protocol.Enums
{
    /// <summary>
    /// 卡牌标签 —— 标记卡牌的特殊属性和规则。
    /// 使用 Flags 支持单张卡牌拥有多个标签。
    /// </summary>
    [Flags]
    public enum CardTag
    {
        /// <summary>无标签</summary>
        无 = 0,

        /// <summary>跨路生效：可以作用于非同路的玩家（中枢塔场景禁用）</summary>
        跨路生效 = 1 << 0,

        /// <summary>卡组循环：打出后不进弃牌堆，直接返回牌库</summary>
        卡组循环 = 1 << 1,

        /// <summary>消耗：打出后从游戏中移除，不进弃牌堆</summary>
        消耗 = 1 << 2,

        /// <summary>固有：战斗开始时必定在起手</summary>
        固有 = 1 << 3,

        /// <summary>保留：回合结束不会被弃置</summary>
        保留 = 1 << 4,
    }
}
