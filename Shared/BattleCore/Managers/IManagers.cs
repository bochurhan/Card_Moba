#pragma warning disable CS8632

using System.Collections.Generic;
using CardMoba.BattleCore.Context;
using CardMoba.BattleCore.Foundation;
using CardMoba.Protocol.Enums;

namespace CardMoba.BattleCore.Managers
{
    /// <summary>
    /// 触发器管理器接口。
    /// 负责触发器注册、按时机触发，以及生命周期衰减。
    /// </summary>
    public interface ITriggerManager
    {
        string Register(TriggerUnit trigger);

        bool Unregister(string triggerId);

        int UnregisterBySourceId(string sourceId);

        void Fire(BattleContext ctx, TriggerTiming timing, TriggerContext triggerCtx);

        void TickDecay(BattleContext ctx);
    }

    /// <summary>
    /// 卡牌区位与实例生命周期管理接口。
    /// 不负责效果结算，只负责 BattleCard 的创建、移动和销毁。
    /// </summary>
    public interface ICardManager
    {
        void InitBattleDeck(BattleContext ctx, string playerId, List<(string configId, int count)> deckConfig);

        List<BattleCard> DrawCards(BattleContext ctx, string playerId, int count);

        /// <summary>
        /// 提交定策牌。提交后生成定策快照，真实实例立即进入结算后去向。
        /// </summary>
        bool CommitPlanCard(BattleContext ctx, string cardInstanceId);

        void MoveCard(BattleContext ctx, BattleCard card, CardZone targetZone);

        bool MoveCardToTopOfDeck(BattleContext ctx, BattleCard card);

        void ScanStatCards(BattleContext ctx);

        void OnRoundStart(BattleContext ctx, int round);

        void OnRoundEnd(BattleContext ctx, int round);

        void DestroyTempCards(BattleContext ctx);

        BattleCard GenerateCard(BattleContext ctx, string ownerId, string configId, CardZone targetZone, bool tempCard = true);

        BattleCard? GetCard(BattleContext ctx, string instanceId);

        /// <summary>
        /// 通过最终校验后，将瞬策牌从手牌移到结算后去向。
        /// </summary>
        BattleCard? PrepareInstantCard(BattleContext ctx, string playerId, string cardInstanceId);
    }

    /// <summary>
    /// Buff 管理器接口。
    /// BuffManager 是当前运行时 Buff 的唯一真源。
    /// </summary>
    public interface IBuffManager
    {
        BuffUnit AddBuff(BattleContext ctx, string targetEntityId, string buffConfigId, string sourcePlayerId, int value = 0, int duration = -1);

        bool RemoveBuff(BattleContext ctx, string targetEntityId, string buffRuntimeId);

        int RemoveBuffsByConfig(BattleContext ctx, string targetEntityId, string buffConfigId);

        bool HasBuff(BattleContext ctx, string entityId, string buffConfigId);

        bool HasBuffType(BattleContext ctx, string entityId, BuffType buffType);

        IReadOnlyList<BuffUnit> GetBuffs(string entityId);

        void TickDecay(BattleContext ctx);

        void OnRoundEnd(BattleContext ctx, int round);
    }
}
