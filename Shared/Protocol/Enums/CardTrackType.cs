namespace CardMoba.Protocol.Enums
{
    /// <summary>
    /// 卡牌双轨类型 —— 核心机制：瞬策牌即时结算，定策牌回合末统一结算。
    /// </summary>
    public enum CardTrackType
    {
        /// <summary>瞬策牌：操作期内打出后立即结算</summary>
        瞬策牌 = 1,

        /// <summary>定策牌：操作期内暗置提交，回合末统一结算</summary>
        定策牌 = 2,
    }
}
