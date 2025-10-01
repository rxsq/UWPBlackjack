using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace UWPBlackjack.Core
{
    public sealed class Hand
    {
        private readonly List<Card> _cards = [];
        public ReadOnlyCollection<Card> Cards => _cards.AsReadOnly();

        public void Clear() => _cards.Clear();
        public void Add(Card c) => _cards.Add(c);
        public int Count => _cards.Count;

        /// <summary>
        /// Standard blackjack total:
        /// - J/Q/K are 10
        /// - Aces count as 11 where possible without busting; otherwise as 1.
        /// </summary>
        public int Value
        {
            get
            {
                int total = 0;
                int aceCount = 0;

                foreach (var c in _cards)
                {
                    if (c.IsAce) { aceCount++; total += 11; }
                    else total += c.FaceValue; // 2..10 as-is, J/Q/K -> 10
                }

                // Demote Aces from 11 to 1 until we’re not busting
                while (total > 21 && aceCount-- > 0)
                    total -= 10;

                return total;
            }
        }

        /// <summary>
        /// True if at least one Ace is counting as 11 in the current Value.
        /// </summary>
        public bool IsSoft
        {
            get
            {
                if (_cards.Count == 0) return false;

                int hardTotal = _cards.Sum(c => c.IsAce ? 1 : c.FaceValue);
                int softTotal = Value;
                return softTotal != hardTotal && softTotal <= 21;
            }
        }

        public bool IsBlackjack => _cards.Count == 2 && Value == 21;
        public bool IsBust => Value > 21;

        public override string ToString() => $"{string.Join(", ", _cards)} ({Value})";
    }
}
