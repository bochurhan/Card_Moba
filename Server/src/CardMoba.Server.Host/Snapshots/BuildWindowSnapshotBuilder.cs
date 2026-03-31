using System;
using System.Collections.Generic;
using System.Linq;
using CardMoba.MatchFlow.Context;
using CardMoba.MatchFlow.Rules;
using CardMoba.Protocol.Messages.Common;
using CardMoba.Server.Host.Config;
using CardMoba.Server.Host.Services;

namespace CardMoba.Server.Host.Snapshots
{
    /// <summary>
    /// 按玩家视角裁剪构筑窗口快照。
    /// 当前只向本人暴露具体候选，其它玩家仅暴露锁定和基础状态。
    /// </summary>
    public sealed class BuildWindowSnapshotBuilder
    {
        private readonly ServerCardCatalog _cardCatalog;

        public BuildWindowSnapshotBuilder(ServerCardCatalog cardCatalog)
        {
            _cardCatalog = cardCatalog;
        }

        public BuildWindowDto Build(MatchContext context, PendingLocalMatchRoom room, string viewerPlayerId)
        {
            if (context.ActiveBuildWindow == null)
                throw new InvalidOperationException("当前没有可构建的构筑窗口。");

            var dto = new BuildWindowDto
            {
                MatchId = context.MatchId,
                BattleIndex = context.CurrentStepIndex + 1,
                TotalBattleCount = context.Ruleset.Steps.Count,
                BattleTitle = $"第 {context.CurrentStepIndex + 1}/{context.Ruleset.Steps.Count} 场结束",
                DeadlineUnixMs = context.ActiveBuildWindow.DeadlineUnixMs,
                LocalPlayerId = viewerPlayerId,
            };

            foreach (var playerWindow in context.ActiveBuildWindow.PlayerWindows.Values.OrderBy(item => item.PlayerId, StringComparer.Ordinal))
            {
                var participant = room.Participants.FirstOrDefault(item => string.Equals(item.PlayerId, playerWindow.PlayerId, StringComparison.Ordinal));
                var playerDto = BuildPlayerWindow(context, playerWindow, participant?.DisplayName ?? playerWindow.PlayerId, viewerPlayerId);
                dto.Players.Add(playerDto);
                if (string.Equals(playerWindow.PlayerId, viewerPlayerId, StringComparison.Ordinal))
                    dto.LocalPlayer = playerDto;
            }

            return dto;
        }

        private BuildPlayerWindowDto BuildPlayerWindow(MatchContext context, PlayerBuildWindowState playerWindow, string displayName, string viewerPlayerId)
        {
            bool isLocal = string.Equals(playerWindow.PlayerId, viewerPlayerId, StringComparison.Ordinal);
            var dto = new BuildPlayerWindowDto
            {
                PlayerId = playerWindow.PlayerId,
                DisplayName = displayName,
                IsLocalPlayer = isLocal,
                IsLocked = playerWindow.IsLocked,
                CanLock = isLocal && playerWindow.NextOpportunityIndex >= playerWindow.OpportunityCount,
                PreviewHp = playerWindow.PreviewHp,
                MaxHp = playerWindow.MaxHp,
                OpportunityCount = playerWindow.OpportunityCount,
                NextOpportunityIndex = playerWindow.NextOpportunityIndex,
                ResolvedOpportunityCount = playerWindow.Opportunities.Count(item => item.IsResolved),
                RestrictionKind = playerWindow.RestrictionMode == BuildWindowRestrictionMode.ForcedRecovery
                    ? BuildWindowRestrictionKind.ForcedRecovery
                    : BuildWindowRestrictionKind.None,
            };

            foreach (var opportunity in playerWindow.Opportunities.Where(item => item.IsResolved))
                dto.ResolvedChoiceSummaries.Add(SummarizeResolvedChoice(opportunity));

            if (isLocal && playerWindow.Opportunities.Count > playerWindow.NextOpportunityIndex)
                dto.CurrentOpportunity = BuildOpportunityDto(playerWindow.Opportunities[playerWindow.NextOpportunityIndex]);

            return dto;
        }

        private BuildOpportunityDto BuildOpportunityDto(BuildOpportunityState opportunity)
        {
            var dto = new BuildOpportunityDto
            {
                OpportunityIndex = opportunity.OpportunityIndex,
                HealAmount = opportunity.Offers.HealAmount,
                DraftGroupsRevealed = opportunity.Offers.DraftGroupsRevealed,
                CommittedActionType = ToProtocolBuildActionType(opportunity.CommittedActionType),
            };

            foreach (var action in opportunity.AvailableActions)
                dto.AvailableActions.Add(ToProtocolBuildActionType(action));

            foreach (var card in opportunity.Offers.UpgradableCards)
                dto.UpgradableCards.Add(BuildCardCandidate(card));

            foreach (var card in opportunity.Offers.RemovableCards)
                dto.RemovableCards.Add(BuildCardCandidate(card));

            foreach (var group in opportunity.Offers.DraftGroups)
            {
                var groupDto = new BuildDraftGroupDto { GroupIndex = group.GroupIndex };
                foreach (var offer in group.Offers)
                {
                    var config = _cardCatalog.GetCard(offer.EffectiveConfigId) ?? _cardCatalog.GetCard(offer.BaseConfigId);
                    groupDto.Offers.Add(new BuildDraftOfferDto
                    {
                        OfferId = offer.OfferId,
                        PersistentCardId = offer.PersistentCardId,
                        BaseConfigId = offer.BaseConfigId,
                        EffectiveConfigId = offer.EffectiveConfigId,
                        DisplayName = config?.CardName ?? offer.EffectiveConfigId,
                        Description = config?.Description ?? string.Empty,
                        EnergyCost = config?.EnergyCost ?? 0,
                        Rarity = ToProtocolBuildCardRarity(offer.Rarity),
                        UpgradeLevel = offer.UpgradeLevel,
                        IsUpgraded = offer.IsUpgraded,
                    });
                }

                dto.DraftGroups.Add(groupDto);
            }

            return dto;
        }

        private BuildCardCandidateDto BuildCardCandidate(BuildCardCandidate card)
        {
            var config = _cardCatalog.GetCard(card.EffectiveConfigId) ?? _cardCatalog.GetCard(card.BaseConfigId);
            return new BuildCardCandidateDto
            {
                PersistentCardId = card.PersistentCardId,
                BaseConfigId = card.BaseConfigId,
                EffectiveConfigId = card.EffectiveConfigId,
                DisplayName = config?.CardName ?? card.EffectiveConfigId,
                Description = config?.Description ?? string.Empty,
                EnergyCost = config?.EnergyCost ?? 0,
                UpgradeLevel = card.UpgradeLevel,
            };
        }

        private static string SummarizeResolvedChoice(BuildOpportunityState opportunity)
        {
            if (opportunity.Choice == null)
                return opportunity.CommittedActionType.ToString();

            return opportunity.Choice.ActionType switch
            {
                BuildActionType.Heal => $"休息 +{opportunity.Offers.HealAmount}",
                BuildActionType.UpgradeCard => "升级 1 张牌",
                BuildActionType.RemoveCard => "删除 1 张牌",
                BuildActionType.AddCard => "拿牌",
                _ => opportunity.Choice.ActionType.ToString(),
            };
        }

        private static ProtocolBuildActionType ToProtocolBuildActionType(BuildActionType actionType)
        {
            return actionType switch
            {
                BuildActionType.Heal => ProtocolBuildActionType.Heal,
                BuildActionType.AddCard => ProtocolBuildActionType.AddCard,
                BuildActionType.RemoveCard => ProtocolBuildActionType.RemoveCard,
                BuildActionType.UpgradeCard => ProtocolBuildActionType.UpgradeCard,
                BuildActionType.CustomPlaceholder => ProtocolBuildActionType.CustomPlaceholder,
                _ => ProtocolBuildActionType.None,
            };
        }

        private static ProtocolBuildCardRarity ToProtocolBuildCardRarity(MatchFlow.Definitions.BuildCardRarity rarity)
        {
            return rarity switch
            {
                MatchFlow.Definitions.BuildCardRarity.Uncommon => ProtocolBuildCardRarity.Uncommon,
                MatchFlow.Definitions.BuildCardRarity.Rare => ProtocolBuildCardRarity.Rare,
                MatchFlow.Definitions.BuildCardRarity.Legendary => ProtocolBuildCardRarity.Legendary,
                _ => ProtocolBuildCardRarity.Common,
            };
        }
    }
}
