using System;
using System.Collections.Generic;
using CardMoba.MatchFlow.Deck;
using CardMoba.Protocol.Enums;

namespace CardMoba.MatchFlow.Context
{
    public sealed class PlayerLoadout
    {
        public HeroClass ClassId { get; set; } = HeroClass.Universal;
        public string? HeroId { get; set; }
        public List<string> EquipmentIds { get; } = new List<string>();
        public string? DefaultBuildPoolId { get; set; }

        public PlayerLoadout Clone()
        {
            var clone = new PlayerLoadout
            {
                ClassId = ClassId,
                HeroId = HeroId,
                DefaultBuildPoolId = DefaultBuildPoolId,
            };

            foreach (var equipmentId in EquipmentIds)
                clone.EquipmentIds.Add(equipmentId);

            return clone;
        }
    }

    public sealed class PlayerMatchState
    {
        public string PlayerId { get; set; } = string.Empty;
        public string TeamId { get; set; } = string.Empty;
        public int PersistentHp { get; set; }
        public int MaxHp { get; set; }
        public PersistentDeckState Deck { get; set; } = new PersistentDeckState();
        public int BonusBuildPickCount { get; set; }
        public bool IsConnected { get; set; } = true;
        public bool IsAutoControlled { get; set; }
        public bool IsBuildLocked { get; set; }
        public bool WasDefeatedInLastBattle { get; set; }
        public PlayerLoadout Loadout { get; set; } = new PlayerLoadout();

        public PlayerMatchState Clone()
        {
            return new PlayerMatchState
            {
                PlayerId = PlayerId,
                TeamId = TeamId,
                PersistentHp = PersistentHp,
                MaxHp = MaxHp,
                Deck = Deck.Clone(),
                BonusBuildPickCount = BonusBuildPickCount,
                IsConnected = IsConnected,
                IsAutoControlled = IsAutoControlled,
                IsBuildLocked = IsBuildLocked,
                WasDefeatedInLastBattle = WasDefeatedInLastBattle,
                Loadout = Loadout.Clone(),
            };
        }
    }
}
