#pragma warning disable CS8632

namespace CardMoba.BattleCore.Foundation
{
    /// <summary>
    /// 战斗中卡牌所在的区域。
    /// </summary>
    public enum CardZone
    {
        /// <summary>牌组（未抽取）</summary>
        Deck = 0,

        /// <summary>手牌（可操作）</summary>
        Hand = 1,

        /// <summary>保留枚举：旧版真实定策区。当前定策牌改为快照记录，不再有真实实例驻留。</summary>
        StrategyZone = 2,

        /// <summary>弃牌堆（结算后归位，可被循环牌拉回）</summary>
        Discard = 3,

        /// <summary>消耗区（消耗牌使用后永久记录于此）</summary>
        Consume = 4,
    }
}
