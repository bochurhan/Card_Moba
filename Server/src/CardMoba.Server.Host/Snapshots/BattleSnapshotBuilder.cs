using System;
using System.Linq;
using CardMoba.BattleCore.Context;
using CardMoba.BattleCore.Core;
using CardMoba.BattleCore.Foundation;
using CardMoba.MatchFlow.Context;
using CardMoba.Protocol.Enums;
using CardMoba.Protocol.Messages.Common;
using CardMoba.Server.Host.Config;

namespace CardMoba.Server.Host.Snapshots
{
    /// <summary>
    /// 按玩家视角裁剪战斗快照。
    /// 当前 localhost 1v1 只返回本人与对手两份状态，
    /// 其中本人可见手牌详情与弃牌详情，对手只暴露计数信息。
    /// </summary>
    public sealed class BattleSnapshotBuilder
    {
        private readonly ServerCardCatalog _cardCatalog;

        public BattleSnapshotBuilder(ServerCardCatalog cardCatalog)
        {
            _cardCatalog = cardCatalog;
        }

        public BattleSnapshotDto Build(
            MatchContext matchContext,
            string viewerPlayerId,
            ICollection<string>? lockedPlayerIds = null,
            IDictionary<string, long>? lockCooldownUntilUnixMsByPlayer = null)
        {
            if (matchContext.ActiveBattleContext == null || matchContext.ActiveRoundManager == null)
                throw new InvalidOperationException("当前没有可构建快照的 active battle。");

            var battleContext = matchContext.ActiveBattleContext;
            var roundManager = matchContext.ActiveRoundManager;
            var localPlayer = battleContext.GetPlayer(viewerPlayerId)
                ?? throw new InvalidOperationException($"战斗中不存在玩家：{viewerPlayerId}");
            var opponentPlayer = battleContext.AllPlayers.Values
                .First(player => !string.Equals(player.PlayerId, viewerPlayerId, StringComparison.Ordinal));

            return new BattleSnapshotDto
            {
                MatchId = matchContext.MatchId,
                BattleId = battleContext.BattleId,
                BattleIndex = matchContext.CurrentStepIndex + 1,
                TotalBattleCount = matchContext.Ruleset.Steps.Count,
                CurrentRound = roundManager.CurrentRound,
                IsBattleOver = roundManager.IsBattleOver,
                PhaseKind = MapPhaseKind(matchContext),
                LocalPlayer = BuildPlayerState(
                    battleContext,
                    roundManager,
                    localPlayer,
                    includeCardDetails: true,
                    lockedPlayerIds,
                    lockCooldownUntilUnixMsByPlayer),
                OpponentPlayer = BuildPlayerState(
                    battleContext,
                    roundManager,
                    opponentPlayer,
                    includeCardDetails: false,
                    lockedPlayerIds,
                    lockCooldownUntilUnixMsByPlayer),
            };
        }

        private BattlePlayerStateDto BuildPlayerState(
            BattleContext battleContext,
            RoundManager roundManager,
            PlayerData player,
            bool includeCardDetails,
            ICollection<string>? lockedPlayerIds,
            IDictionary<string, long>? lockCooldownUntilUnixMsByPlayer)
        {
            var dto = new BattlePlayerStateDto
            {
                PlayerId = player.PlayerId,
                TeamId = player.TeamId,
                IsLocalPlayer = includeCardDetails,
                IsAlive = player.HeroEntity.IsAlive,
                Hp = player.HeroEntity.Hp,
                MaxHp = player.HeroEntity.MaxHp,
                Shield = player.HeroEntity.Shield,
                Armor = player.HeroEntity.Armor,
                Energy = player.Energy,
                MaxEnergy = player.MaxEnergy,
                DeckCount = player.GetCardsInZone(CardZone.Deck).Count,
                DiscardCount = player.GetCardsInZone(CardZone.Discard).Count,
                HandCount = player.GetCardsInZone(CardZone.Hand).Count,
                PendingPlanCount = battleContext.PendingPlanSnapshots.Count(item => string.Equals(item.PlayerId, player.PlayerId, StringComparison.Ordinal)),
                IsTurnLocked = lockedPlayerIds != null && lockedPlayerIds.Contains(player.PlayerId),
                LockCooldownUntilUnixMs = lockCooldownUntilUnixMsByPlayer != null
                    && lockCooldownUntilUnixMsByPlayer.TryGetValue(player.PlayerId, out var cooldownUntil)
                    ? cooldownUntil
                    : 0,
            };

            foreach (var buff in battleContext.BuffManager.GetBuffs(player.HeroEntity.EntityId))
            {
                dto.Buffs.Add(new BattleBuffDto
                {
                    ConfigId = buff.ConfigId,
                    DisplayName = string.IsNullOrWhiteSpace(buff.DisplayName) ? buff.ConfigId : buff.DisplayName,
                    Value = buff.Value,
                    RemainingRounds = buff.RemainingRounds,
                });
            }

            if (!includeCardDetails)
                return dto;

            foreach (var card in player.GetCardsInZone(CardZone.Hand))
                dto.HandCards.Add(BuildCardDto(battleContext, roundManager, player.PlayerId, card, resolveDisplayedCost: true));

            foreach (var card in player.GetCardsInZone(CardZone.Discard))
                dto.DiscardCards.Add(BuildCardDto(battleContext, roundManager, player.PlayerId, card, resolveDisplayedCost: false));

            return dto;
        }

        private HandCardDto BuildCardDto(
            BattleContext battleContext,
            RoundManager roundManager,
            string playerId,
            BattleCard card,
            bool resolveDisplayedCost)
        {
            var config = _cardCatalog.GetCard(card.GetEffectiveConfigId()) ?? _cardCatalog.GetCard(card.ConfigId);
            int baseCost = config?.EnergyCost ?? 0;
            int displayedCost = resolveDisplayedCost
                ? roundManager.ResolvePlayCost(battleContext, playerId, card).FinalCost
                : baseCost;

            return new HandCardDto
            {
                InstanceId = card.InstanceId,
                BaseConfigId = card.ConfigId,
                EffectiveConfigId = card.GetEffectiveConfigId(),
                DisplayName = config?.CardName ?? card.GetEffectiveConfigId(),
                Description = config?.Description ?? string.Empty,
                EnergyCost = baseCost,
                DisplayedCost = displayedCost,
                TrackType = config?.TrackType ?? CardTrackType.Instant,
                RequiresDiscardSelection = RequiresDiscardSelection(config),
            };
        }

        private static bool RequiresDiscardSelection(CardMoba.ConfigModels.Card.CardConfig? config)
        {
            if (config?.Effects == null)
                return false;

            foreach (var effect in config.Effects)
            {
                if (effect.EffectType == EffectType.MoveSelectedCardToDeckTop)
                    return true;
            }

            return false;
        }

        private static ServerPhaseKind MapPhaseKind(MatchContext context)
        {
            if (context.CurrentPhase == CardMoba.MatchFlow.Rules.MatchPhase.BuildWindow)
                return ServerPhaseKind.BuildWindow;
            if (context.CurrentPhase == CardMoba.MatchFlow.Rules.MatchPhase.MatchEnded)
                return ServerPhaseKind.MatchEnded;

            return context.ActiveBattleContext?.CurrentPhase == BattleContext.BattlePhase.Settlement
                ? ServerPhaseKind.BattleSettlement
                : ServerPhaseKind.BattleOperation;
        }
    }
}
