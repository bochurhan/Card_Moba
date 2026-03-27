#pragma warning disable CS8632

using System.Collections.Generic;
using CardMoba.BattleCore.Context;
using CardMoba.BattleCore.Foundation;

namespace CardMoba.BattleCore.Managers
{
    /// <summary>
    /// Runtime card lifecycle and zone movement contract.
    /// </summary>
    public interface ICardManager
    {
        void InitBattleDeck(BattleContext ctx, string playerId, List<(string configId, int count)> deckConfig);

        List<BattleCard> DrawCards(BattleContext ctx, string playerId, int count);

        bool CommitPlanCard(BattleContext ctx, string cardInstanceId);

        void MoveCard(BattleContext ctx, BattleCard card, CardZone targetZone);

        bool MoveCardToTopOfDeck(BattleContext ctx, BattleCard card);

        void ScanStatCards(BattleContext ctx);

        void OnRoundStart(BattleContext ctx, int round);

        void OnRoundEnd(BattleContext ctx, int round);

        void DestroyTempCards(BattleContext ctx);

        BattleCard GenerateCard(BattleContext ctx, string ownerId, string configId, CardZone targetZone, bool tempCard = true);

        BattleCard? GetCard(BattleContext ctx, string instanceId);

        BattleCard? PrepareInstantCard(BattleContext ctx, string playerId, string cardInstanceId);
    }
}
