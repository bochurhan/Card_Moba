using CardMoba.Client.GameLogic.Abstractions;
using CardMoba.Protocol.Messages.Common;
using CardMoba.Protocol.Messages.Messages;

namespace CardMoba.Client.GameLogic.Online
{
    /// <summary>
    /// 将联机协议 DTO 映射为客户端 UI 使用的 ViewModel。
    /// </summary>
    public static class OnlineMessageMapper
    {
        public static BattleSnapshotViewState ToBattleViewState(BattleSnapshotDto dto)
        {
            var viewState = new BattleSnapshotViewState
            {
                MatchId = dto.MatchId,
                BattleId = dto.BattleId,
                BattleIndex = dto.BattleIndex,
                TotalBattleCount = dto.TotalBattleCount,
                CurrentRound = dto.CurrentRound,
                IsBattleOver = dto.IsBattleOver,
                PhaseKind = ToBattlePhaseKind(dto.PhaseKind),
                LocalPlayer = ToBattlePlayerViewState(dto.LocalPlayer, isLocalPlayer: true),
                OpponentPlayer = ToBattlePlayerViewState(dto.OpponentPlayer, isLocalPlayer: false),
            };

            foreach (var card in dto.LocalPlayer.HandCards)
                viewState.LocalHandCards.Add(ToBattleCardViewState(card));

            foreach (var card in dto.LocalPlayer.DiscardCards)
                viewState.LocalDiscardCards.Add(ToBattleCardViewState(card));

            return viewState;
        }

        public static BuildWindowViewState ToBuildWindowViewState(BuildWindowDto dto)
        {
            var viewState = new BuildWindowViewState
            {
                BattleIndex = dto.BattleIndex,
                TotalBattleCount = dto.TotalBattleCount,
                BattleTitle = dto.BattleTitle,
                DisplayText = $"第 {dto.BattleIndex}/{dto.TotalBattleCount} 场结束 · 构筑阶段",
                DeadlineUnixMs = dto.DeadlineUnixMs,
                LocalPlayerId = dto.LocalPlayerId,
                LocalPlayer = ToPlayerBuildWindowViewState(dto.LocalPlayer),
            };

            foreach (var player in dto.Players)
                viewState.Players.Add(ToPlayerBuildWindowViewState(player));

            return viewState;
        }

        public static BattlePhaseViewState ToBattlePhaseViewState(PhaseChangedMessage message)
        {
            var phaseKind = ToBattlePhaseKind(message.PhaseKind);
            return new BattlePhaseViewState
            {
                PhaseKind = phaseKind,
                DisplayText = BuildPhaseDisplayText(phaseKind, message.BattleIndex, message.TotalBattleCount),
                TimerText = BuildTimerText(phaseKind),
                UseOperationTimer = false,
            };
        }

        private static BattlePlayerViewState ToBattlePlayerViewState(BattlePlayerStateDto dto, bool isLocalPlayer)
        {
            var viewState = new BattlePlayerViewState
            {
                PlayerId = dto.PlayerId,
                TeamId = dto.TeamId,
                DisplayName = isLocalPlayer ? "你" : "对手",
                IsAlive = dto.IsAlive,
                Hp = dto.Hp,
                MaxHp = dto.MaxHp,
                Shield = dto.Shield,
                Armor = dto.Armor,
                Energy = dto.Energy,
                MaxEnergy = dto.MaxEnergy,
                HandCount = dto.HandCount,
                DeckCount = dto.DeckCount,
                DiscardCount = dto.DiscardCount,
                PendingPlanCount = dto.PendingPlanCount,
                IsTurnLocked = dto.IsTurnLocked,
                LockCooldownUntilUnixMs = dto.LockCooldownUntilUnixMs,
            };

            foreach (var buff in dto.Buffs)
            {
                viewState.Buffs.Add(new BattleBuffViewState
                {
                    ConfigId = buff.ConfigId,
                    DisplayName = buff.DisplayName,
                    Value = buff.Value,
                    RemainingRounds = buff.RemainingRounds,
                });
            }

            viewState.BuffSummaryText = BuildBuffSummary(viewState);
            return viewState;
        }

        private static BattleCardViewState ToBattleCardViewState(HandCardDto dto)
        {
            return new BattleCardViewState
            {
                InstanceId = dto.InstanceId,
                BaseConfigId = dto.BaseConfigId,
                EffectiveConfigId = dto.EffectiveConfigId,
                DisplayName = dto.DisplayName,
                Description = dto.Description,
                DisplayedCost = dto.DisplayedCost,
                TrackType = dto.TrackType,
                RequiresDiscardSelection = dto.RequiresDiscardSelection,
            };
        }

        private static PlayerBuildWindowViewState ToPlayerBuildWindowViewState(BuildPlayerWindowDto dto)
        {
            var viewState = new PlayerBuildWindowViewState
            {
                PlayerId = dto.PlayerId,
                DisplayName = dto.DisplayName,
                PreviewHp = dto.PreviewHp,
                MaxHp = dto.MaxHp,
                OpportunityCount = dto.OpportunityCount,
                NextOpportunityIndex = dto.NextOpportunityIndex,
                ResolvedOpportunityCount = dto.ResolvedOpportunityCount,
                IsLocked = dto.IsLocked,
                CanLock = dto.CanLock,
                RestrictionMode = dto.RestrictionKind == BuildWindowRestrictionKind.ForcedRecovery
                    ? BuildWindowRestrictionViewMode.ForcedRecovery
                    : BuildWindowRestrictionViewMode.None,
                RestrictionText = dto.RestrictionKind == BuildWindowRestrictionKind.ForcedRecovery
                    ? "本场战败，只能休息"
                    : string.Empty,
            };

            foreach (var summary in dto.ResolvedChoiceSummaries)
                viewState.ResolvedChoiceSummaries.Add(summary);

            if (dto.CurrentOpportunity != null)
                viewState.CurrentOpportunity = ToBuildOpportunityViewState(dto.CurrentOpportunity);

            return viewState;
        }

        private static BuildOpportunityViewState ToBuildOpportunityViewState(BuildOpportunityDto dto)
        {
            var viewState = new BuildOpportunityViewState
            {
                OpportunityIndex = dto.OpportunityIndex,
                HealAmount = dto.HealAmount,
                CommittedActionType = ToBuildActionViewType(dto.CommittedActionType),
                DraftGroupsRevealed = dto.DraftGroupsRevealed,
            };

            foreach (var action in dto.AvailableActions)
                viewState.AvailableActions.Add(ToBuildActionViewType(action));

            foreach (var card in dto.UpgradableCards)
                viewState.UpgradableCards.Add(ToBuildCardViewState(card));

            foreach (var card in dto.RemovableCards)
                viewState.RemovableCards.Add(ToBuildCardViewState(card));

            foreach (var group in dto.DraftGroups)
            {
                var groupView = new BuildDraftGroupViewState { GroupIndex = group.GroupIndex };
                foreach (var offer in group.Offers)
                {
                    groupView.Offers.Add(new BuildDraftOfferViewState
                    {
                        OfferId = offer.OfferId,
                        PersistentCardId = offer.PersistentCardId,
                        ConfigId = offer.BaseConfigId,
                        EffectiveConfigId = offer.EffectiveConfigId,
                        DisplayName = offer.DisplayName,
                        Description = offer.Description,
                        RarityText = ToRarityText(offer.Rarity),
                        Cost = offer.EnergyCost,
                        UpgradeLevel = offer.UpgradeLevel,
                        IsUpgraded = offer.IsUpgraded,
                    });
                }

                viewState.DraftGroups.Add(groupView);
            }

            return viewState;
        }

        private static BuildCardViewState ToBuildCardViewState(BuildCardCandidateDto dto)
        {
            return new BuildCardViewState
            {
                PersistentCardId = dto.PersistentCardId,
                ConfigId = dto.BaseConfigId,
                EffectiveConfigId = dto.EffectiveConfigId,
                DisplayName = dto.DisplayName,
                Description = dto.Description,
                Cost = dto.EnergyCost,
                UpgradeLevel = dto.UpgradeLevel,
            };
        }

        private static BattleClientPhaseKind ToBattlePhaseKind(ServerPhaseKind phaseKind)
        {
            return phaseKind switch
            {
                ServerPhaseKind.BattleOperation => BattleClientPhaseKind.Operation,
                ServerPhaseKind.BattleSettlement => BattleClientPhaseKind.Settlement,
                ServerPhaseKind.BuildWindow => BattleClientPhaseKind.BuildWindow,
                ServerPhaseKind.MatchEnded => BattleClientPhaseKind.MatchEnded,
                _ => BattleClientPhaseKind.Unknown,
            };
        }

        private static BuildActionViewType ToBuildActionViewType(ProtocolBuildActionType actionType)
        {
            return actionType switch
            {
                ProtocolBuildActionType.Heal => BuildActionViewType.Heal,
                ProtocolBuildActionType.AddCard => BuildActionViewType.AddCard,
                ProtocolBuildActionType.RemoveCard => BuildActionViewType.RemoveCard,
                ProtocolBuildActionType.UpgradeCard => BuildActionViewType.UpgradeCard,
                _ => BuildActionViewType.None,
            };
        }

        private static string ToRarityText(ProtocolBuildCardRarity rarity)
        {
            return rarity switch
            {
                ProtocolBuildCardRarity.Uncommon => "罕见",
                ProtocolBuildCardRarity.Rare => "稀有",
                ProtocolBuildCardRarity.Legendary => "传说",
                _ => "普通",
            };
        }

        private static string BuildPhaseDisplayText(BattleClientPhaseKind phaseKind, int battleIndex, int totalBattleCount)
        {
            string phaseText = phaseKind switch
            {
                BattleClientPhaseKind.Operation => "操作期",
                BattleClientPhaseKind.OpponentAction => "对手行动",
                BattleClientPhaseKind.Settlement => "结算期",
                BattleClientPhaseKind.BuildWindow => "构筑阶段",
                BattleClientPhaseKind.MatchEnded => "整局结束",
                _ => "阶段同步",
            };

            return $"第 {battleIndex}/{totalBattleCount} 场 · {phaseText}";
        }

        private static string BuildTimerText(BattleClientPhaseKind phaseKind)
        {
            return phaseKind switch
            {
                BattleClientPhaseKind.Operation => "操作中（服务端同步）",
                BattleClientPhaseKind.Settlement => "结算中...",
                BattleClientPhaseKind.BuildWindow => "构筑阶段",
                BattleClientPhaseKind.MatchEnded => "整局结束",
                _ => string.Empty,
            };
        }

        private static string BuildBuffSummary(BattlePlayerViewState player)
        {
            if (player.Buffs.Count == 0)
                return "无";

            var parts = new System.Collections.Generic.List<string>(player.Buffs.Count);
            foreach (var buff in player.Buffs)
            {
                if (buff.Value > 0 && buff.RemainingRounds > 0)
                    parts.Add($"{buff.DisplayName}({buff.Value}，{buff.RemainingRounds}回合)");
                else if (buff.Value > 0)
                    parts.Add($"{buff.DisplayName}({buff.Value})");
                else if (buff.RemainingRounds > 0)
                    parts.Add($"{buff.DisplayName}({buff.RemainingRounds}回合)");
                else
                    parts.Add(buff.DisplayName);
            }

            return string.Join(" / ", parts);
        }
    }
}
