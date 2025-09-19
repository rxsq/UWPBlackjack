using System;
using System.Threading;
using UWPBlackjack.Core;

namespace UWPBlackjack.Game
{
    /// <summary>
    /// Core blackjack rules + round lifecycle 
    /// </summary>
    public class BlackjackGame
    {
        public Phase Phase { get; private set; } = Phase.Betting;
        public int Bankroll { get; private set; } = 1000;
        public int Bet { get; private set; } = 50;

        public Hand Player { get; } = new Hand();
        public Hand Dealer { get; } = new Hand();

        public string LastOutcome { get; private set; } = "";
        public int LastPayout { get; private set; } = 0;

        private readonly Deck _deck = new();

        public void StartSession()
        {
            Bankroll = 1000;
            Bet = 50;
            Phase = Phase.Betting;
            LastOutcome = "";
            LastPayout = 0;
        }

        /// <summary> Hook for animations/timers later. </summary>
        public void Update(TimeSpan _elapsed) { /* no-op for now */ }

        public void AdjustBet(int delta)
        {
            if (Phase != Phase.Betting) return;
            Bet = Math.Max(10, Math.Min(1000, Bet + delta));
            Bet = Math.Min(Bet, Math.Max(10, Bankroll)); // can't bet more than bankroll
            System.Diagnostics.Debug.WriteLine($"Bet changed: {Bet}");
        }

        public void NewRound()
        {
            if (Phase != Phase.Betting) return;

            if (Bankroll <= 0)
            {
                // broke; lock to RoundOver with message
                Phase = Phase.RoundOver;
                LastOutcome = "Bankroll empty";
                LastPayout = 0;
                return;
            }

            if (Bet > Bankroll) Bet = Bankroll;

            Player.Clear();
            Dealer.Clear();
            LastOutcome = "";
            LastPayout = 0;

            // Simple: shuffle at start of each round (fine for MVP)
            _deck.Shuffle();

            // Initial deal: P, D, P, D
            Player.Add(_deck.Draw());
            Dealer.Add(_deck.Draw());
            Player.Add(_deck.Draw());
            Dealer.Add(_deck.Draw());

            Phase = Phase.PlayerTurn;

            // Immediate blackjack resolution
            if (Player.IsBlackjack || Dealer.IsBlackjack)
            {
                Phase = Phase.RoundOver;
                Settle();
            }
            else
            {
                Phase = Phase.PlayerTurn;
            }
        }

        public void Hit()
        {
            if (Phase != Phase.PlayerTurn) return;

            Player.Add(_deck.Draw());

            if (Player.IsBust)
            {
                Phase = Phase.RoundOver;
                Settle();
            }
        }

        public void Stand()
        {
            if (Phase != Phase.PlayerTurn) return;

            Phase = Phase.DealerTurn;

            // Dealer hits soft 17? Standard house rules vary; we'll stand on all 17s for MVP.
            while (Dealer.Value < 17)
                Dealer.Add(_deck.Draw());

            Phase = Phase.RoundOver;
            Settle();
        }

        public void NextHand()
        {
            if (Phase == Phase.RoundOver)
                Phase = Phase.Betting;
        }

        // --- Settlement --------------------------------------------------------

        private void Settle()
        {
            int pv = Player.Value;
            int dv = Dealer.Value;

            int payout = 0; // net change to bankroll

            if (Player.IsBlackjack && !Dealer.IsBlackjack)
            {
                payout = (int)Math.Round(Bet * 1.5); // 3:2 payout
                LastOutcome = "Blackjack! You win 3:2";
            }
            else if (Dealer.IsBlackjack && !Player.IsBlackjack)
            {
                payout = -Bet;
                LastOutcome = "Dealer blackjack — you lose";
            }
            else if (pv > 21)
            {
                payout = -Bet;
                LastOutcome = "Bust — you lose";
            }
            else if (dv > 21)
            {
                payout = Bet;
                LastOutcome = "Dealer bust — you win";
            }
            else if (pv > dv)
            {
                payout = Bet;
                LastOutcome = "You win";
            }
            else if (pv < dv)
            {
                payout = -Bet;
                LastOutcome = "You lose";
            }
            else
            {
                payout = 0;
                LastOutcome = "Push";
            }

            Bankroll += payout;
            LastPayout = payout;
        }

    }
}
