using CardMoba.ConfigModels.Card;
using CardMoba.Protocol.Enums;

namespace CardMoba.BattleCore.Context
{
    /// <summary>
    /// [已废弃] 旧版玩家操作指令 —— 请使用 PlayedCard 代替。
    /// 
    /// 保留此类仅为兼容旧代码，新代码应使用 PlayedCard：
    /// - PlayedCard 支持 string 类型的 PlayerId（统一格式）
    /// - PlayedCard 支持目标解析、运行时 ID 等完整功能
    /// </summary>
    [System.Obsolete("请使用 PlayedCard 代替 CardAction")]
    public class CardAction
    {
        /// <summary>操作者的玩家ID（字符串格式）</summary>
        public string SourcePlayerId { get; set; } = string.Empty;

        /// <summary>目标玩家ID（字符串格式）</summary>
        public string TargetPlayerId { get; set; } = string.Empty;

        /// <summary>打出的卡牌配置</summary>
        public CardConfig Card { get; set; } = null!;

        /// <summary>是瞬策牌（立即结算）还是定策牌（回合末结算）</summary>
        public CardTrackType TrackType => Card.TrackType;

        /// <summary>
        /// 转换为新版 PlayedCard。
        /// </summary>
        public PlayedCard ToPlayedCard()
        {
            return new PlayedCard
            {
                SourcePlayerId = this.SourcePlayerId,
                Config = this.Card,
                RawTargetGroup = string.IsNullOrEmpty(TargetPlayerId) 
                    ? new System.Collections.Generic.List<string>() 
                    : new System.Collections.Generic.List<string> { TargetPlayerId }
            };
        }
    }
}
