
#pragma warning disable CS8632

using System.Collections.Generic;
using CardMoba.BattleCore.Foundation;
using CardMoba.BattleCore.Context;
using CardMoba.Protocol.Enums;

namespace CardMoba.BattleCore.Managers
{
    // ══════════════════════════════════════════════════════════════
    // ITriggerManager
    // ══════════════════════════════════════════════════════════════

    /// <summary>
    /// 触发器管理器接口 —— 内部逻辑总线。
    /// 负责触发器注册、按时机分组存储、事件响应时优先级排序并推入 PendingQueue。
    /// ⚠️ TriggerManager 不持有 SettlementEngine 引用，只向 PendingQueue 推送。
    /// </summary>
    public interface ITriggerManager
    {
        /// <summary>注册触发器（返回生成的 TriggerId）</summary>
        string Register(TriggerUnit trigger);

        /// <summary>按 TriggerId 注销触发器</summary>
        bool Unregister(string triggerId);

        /// <summary>注销指定来源 ID 关联的所有触发器（Buff 移除时调用）</summary>
        int UnregisterBySourceId(string sourceId);

        /// <summary>
        /// 触发指定时机的所有满足条件的触发器。
        /// 触发器效果不直接执行，而是推入 ctx.PendingQueue。
        /// </summary>
        void Fire(BattleContext ctx, TriggerTiming timing, TriggerContext triggerCtx);

        /// <summary>每回合结束衰减触发器剩余回合数，到期自动注销</summary>
        void TickDecay(BattleContext ctx);
    }

    // ══════════════════════════════════════════════════════════════
    // ICardManager
    // ══════════════════════════════════════════════════════════════

    /// <summary>
    /// 卡牌区域管理器接口 —— 管理所有 BattleCard 实例的生命周期和区域流转。
    /// 不含结算逻辑，区域移动触发的效果通过 EventBus 广播。
    /// </summary>
    public interface ICardManager
    {
        /// <summary>战斗开始时，将卡组配置展开为 BattleCard 实例</summary>
        void InitBattleDeck(BattleContext ctx, string playerId, List<(string configId, int count)> deckConfig);

        /// <summary>从卡组顶部抽取指定数量的牌到手牌</summary>
        List<BattleCard> DrawCards(BattleContext ctx, string playerId, int count);

        /// <summary>提交定策牌（Hand → StrategyZone）</summary>
        bool CommitPlanCard(BattleContext ctx, string cardInstanceId);

        /// <summary>将卡牌移动到指定区域</summary>
        void MoveCard(BattleContext ctx, BattleCard card, CardZone targetZone);

        /// <summary>
        /// 扫描所有玩家手牌中的状态牌，触发 OnStatCardHeld 效果，推入 PendingQueue
        /// </summary>
        /// <remarks>
        /// 状态牌是普通 BattleCard 实例上的特殊标记，默认按手牌持有语义扫描，而不是依赖独立的运行时区域。
        /// </remarks>
        void ScanStatCards(BattleContext ctx);

        /// <summary>回合开始时处理抽牌等逻辑</summary>
        void OnRoundStart(BattleContext ctx, int round);

        /// <summary>回合结束时弃手牌（Hand + StrategyZone → Discard）</summary>
        void OnRoundEnd(BattleContext ctx, int round);

        /// <summary>结算完成后，清理所有 TempCard=true 的牌</summary>
        void DestroyTempCards(BattleContext ctx);

        /// <summary>动态创建一张临时卡牌并放入指定区域</summary>
        BattleCard GenerateCard(BattleContext ctx, string ownerId, string configId, CardZone targetZone, bool tempCard = true);

        /// <summary>通过实例 ID 获取 BattleCard</summary>
        BattleCard? GetCard(BattleContext ctx, string instanceId);

        /// <summary>
        /// 在出牌前校验完成后，将瞬策牌从 Hand 移出并落到最终区域。
        /// 成功时返回该实例，失败时返回 null。
        /// </summary>
        BattleCard? PrepareInstantCard(BattleContext ctx, string playerId, string cardInstanceId);
    }

    // ══════════════════════════════════════════════════════════════
    // IBuffManager
    // ══════════════════════════════════════════════════════════════

    /// <summary>
    /// Buff 管理器接口（薄中介）—— 负责 BuffUnit 的 CRUD 及触发器/修正器的生命周期同步。
    /// 不含触发逻辑（吸血/荆棘等效果通过 TriggerUnit 描述，由 TriggerManager 响应）。
    /// </summary>
    public interface IBuffManager
    {
        /// <summary>
        /// 添加 Buff（自动创建 BuffUnit 实例、注册关联触发器和修正器）。
        /// </summary>
        BuffUnit AddBuff(BattleContext ctx, string targetEntityId, string buffConfigId,
            string sourcePlayerId, int value = 0, int duration = -1);

        /// <summary>按运行时 ID 移除 Buff（自动注销关联触发器和修正器）</summary>
        bool RemoveBuff(BattleContext ctx, string targetEntityId, string buffRuntimeId);

        /// <summary>移除指定实体上某类型的所有 Buff</summary>
        int RemoveBuffsByConfig(BattleContext ctx, string targetEntityId, string buffConfigId);

        /// <summary>查询指定实体是否拥有某类型 Buff</summary>
        bool HasBuff(BattleContext ctx, string entityId, string buffConfigId);

        /// <summary>查询指定实体是否拥有某种 BuffType，用于统一规则判定。</summary>
        bool HasBuffType(BattleContext ctx, string entityId, BuffType buffType);

        /// <summary>获取指定实体当前持有的 Buff 列表。BuffManager 是唯一真源。</summary>
        IReadOnlyList<BuffUnit> GetBuffs(string entityId);

        /// <summary>回合结束：衰减所有 Buff 的 RemainingRounds，到期调用 RemoveBuff</summary>
        void TickDecay(BattleContext ctx);

        /// <summary>回合结束统一入口（衰减 + DOT 触发）</summary>
        void OnRoundEnd(BattleContext ctx, int round);
    }

    // ══════════════════════════════════════════════════════════════
    // IValueModifierManager
    // ══════════════════════════════════════════════════════════════

    /// <summary>数值修正器类型</summary>
    public enum ModifierType
    {
        /// <summary>加法修正（finalValue = baseValue + delta）</summary>
        Add = 0,
        /// <summary>乘法修正（finalValue = baseValue × factor）</summary>
        Mul = 1,
        /// <summary>强制覆盖（极少使用）</summary>
        Set = 2,
    }

    public enum ModifierScope
    {
        OutgoingDamage = 0,
        IncomingDamage = 1,
    }

    /// <summary>数值修正器</summary>
    public class ValueModifier
    {
        /// <summary>修正器唯一 ID（格式 "mod_xxxx"）</summary>
        public string ModifierId { get; set; } = string.Empty;
        /// <summary>修正器类型</summary>
        public ModifierType Type { get; set; }
        /// <summary>修正值（Add 时为 delta，Mul 时为 factor，Set 时为 fixedValue）</summary>
        public int Value { get; set; }
        /// <summary>归属玩家 ID（决定此修正器影响哪个玩家的效果）</summary>
        public string OwnerPlayerId { get; set; } = string.Empty;
        /// <summary>作用的效果类型（如 EffectType.Damage）</summary>
        public EffectType TargetEffectType { get; set; }
        /// <summary>淇鐢熸晥鐨勬柟鍚戯紙鍑轰激鎴栧叆浼わ級</summary>
        public ModifierScope Scope { get; set; }
    }

    /// <summary>
    /// 数值修正器管理器接口 —— 管理运行时数值修正器，在结算引擎计算效果值时动态应用。
    /// 力量/虚弱/伤害减免等均以 ValueModifier 形式注册。
    /// 应用顺序固定：Add → Mul → Set。
    /// </summary>
    public interface IValueModifierManager
    {
        /// <summary>注册修正器（返回生成的 ModifierId）</summary>
        string AddModifier(ValueModifier modifier);

        /// <summary>注销修正器</summary>
        bool RemoveModifier(string modifierId);

        /// <summary>
        /// 计算最终数值（对 baseValue 依次应用 Add → Mul → Set）。
        /// </summary>
        /// <param name="effectType">效果类型（筛选适用的修正器）</param>
        /// <param name="ownerPlayerId">施法者玩家 ID（筛选归属玩家）</param>
        /// <param name="baseValue">基础数值</param>
        /// <returns>修正后的最终数值</returns>
        int Apply(EffectType effectType, string ownerPlayerId, ModifierScope scope, int baseValue);
    }
}
