using System;
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

        public int HighestScore => HighScoreManager.GetHighestScore();

        /// <summary>
        /// Start session sets initial values
        /// </summary>
        public async void StartSession()
        {
            Bankroll = 500;
            Bet = 25;
            Phase = Phase.Betting;
            LastOutcome = "";
            LastPayout = 0;
            await HighScoreManager.LoadAsync();
        }

        /// <summary>
        /// Allows for bet to be adjusted to specified amount
        /// </summary>
        /// <param name="amount"></param>
        public void AdjustBet(int amount)
        {
            // cannot adjust bet if not in betting
            if (Phase != Phase.Betting) return;

            Bet = Math.Max(10, Bet + amount); // minimum bet is 10 and no maximum bet
            Bet = Math.Min(Bet, Math.Max(10, Bankroll)); // can't bet more than bankroll
        }

        /// <summary>
        /// New round logic, checks for empty bankroll, clears P and D hands, and shuffles deck
        /// </summary>
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

        /// <summary>
        /// Draws a card for the player, and check if they bust
        /// </summary>
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

        /// <summary>
        /// Draws a card for the dealer, and check if they bust
        /// </summary>
        public void DealerHitOne()
        {
            if (Phase != Phase.DealerTurn) return;

            Dealer.Add(Deck.Draw());

            if (Dealer.IsBust)
            {
                Phase = Phase.RoundOver;
                Settle();
            }
        }

        /// <summary>
        /// After dealer's value >= 17, this is called to settle the game
        /// </summary>
        public void FinishDealer()
        {
            Phase = Phase.RoundOver;
            Settle();
        }

        /// <summary>
        /// Stand just changes phase to dealer's turn, ui handles drawing cards, etc
        /// </summary>
        public void Stand()
        {
            if (Phase != Phase.PlayerTurn) return;
            Phase = Phase.DealerTurn;
        }

        /// <summary>
        /// Next hand changes state back to betting
        /// </summary>
        public void NextHand()
        {
            if (Phase == Phase.RoundOver)
            Phase = Phase.Betting;
        }

        /// <summary>
        /// Ui handles drawing of cards, this changes to dealer turn and doubles the bet
        /// </summary>
        public void Double()
        {
            if (Phase != Phase.PlayerTurn || Player.Cards.Count != 2 || (Bankroll-Bet) < Bet)
                return;
            Player.Add(Deck.Draw());
            if (Player.IsBust)
            {
                Phase = Phase.RoundOver;
                Settle();
                return;
            }
            Bet *= 2;
            Phase = Phase.DealerTurn;
        }

        /// <summary>
        /// Game settling, check player value and dealer value and handle it respectively
        /// </summary>
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

            _ = HighScoreManager.AddScoreAsync(Bankroll);
        }

    }
}
