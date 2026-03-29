using System;
using System.Collections.Generic;
using CardMoba.MatchFlow.Context;
using CardMoba.MatchFlow.Rules;

namespace CardMoba.MatchFlow.Context
{
    public sealed class BuildWindowState
    {
        public int StepIndex { get; set; }
        public long OpenAtUnixMs { get; set; }
        public long DeadlineUnixMs { get; set; }
        public Dictionary<string, PlayerBuildWindowState> PlayerWindows { get; } = new Dictionary<string, PlayerBuildWindowState>();
    }

    public sealed class PlayerBuildWindowState
    {
        public string PlayerId { get; set; } = string.Empty;
        public int OpportunityCount { get; set; }
        public int NextOpportunityIndex { get; set; }
        public int PreviewHp { get; set; }
        public int MaxHp { get; set; }
        public float HealPercent { get; set; }
        public BuildWindowRestrictionMode RestrictionMode { get; set; }
        public MatchFlow.Deck.PersistentDeckState PreviewDeck { get; set; } = new MatchFlow.Deck.PersistentDeckState();
        public List<BuildOpportunityState> Opportunities { get; } = new List<BuildOpportunityState>();
        public bool IsLocked { get; set; }
    }

    public sealed class BuildOpportunityState
    {
        public int OpportunityIndex { get; set; }
        public List<BuildActionType> AvailableActions { get; } = new List<BuildActionType>();
        public BuildOfferSet Offers { get; set; } = new BuildOfferSet();
        public BuildChoice? Choice { get; set; }
        public bool IsResolved { get; set; }
    }

    public sealed class BuildOfferSet
    {
        public int HealAmount { get; set; }
        public List<BuildCardCandidate> UpgradableCards { get; } = new List<BuildCardCandidate>();
        public List<BuildCardCandidate> RemovableCards { get; } = new List<BuildCardCandidate>();
        public List<BuildDraftGroup> DraftGroups { get; } = new List<BuildDraftGroup>();
    }

    public sealed class BuildCardCandidate
    {
        public string PersistentCardId { get; set; } = string.Empty;
        public string BaseConfigId { get; set; } = string.Empty;
        public string EffectiveConfigId { get; set; } = string.Empty;
        public int UpgradeLevel { get; set; }
    }

    public sealed class BuildDraftGroup
    {
        public int GroupIndex { get; set; }
        public List<BuildDraftCardOffer> Offers { get; } = new List<BuildDraftCardOffer>();
    }

    public sealed class BuildDraftCardOffer
    {
        public string OfferId { get; set; } = string.Empty;
        public string PersistentCardId { get; set; } = string.Empty;
        public string BaseConfigId { get; set; } = string.Empty;
        public string EffectiveConfigId { get; set; } = string.Empty;
        public int UpgradeLevel { get; set; }
        public MatchFlow.Definitions.BuildCardRarity Rarity { get; set; }
        public bool IsUpgraded { get; set; }
    }

    public sealed class BuildChoice
    {
        public BuildActionType ActionType { get; set; }
        public string? TargetPersistentCardId { get; set; }
        public Dictionary<int, string?> SelectedDraftOfferIdsByGroup { get; } = new Dictionary<int, string?>();

        public static BuildChoice Create(BuildActionType actionType)
        {
            return new BuildChoice { ActionType = actionType };
        }
    }
}
