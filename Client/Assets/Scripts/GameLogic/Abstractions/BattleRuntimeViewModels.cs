using System.Collections.Generic;
using CardMoba.Protocol.Enums;

namespace CardMoba.Client.GameLogic.Abstractions
{
    /// <summary>
    /// 客户端阶段视图类型。
    /// UI 只依赖这一层，不直接依赖 MatchFlow 或服务端协议枚举。
    /// </summary>
    public enum BattleClientPhaseKind
    {
        Unknown = 0,
        Operation = 1,
        OpponentAction = 2,
        Settlement = 3,
        BuildWindow = 4,
        MatchEnded = 5,
    }

    /// <summary>
    /// 阶段显示视图模型。
    /// </summary>
    public sealed class BattlePhaseViewState
    {
        public BattleClientPhaseKind PhaseKind { get; set; }
        public string DisplayText { get; set; } = string.Empty;
        public string TimerText { get; set; } = string.Empty;
        public bool UseOperationTimer { get; set; }
    }

    /// <summary>
    /// 战斗快照视图模型。
    /// 本地 runtime 与联机 runtime 都应映射到这一层。
    /// </summary>
    public sealed class BattleSnapshotViewState
    {
        public string MatchId { get; set; } = string.Empty;
        public string BattleId { get; set; } = string.Empty;
        public int BattleIndex { get; set; }
        public int TotalBattleCount { get; set; }
        public int CurrentRound { get; set; }
        public bool IsBattleOver { get; set; }
        public BattleClientPhaseKind PhaseKind { get; set; }
        public BattlePlayerViewState LocalPlayer { get; set; } = new BattlePlayerViewState();
        public BattlePlayerViewState OpponentPlayer { get; set; } = new BattlePlayerViewState();
        public List<BattleCardViewState> LocalHandCards { get; } = new List<BattleCardViewState>();
        public List<BattleCardViewState> LocalDiscardCards { get; } = new List<BattleCardViewState>();
    }

    /// <summary>
    /// 玩家战斗面板视图模型。
    /// </summary>
    public sealed class BattlePlayerViewState
    {
        public string PlayerId { get; set; } = string.Empty;
        public string TeamId { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public bool IsAlive { get; set; }
        public int Hp { get; set; }
        public int MaxHp { get; set; }
        public int Shield { get; set; }
        public int Armor { get; set; }
        public int Energy { get; set; }
        public int MaxEnergy { get; set; }
        public int HandCount { get; set; }
        public int DeckCount { get; set; }
        public int DiscardCount { get; set; }
        public int PendingPlanCount { get; set; }
        public bool IsTurnLocked { get; set; }
        public long LockCooldownUntilUnixMs { get; set; }
        public string BuffSummaryText { get; set; } = string.Empty;
        public List<BattleBuffViewState> Buffs { get; } = new List<BattleBuffViewState>();
    }

    /// <summary>
    /// Buff 视图模型。
    /// </summary>
    public sealed class BattleBuffViewState
    {
        public string ConfigId { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public int Value { get; set; }
        public int RemainingRounds { get; set; }
    }

    /// <summary>
    /// 战斗卡牌视图模型。
    /// </summary>
    public sealed class BattleCardViewState
    {
        public string InstanceId { get; set; } = string.Empty;
        public string BaseConfigId { get; set; } = string.Empty;
        public string EffectiveConfigId { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public int DisplayedCost { get; set; }
        public CardTrackType TrackType { get; set; }
        public bool RequiresDiscardSelection { get; set; }
    }

    /// <summary>
    /// 构筑动作视图类型，避免 UI 直接依赖 MatchFlow 运行时枚举。
    /// </summary>
    public enum BuildActionViewType
    {
        None = 0,
        Heal = 1,
        AddCard = 2,
        RemoveCard = 3,
        UpgradeCard = 4,
    }

    /// <summary>
    /// 构筑限制模式。
    /// </summary>
    public enum BuildWindowRestrictionViewMode
    {
        None = 0,
        ForcedRecovery = 1,
    }

    /// <summary>
    /// 构筑窗口整体视图模型。
    /// </summary>
    public sealed class BuildWindowViewState
    {
        public int BattleIndex { get; set; }
        public int TotalBattleCount { get; set; }
        public string BattleTitle { get; set; } = string.Empty;
        public string DisplayText { get; set; } = string.Empty;
        public long DeadlineUnixMs { get; set; }
        public string LocalPlayerId { get; set; } = string.Empty;
        public PlayerBuildWindowViewState LocalPlayer { get; set; }
        public List<PlayerBuildWindowViewState> Players { get; } = new List<PlayerBuildWindowViewState>();
    }

    /// <summary>
    /// 单个玩家的构筑窗口视图模型。
    /// </summary>
    public sealed class PlayerBuildWindowViewState
    {
        public string PlayerId { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public int PreviewHp { get; set; }
        public int MaxHp { get; set; }
        public int OpportunityCount { get; set; }
        public int NextOpportunityIndex { get; set; }
        public int ResolvedOpportunityCount { get; set; }
        public bool IsLocked { get; set; }
        public bool CanLock { get; set; }
        public BuildWindowRestrictionViewMode RestrictionMode { get; set; }
        public string RestrictionText { get; set; } = string.Empty;
        public List<string> ResolvedChoiceSummaries { get; } = new List<string>();
        public BuildOpportunityViewState CurrentOpportunity { get; set; }
    }

    /// <summary>
    /// 当前可编辑的单次构筑机会视图模型。
    /// </summary>
    public sealed class BuildOpportunityViewState
    {
        public int OpportunityIndex { get; set; }
        public int HealAmount { get; set; }
        public BuildActionViewType CommittedActionType { get; set; }
        public bool DraftGroupsRevealed { get; set; }
        public List<BuildActionViewType> AvailableActions { get; } = new List<BuildActionViewType>();
        public List<BuildCardViewState> UpgradableCards { get; } = new List<BuildCardViewState>();
        public List<BuildCardViewState> RemovableCards { get; } = new List<BuildCardViewState>();
        public List<BuildDraftGroupViewState> DraftGroups { get; } = new List<BuildDraftGroupViewState>();
    }

    /// <summary>
    /// 构筑卡牌候选视图模型。
    /// </summary>
    public sealed class BuildCardViewState
    {
        public string PersistentCardId { get; set; } = string.Empty;
        public string ConfigId { get; set; } = string.Empty;
        public string EffectiveConfigId { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public int Cost { get; set; }
        public int UpgradeLevel { get; set; }
    }

    /// <summary>
    /// 单组拿牌候选。
    /// </summary>
    public sealed class BuildDraftGroupViewState
    {
        public int GroupIndex { get; set; }
        public List<BuildDraftOfferViewState> Offers { get; } = new List<BuildDraftOfferViewState>();
    }

    /// <summary>
    /// 拿牌候选视图模型。
    /// </summary>
    public sealed class BuildDraftOfferViewState
    {
        public string OfferId { get; set; } = string.Empty;
        public string PersistentCardId { get; set; } = string.Empty;
        public string ConfigId { get; set; } = string.Empty;
        public string EffectiveConfigId { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string RarityText { get; set; } = string.Empty;
        public int Cost { get; set; }
        public int UpgradeLevel { get; set; }
        public bool IsUpgraded { get; set; }
    }

    /// <summary>
    /// UI 提交给运行时的构筑选择。
    /// </summary>
    public sealed class BuildChoiceViewState
    {
        public BuildActionViewType ActionType { get; set; }
        public string TargetPersistentCardId { get; set; } = string.Empty;
        public Dictionary<int, string> SelectedDraftOfferIdsByGroup { get; } = new Dictionary<int, string>();
    }
}
