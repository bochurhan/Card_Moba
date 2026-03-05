
#pragma warning disable CS8632

namespace CardMoba.BattleCore.Foundation
{
    /// <summary>
    /// 战斗中卡牌所在的区域。
    /// </summary>
    public enum CardZone
    {
        /// <summary>卡组（未抽取）</summary>
        Deck = 0,

        /// <summary>手牌（可操作）</summary>
        Hand = 1,

        /// <summary>定策区（本回合已提交的定策牌，等待结算）</summary>
        StrategyZone = 2,

        /// <summary>弃牌堆（结算后归位，可被循环牌拉回）</summary>
        Discard = 3,

        /// <summary>消耗区（消耗牌使用后永久记录于此）</summary>
        Consume = 4,

        /// <summary>状态牌区（持有中的状态牌，绑定持续触发效果）</summary>
        StatZone = 5,
    }
}
