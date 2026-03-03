using System.Collections.Generic;
using CardMoba.ConfigModels.Card;
using CardMoba.Protocol.Enums;

namespace CardMoba.BattleCore.Context
{
    /// <summary>
    /// 已打出的卡牌实例 —— 记录一次出牌操作的完整运行时信息。
    /// 
    /// 与 CardConfig（静态配置）不同，PlayedCard 包含：
    /// - 打出时的上下文信息（谁打的、在哪条路、目标是谁）
    /// - 目标解析结果（从 RawTargetGroup 解析为 ResolvedTargets）
    /// - 运行时唯一ID（用于反制牌定位）
    /// </summary>
    public class PlayedCard
    {
        /// <summary>
        /// 运行时唯一标识（每张打出的牌都有唯一ID）。
        /// 格式："{回合数}_{玩家ID}_{序号}"，如 "3_1_2" 表示第3回合玩家1打出的第2张牌。
        /// 用于反制牌精确定位目标卡牌。
        /// </summary>
        public string RuntimeId { get; set; } = string.Empty;

        /// <summary>操作者的玩家ID</summary>
        public string SourcePlayerId { get; set; } = string.Empty;

        /// <summary>打出时所在的分路索引（0=上路, 1=中路, 2=下路）</summary>
        public int LaneIndex { get; set; }

        /// <summary>卡牌静态配置（引用 ConfigModels 中的定义）</summary>
        public CardConfig Config { get; set; } = null!;

        // ── 目标信息 ──

        /// <summary>
        /// 原始目标组 —— 打牌时记录的目标。
        /// 
        /// 不同牌类型的填充规则：
        /// - 单体牌：["目标玩家ID"]
        /// - 多目标牌：["敌方ID", "友方ID"]（按 TargetSelections 顺序）
        /// - AOE牌：[] (空，由 TargetResolver 解析)
        /// - 反制牌：[] (空，由 CounterHandler 查找)
        /// </summary>
        public List<string> RawTargetGroup { get; set; } = new();

        /// <summary>
        /// 解析后的实际目标列表 —— 结算时由 TargetResolver 填充。
        /// 
        /// 所有类型的牌在结算前都会被解析为玩家ID列表：
        /// - 单体牌：与 RawTargetGroup 相同
        /// - AOE牌：根据 EffectRange 解析出的所有符合条件的玩家
        /// - 反制牌：[] (不走 TargetResolver，由 Handler 内部处理)
        /// </summary>
        public List<string> ResolvedTargets { get; set; } = new();

        // ── 结算状态 ──

        /// <summary>
        /// 卡牌是否已被反制作废。
        /// 被反制的牌在后续堆叠层不会生效。
        /// </summary>
        public bool IsCountered { get; set; }

        /// <summary>
        /// 卡牌是否已完成结算。
        /// 用于避免重复结算。
        /// </summary>
        public bool IsResolved { get; set; }

        /// <summary>
        /// 效果间传值上下文 —— 同一张牌内多个效果之间共享数值的临时存储。
        ///
        /// 生命周期：仅在本张牌的结算过程中有效，结算完毕后不保留。
        ///
        /// 约定的 Key（Handler 写入，后续效果读取）：
        ///   "LastDamageDealt"  —— 本次 DamageHandler 实际造成的 HP 伤害值
        ///   "LastHealAmount"   —— 本次 HealHandler 实际回复的生命值
        ///   "LastShieldAmount" —— 本次 ShieldHandler 实际施加的护盾值
        /// </summary>
        public Dictionary<string, int> EffectContext { get; set; } = new();

        // ── 便捷属性 ──

        /// <summary>卡牌轨道类型（瞬策/定策）</summary>
        public CardTrackType TrackType => Config.TrackType;

        /// <summary>卡牌所属结算层</summary>
        public SettlementLayer Layer => Config.Layer;

        /// <summary>卡牌效果生效范围</summary>
        public EffectRange EffectRange => Config.EffectRange;

        /// <summary>卡牌是否为反制牌</summary>
        public bool IsCounterCard => Config.Layer == SettlementLayer.Counter;

        /// <summary>卡牌是否为AOE牌（需要自动解析目标）</summary>
        public bool IsAOE => Config.EffectRange >= EffectRange.CurrentLaneEnemies;

        /// <summary>卡牌是否需要玩家选择目标</summary>
        public bool RequiresTargetSelection => !IsAOE && !IsCounterCard && Config.EffectRange != EffectRange.Self;
    }
}
