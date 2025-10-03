using System;

namespace UWPBlackjack.Core
{
    public struct Card
    {
        public int Rank { get; }
        public Suit Suit { get; }
        public Card(int rank, Suit suit)
        {
            if (rank < 1 || rank > 13) 
            {
                throw new ArgumentOutOfRangeException(nameof(rank), "Rank must be 1..13 (Ace..King).");
            }
            Rank = rank;
            Suit = suit;
        }
        public bool IsAce => Rank == 1;
        public bool IsFaceCard => Rank >= 11; // Jack, Queen, King
        public int FaceValue => Rank > 10 ? 10 : Rank; // Face cards are worth 10
        public string DisplayValue
        {
            get
            {
                if (IsAce)
                {
                    return "1/11";
                }
                else if (IsFaceCard)
                {
                    return "10";
                }
                else
                {
                    return Rank.ToString();
                }
            }
        }
    }
}
