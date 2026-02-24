using CardMoba.ConfigModels.Card;
using CardMoba.Protocol.Enums;

namespace CardMoba.BattleCore.Context
{
    /// <summary>
    /// 玩家操作指令 —— 记录一次出牌操作的完整信息。
    /// 操作期内玩家每打出一张牌，就会生成一个 CardAction。
    /// </summary>
    public class CardAction
    {
        /// <summary>操作者的玩家ID</summary>
        public int SourcePlayerId { get; set; }

        /// <summary>目标玩家ID（如果需要指定目标）</summary>
        public int TargetPlayerId { get; set; }

        /// <summary>打出的卡牌配置</summary>
        public CardConfig Card { get; set; } = null!;

        /// <summary>是瞬策牌（立即结算）还是定策牌（回合末结算）</summary>
        public CardTrackType TrackType => Card.TrackType;
    }
}
