using System.Collections.Generic;
using CardMoba.Protocol.Enums;

namespace CardMoba.Protocol.Messages.Common
{
    public enum ServerPhaseKind
    {
        Unknown = 0,
        BattleOperation = 1,
        BattleSettlement = 2,
        BuildWindow = 3,
        MatchEnded = 4,
    }

    public enum BuildWindowRestrictionKind
    {
        None = 0,
        ForcedRecovery = 1,
    }

    public enum ProtocolBuildActionType
    {
        None = 0,
        Heal = 1,
        AddCard = 2,
        RemoveCard = 3,
        UpgradeCard = 4,
        CustomPlaceholder = 99,
    }

    public enum ProtocolBuildCardRarity
    {
        Common = 1,
        Uncommon = 2,
        Rare = 3,
        Legendary = 4,
    }

    public enum ProtocolBattleEndReason
    {
        None = 0,
        RoundLimitReached = 1,
        TeamEliminated = 2,
        ObjectiveDestroyed = 3,
    }

    public enum ProtocolMatchEndReason
    {
        None = 0,
        ObjectiveDestroyed = 1,
    }

    public enum ProtocolActionErrorCode
    {
        Unknown = 0,
        InvalidPhase = 1,
        PlayerNotFound = 2,
        CardNotFound = 3,
        CardOwnershipMismatch = 4,
        PlayRuleBlocked = 5,
        NotEnoughEnergy = 6,
        PlayerCannotAct = 7,
        BuildChoiceInvalid = 8,
        BuildWindowLockBlocked = 9,
        ActionExecutionFailed = 10,
        BattleTurnLockCooldown = 11,
        BattleTurnLockStateInvalid = 12,
    }

    public sealed class MatchParticipantDto
    {
        public string PlayerId { get; set; } = string.Empty;
        public string TeamId { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public bool IsReady { get; set; }
    }

    public sealed class BuildChoiceDto
    {
        public ProtocolBuildActionType ActionType { get; set; }
        public string TargetPersistentCardId { get; set; } = string.Empty;
        public Dictionary<int, string?> SelectedDraftOfferIdsByGroup { get; } = new Dictionary<int, string?>();
    }

    public sealed class BattleBuffDto
    {
        public string ConfigId { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public int Value { get; set; }
        public int RemainingRounds { get; set; }
    }

    public sealed class HandCardDto
    {
        public string InstanceId { get; set; } = string.Empty;
        public string BaseConfigId { get; set; } = string.Empty;
        public string EffectiveConfigId { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public int EnergyCost { get; set; }
        public int DisplayedCost { get; set; }
        public CardTrackType TrackType { get; set; }
        public bool RequiresDiscardSelection { get; set; }
    }

    public sealed class BattlePlayerStateDto
    {
        public string PlayerId { get; set; } = string.Empty;
        public string TeamId { get; set; } = string.Empty;
        public bool IsLocalPlayer { get; set; }
        public bool IsAlive { get; set; }
        public int Hp { get; set; }
        public int MaxHp { get; set; }
        public int Shield { get; set; }
        public int Armor { get; set; }
        public int Energy { get; set; }
        public int MaxEnergy { get; set; }
        public int DeckCount { get; set; }
        public int DiscardCount { get; set; }
        public int HandCount { get; set; }
        public int PendingPlanCount { get; set; }
        public bool IsTurnLocked { get; set; }
        public long LockCooldownUntilUnixMs { get; set; }
        public List<BattleBuffDto> Buffs { get; } = new List<BattleBuffDto>();
        public List<HandCardDto> HandCards { get; } = new List<HandCardDto>();
        public List<HandCardDto> DiscardCards { get; } = new List<HandCardDto>();
    }

    public sealed class BattleSnapshotDto
    {
        public string MatchId { get; set; } = string.Empty;
        public string BattleId { get; set; } = string.Empty;
        public int BattleIndex { get; set; }
        public int TotalBattleCount { get; set; }
        public int CurrentRound { get; set; }
        public bool IsBattleOver { get; set; }
        public ServerPhaseKind PhaseKind { get; set; }
        public BattlePlayerStateDto LocalPlayer { get; set; } = new BattlePlayerStateDto();
        public BattlePlayerStateDto OpponentPlayer { get; set; } = new BattlePlayerStateDto();
    }

    public sealed class BuildCardCandidateDto
    {
        public string PersistentCardId { get; set; } = string.Empty;
        public string BaseConfigId { get; set; } = string.Empty;
        public string EffectiveConfigId { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public int EnergyCost { get; set; }
        public int UpgradeLevel { get; set; }
    }

    public sealed class BuildDraftOfferDto
    {
        public string OfferId { get; set; } = string.Empty;
        public string PersistentCardId { get; set; } = string.Empty;
        public string BaseConfigId { get; set; } = string.Empty;
        public string EffectiveConfigId { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public int EnergyCost { get; set; }
        public ProtocolBuildCardRarity Rarity { get; set; }
        public int UpgradeLevel { get; set; }
        public bool IsUpgraded { get; set; }
    }

    public sealed class BuildDraftGroupDto
    {
        public int GroupIndex { get; set; }
        public List<BuildDraftOfferDto> Offers { get; } = new List<BuildDraftOfferDto>();
    }

    public sealed class BuildOpportunityDto
    {
        public int OpportunityIndex { get; set; }
        public int HealAmount { get; set; }
        public bool DraftGroupsRevealed { get; set; }
        public ProtocolBuildActionType CommittedActionType { get; set; }
        public List<ProtocolBuildActionType> AvailableActions { get; } = new List<ProtocolBuildActionType>();
        public List<BuildCardCandidateDto> UpgradableCards { get; } = new List<BuildCardCandidateDto>();
        public List<BuildCardCandidateDto> RemovableCards { get; } = new List<BuildCardCandidateDto>();
        public List<BuildDraftGroupDto> DraftGroups { get; } = new List<BuildDraftGroupDto>();
    }

    public sealed class BuildPlayerWindowDto
    {
        public string PlayerId { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public bool IsLocalPlayer { get; set; }
        public bool IsLocked { get; set; }
        public bool CanLock { get; set; }
        public int PreviewHp { get; set; }
        public int MaxHp { get; set; }
        public int OpportunityCount { get; set; }
        public int NextOpportunityIndex { get; set; }
        public int ResolvedOpportunityCount { get; set; }
        public BuildWindowRestrictionKind RestrictionKind { get; set; }
        public List<string> ResolvedChoiceSummaries { get; } = new List<string>();
        public BuildOpportunityDto? CurrentOpportunity { get; set; }
    }

    public sealed class BuildWindowDto
    {
        public string MatchId { get; set; } = string.Empty;
        public int BattleIndex { get; set; }
        public int TotalBattleCount { get; set; }
        public string BattleTitle { get; set; } = string.Empty;
        public long DeadlineUnixMs { get; set; }
        public string LocalPlayerId { get; set; } = string.Empty;
        public BuildPlayerWindowDto LocalPlayer { get; set; } = new BuildPlayerWindowDto();
        public List<BuildPlayerWindowDto> Players { get; } = new List<BuildPlayerWindowDto>();
    }

    public sealed class BattleResultDto
    {
        public string MatchId { get; set; } = string.Empty;
        public string BattleId { get; set; } = string.Empty;
        public int BattleIndex { get; set; }
        public int TotalBattleCount { get; set; }
        public ProtocolBattleEndReason BattleEndReason { get; set; }
        public bool MatchTerminated { get; set; }
        public ProtocolMatchEndReason MatchEndReason { get; set; }
        public string? WinningTeamId { get; set; }
        public string? DestroyedObjectiveEntityId { get; set; }
        public List<string> DeadPlayerIds { get; } = new List<string>();
    }
}
