using System;
using System.Linq;

namespace UWPBlackjack.Core
{
    public sealed class Deck
    {
        private readonly Card[] _cards;
        private int _top;
        private readonly Random _rng = new Random();

        public Deck()
        {
            _cards = Enumerable.Range(0, 52)
                .Select(i => new Card((i % 13) + 1, (Suit)(i / 13)))
                .ToArray();
            Shuffle();
        }

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
            // reset position so dealing starts from top
            _top = 0;
            
            // going backwards through deck
            for (int currentIndex = _cards.Length - 1; currentIndex > 0; currentIndex--)
            {
                // pick a random idx from 0-currentIndex
                int randomIndex = _rng.Next(currentIndex + 1);

                // swap the two cards if they're not the same index
                if (randomIndex != currentIndex)
                {
                    Card temp = _cards[currentIndex];
                    _cards[currentIndex] = _cards[randomIndex];
                    _cards[randomIndex] = temp;
                }
            }
        }
    }
}
