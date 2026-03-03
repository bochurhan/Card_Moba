
#pragma warning disable CS8632

using CardMoba.ConfigModels.Card;

namespace CardMoba.BattleCore.Context
{
    /// <summary>
    /// 卡牌运行时实例 —— 连接配置层与结算层的桥梁。
    ///
    /// 设计原则：
    ///   - <see cref="Config"/> 是只读共享的静态配置（CardConfig），多张相同卡牌共享同一个 Config 引用。
    ///   - <see cref="InstanceId"/> 是该张牌在整场战斗中的唯一身份，由 BattleContext 在初始化牌库时生成。
    ///   - 手牌、牌库、弃牌堆中流转的都是 CardInstance，不是 CardConfig。
    ///   - 打出时（PlayCard/CommitPlanCard），从 CardInstance 构建 <see cref="PlayedCard"/>，
    ///     PlayedCard.RuntimeId 可直接复用 InstanceId，也可重新生成。
    /// </summary>
    public class CardInstance
    {
        // ══════════════════════════════════════════════════════════════
        // 核心字段
        // ══════════════════════════════════════════════════════════════

        /// <summary>
        /// 实例唯一 ID —— 在整场战斗中全局唯一，用于区分两张相同配置的卡牌。
        /// 格式：{playerId}_card_{counter}，例如 "p1_card_0003"
        /// </summary>
        public string InstanceId { get; set; } = string.Empty;

        /// <summary>
        /// 所属玩家 ID —— 记录该卡归属于哪位玩家（便于弃牌归还时校验）。
        /// </summary>
        public string OwnerPlayerId { get; set; } = string.Empty;

        /// <summary>
        /// 静态配置引用 —— 只读，全局共享。
        /// 所有运行时属性均从 Config 读取；禁止在运行时修改 Config 上的字段。
        /// </summary>
        public CardConfig Config { get; set; } = null!;

        // ══════════════════════════════════════════════════════════════
        // 快捷属性（透传 Config，方便调用方少写 .Config.xxx）
        // ══════════════════════════════════════════════════════════════

        /// <summary>卡牌配置 ID（来自 Config）</summary>
        public int CardId => Config.CardId;

        /// <summary>卡牌显示名称（来自 Config）</summary>
        public string CardName => Config.CardName;

        /// <summary>能量消耗（来自 Config）</summary>
        public int EnergyCost => Config.EnergyCost;

        /// <summary>卡牌轨道类型（瞬策/定策，来自 Config）</summary>
        public CardMoba.Protocol.Enums.CardTrackType TrackType => Config.TrackType;

        /// <summary>卡牌描述文本（来自 Config）</summary>
        public string Description => Config.Description;

        // ══════════════════════════════════════════════════════════════
        // 构造
        // ══════════════════════════════════════════════════════════════

        /// <summary>
        /// 创建一个卡牌实例。通常由 <see cref="BattleContext.CreateCardInstance"/> 统一调用。
        /// </summary>
        /// <param name="instanceId">战斗内唯一实例 ID</param>
        /// <param name="ownerPlayerId">所属玩家 ID</param>
        /// <param name="config">静态配置（只读共享）</param>
        public CardInstance(string instanceId, string ownerPlayerId, CardConfig config)
        {
            InstanceId    = instanceId;
            OwnerPlayerId = ownerPlayerId;
            Config        = config;
        }

        // ══════════════════════════════════════════════════════════════
        // 方法
        // ══════════════════════════════════════════════════════════════

        /// <summary>
        /// 将本实例转换为 PlayedCard（出牌时调用）。
        /// PlayedCard.RuntimeId 直接使用 InstanceId，保证可追溯。
        /// </summary>
        public PlayedCard ToPlayedCard()
        {
            return new PlayedCard
            {
                RuntimeId      = InstanceId,
                SourcePlayerId = OwnerPlayerId,
                Config         = Config
            };
        }

        /// <inheritdoc/>
        public override string ToString()
            => $"[CardInstance {InstanceId}] {CardName}(ID={CardId})";
    }
}
