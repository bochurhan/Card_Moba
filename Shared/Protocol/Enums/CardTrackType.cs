namespace CardMoba.Protocol.Enums
{
    /// <summary>
    /// 卡牌双轨类型 —— 核心机制：瞬策牌即时结算，定策牌回合末统一结算。
    /// Card track type — Core mechanic: Instant cards resolve immediately, Plan cards resolve at end of round.
    /// </summary>
    public enum CardTrackType
    {
        /// <summary>瞬策牌：操作期内打出后立即结算 (Instant card: resolves immediately when played)</summary>
        Instant = 1,

        /// <summary>定策牌：操作期内暗置提交，回合末统一结算 (Plan card: submitted face-down, resolves at end of round)</summary>
        Plan = 2,
    }
}