
#pragma warning disable CS8632

using System.Collections.Generic;

namespace CardMoba.BattleCore.Foundation
{
    /// <summary>
    /// 战斗时卡牌实例（BattleCard）—— 区别于静态 CardConfig，每张战斗中的牌都有独立实例。
    ///
    /// 实例化时机：
    ///   战斗开始时，CardManager.InitBattleDeck() 将卡组配置展开为 BattleCard 实例；
    ///   临时牌（如复制牌）由 CardGenerateHandler 动态创建；
    ///   状态牌（如灼烧）由专用效果生成，IsStatCard=true。
    /// </summary>
    public class BattleCard
    {
        // ══════════════════════════════════════════════════════════
        // 身份
        // ══════════════════════════════════════════════════════════

        /// <summary>
        /// 战斗内唯一实例 ID（格式 "bc_001"，由 CardManager.InitBattleDeck 生成）。
        /// 同一张 CardConfig 的多副本各自有独立 InstanceId。
        /// </summary>
        public string InstanceId { get; set; } = string.Empty;

        /// <summary>关联的静态卡牌配置 ID（如 "strike"）</summary>
        public string ConfigId { get; set; } = string.Empty;

        /// <summary>持有者玩家 ID</summary>
        public string OwnerId { get; set; } = string.Empty;

        // ══════════════════════════════════════════════════════════
        // 区域与状态
        // ══════════════════════════════════════════════════════════

        /// <summary>当前所在区域（由 CardManager.MoveCard 统一管理）</summary>
        public CardZone Zone { get; set; } = CardZone.Deck;

        /// <summary>
        /// 临时牌标记（true = 结算后由 CardManager.DestroyTempCards 直接销毁，不进弃牌堆）。
        /// 临时生成的卡牌（如复制效果）应设为 true。
        /// </summary>
        public bool TempCard { get; set; }

        /// <summary>
        /// 状态牌标记（true = 不可主动打出，由 ScanStatCards 在回合结束时触发效果）。
        /// 示例：灼烧状态牌、中毒状态牌。
        /// </summary>
        /// <remarks>
        /// 状态牌仍然是常规卡牌实例，可在牌堆、手牌、弃牌堆之间流转。
        /// 当前主流程不依赖额外专用区；若需要“持有时触发”，由 CardManager.ScanStatCards 扫描手牌中的状态牌。
        /// </remarks>
        public bool IsStatCard { get; set; }

        /// <summary>
        /// 消耗牌标记（true = 使用后进入 Consume 区域而非弃牌堆）。
        /// 消耗牌不可被循环类效果拉回。
        /// </summary>
        public bool IsExhaust { get; set; }

        // ══════════════════════════════════════════════════════════
        // 运行时数据
        // ══════════════════════════════════════════════════════════

        /// <summary>
        /// 运行时附加数据（Handler 或触发器可写入自定义状态）。
        /// 示例：{ "playCount": 2 }（记录本局已打出次数）
        /// </summary>
        public Dictionary<string, object> ExtraData { get; set; } = new Dictionary<string, object>();
    }
}
