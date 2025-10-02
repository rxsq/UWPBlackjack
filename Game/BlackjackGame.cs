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
        public Phase Phase { get; set; } = Phase.Betting;
        public int Bankroll { get; set; } = 500;
        public int Bet { get; set; } = 50;

        public Hand Player { get; } = new Hand();
        public Hand Dealer { get; } = new Hand();

        public string LastOutcome { get; set; } = "";
        public int LastPayout { get; set; } = 0;

        public readonly Deck Deck = new();
        public bool DealerShouldHit => Dealer.Value < 17;

        public void StartSession()
        {
            Bankroll = 500;
            Bet = 25;
            Phase = Phase.Betting;
            LastOutcome = "";
            LastPayout = 0;
        }

        public void AdjustBet(int amount)
        {
            if (Phase != Phase.Betting) return;
            Bet = Math.Max(10, Math.Min(1000, Bet + amount));
            Bet = Math.Min(Bet, Math.Max(10, Bankroll)); // can't bet more than bankroll
        }

        public void NewRound()
        {
            if (Phase != Phase.Betting) return;

            if (Bankroll <= 0)
            {
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

            Deck.Shuffle();

            Phase = Phase.Dealing;
        }

        public void Hit()
        {
            if (Phase != Phase.PlayerTurn) return;

            Player.Add(Deck.Draw());

            if (Player.IsBust)
            {
                Phase = Phase.RoundOver;
                Settle();
            }
        }

        public void DealerHitOne()
        {
            Dealer.Add(Deck.Draw());
        }

        public void FinishDealer()
        {
            Phase = Phase.RoundOver;
            Settle();
        }

        public void Stand()
        {
            if (Phase != Phase.PlayerTurn) return;
            Phase = Phase.DealerTurn;
        }

        public void NextHand()
        {
            if (Phase == Phase.RoundOver)
                Phase = Phase.Betting;
        }

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
