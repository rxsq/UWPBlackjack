using System;
using System.Collections.Generic;
using System.Linq;
using Windows.Media;

namespace UWPBlackjack.Core
{
    public sealed class Deck
    {
        private readonly Card[] _cards;
        private int _top;
        private readonly Random _rng = new();

        public Deck()
        {
            _cards = Enumerable.Range(0, 52)
                .Select(i => new Card((i % 13) + 1, (Suit)(i / 13)))
                .ToArray();
            Shuffle();
        }

        /// <summary>
        /// Number of cards remaining in the deck.
        /// </summary>
        public int Count => _cards.Length - _top;

        /// <summary>
        /// Draws the top card. If empty, automatically reshuffles a new 52-card deck.
        /// </summary>
        public Card Draw()
        {
            if (_top >= _cards.Length)
            {
                Shuffle();
            }
            return _cards[_top++];
        }

        /// <summary>
        /// Resets and shuffles the deck using Fisher–Yates.
        /// </summary>
        public void Shuffle()
        {
            _top = 0;
            for (int i = _cards.Length - 1; i > 0; i--)
            {
                int j = _rng.Next(i + 1);
                (_cards[i], _cards[j]) = (_cards[j], _cards[i]);
            }
        }
    }
}
