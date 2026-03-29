using System;
using System.Collections.Generic;
using System.Linq;

namespace CardMoba.MatchFlow.Deck
{
    public sealed class PersistentDeckState
    {
        public List<PersistentDeckCard> Cards { get; } = new List<PersistentDeckCard>();

        public PersistentDeckState Clone()
        {
            var clone = new PersistentDeckState();
            foreach (var card in Cards)
                clone.Cards.Add(card.Clone());
            return clone;
        }

        public PersistentDeckCard? FindCard(string persistentCardId)
        {
            return Cards.FirstOrDefault(card => string.Equals(card.PersistentCardId, persistentCardId, StringComparison.Ordinal));
        }

        public void AddCard(PersistentDeckCard card)
        {
            if (card == null)
                throw new ArgumentNullException(nameof(card));

            Cards.Add(card);
        }

        public bool RemoveCard(string persistentCardId)
        {
            var card = FindCard(persistentCardId);
            if (card == null)
                return false;

            Cards.Remove(card);
            return true;
        }

        public List<(string configId, int count)> ToDeckConfig()
        {
            return Cards
                .GroupBy(card => card.GetEffectiveConfigId(), StringComparer.Ordinal)
                .OrderBy(group => group.Key, StringComparer.Ordinal)
                .Select(group => (group.Key, group.Count()))
                .ToList();
        }
    }
}
