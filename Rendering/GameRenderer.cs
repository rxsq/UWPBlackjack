using System.Collections.Generic;
using Microsoft.Graphics.Canvas;
using Microsoft.Graphics.Canvas.Text;
using UWPBlackjack.Core;
using UWPBlackjack.Game;
using Windows.Foundation;
using Windows.UI;
using Windows.UI.Text;

namespace UWPBlackjack.Rendering
{
    public sealed class GameRenderer
    {
        private readonly BlackjackGame _game;

        private readonly CanvasTextFormat _hud = new CanvasTextFormat { FontFamily = "Segoe UI", FontSize = 22 };
        private readonly CanvasTextFormat _label = new CanvasTextFormat { FontFamily = "Segoe UI", FontSize = 20 };
        private readonly CanvasTextFormat _rank = new CanvasTextFormat { FontFamily = "Segoe UI", FontWeight = FontWeights.SemiBold, FontSize = 24 };
        private readonly CanvasTextFormat _big = new CanvasTextFormat { FontFamily = "Segoe UI", FontSize = 36, FontWeight = FontWeights.SemiBold };

        // Card layout
        private const float CardW = 80f;
        private const float CardH = 120f;
        private const float CardR = 10f;
        private const float CardGap = 24f;

        public GameRenderer(BlackjackGame game) => _game = game;

        public void Draw(CanvasDrawingSession ds)
        {
            // Table background
            ds.Clear(Color.FromArgb(255, 10, 90, 40));

            // HUD
            ds.DrawText($"Bankroll: ${_game.Bankroll}", 20, 20, Colors.White, _hud);
            ds.DrawText($"Bet: ${_game.Bet}", 20, 48, Colors.White, _hud);
            ds.DrawText($"Phase: {_game.Phase}", 20, 76, Colors.WhiteSmoke, _hud);
            ds.DrawText("Keys: N=new/next • H=hit • S=stand • +/- bet", 20, 104, Colors.LightGray, _hud);

            // Dealer area
            ds.DrawText("Dealer", 100, 150, Colors.White, _label);
            DrawHand(ds, _game.Dealer, new Point(100, 180), hideHoleCard: _game.Phase == Phase.PlayerTurn);

            // Player area
            ds.DrawText($"Player ({_game.Player.Value})", 100, 360, Colors.White, _label);
            DrawHand(ds, _game.Player, new Point(100, 390), hideHoleCard: false);

            // Round outcome
            if (_game.Phase == Phase.RoundOver && !string.IsNullOrEmpty(_game.LastOutcome))
            {
                ds.DrawText(_game.LastOutcome, 100, 570, Colors.Gold, _big);
                ds.DrawText("Press N for next hand", 100, 612, Colors.WhiteSmoke, _label);
            }
        }

        private void DrawHand(CanvasDrawingSession ds, Core.Hand hand, Point start, bool hideHoleCard)
        {
            float x = (float)start.X;
            float y = (float)start.Y;

            for (int i = 0; i < hand.Cards.Count; i++)
            {
                bool isHole = hideHoleCard && i == 1; // hide dealer’s second card during player turn
                DrawCard(ds, isHole ? (Card?)null : hand.Cards[i], new Rect(x, y, CardW, CardH));
                x += CardW + CardGap;
            }
        }

        private static readonly Dictionary<Suit, string> SuitChar = new()
        {
            { Suit.Spades, "♠" }, { Suit.Hearts, "♥" }, { Suit.Diamonds, "♦" }, { Suit.Clubs, "♣" }
        };

        private static string RankText(int r) => r switch
        {
            1 => "A",
            11 => "J",
            12 => "Q",
            13 => "K",
            _ => r.ToString()
        };

        private void DrawCard(CanvasDrawingSession ds, Card? card, Rect rect)
        {
            // Face / back
            if (card.HasValue)
            {
                ds.FillRoundedRectangle(rect, CardR, CardR, Colors.White);
                ds.DrawRoundedRectangle(rect, CardR, CardR, Colors.Black, 2f);

                var c = card.Value;
                bool isRed = c.Suit == Suit.Hearts || c.Suit == Suit.Diamonds;
                var ink = isRed ? Colors.Red : Colors.Black;

                // Rank top-left
                ds.DrawText(RankText(c.Rank), (float)rect.X + 8, (float)rect.Y + 6, ink, _rank);
                ds.DrawText(SuitChar[c.Suit], (float)rect.X + 8, (float)rect.Y + 32, ink, _rank);

                // Big suit center (subtle)
                ds.DrawText(SuitChar[c.Suit],
                    (float)rect.X + (float)rect.Width / 2 - 12,
                    (float)rect.Y + (float)rect.Height / 2 - 16,
                    Color.FromArgb(140, ink.R, ink.G, ink.B), _big);
            }
            else
            {
                // Back-of-card
                ds.FillRoundedRectangle(rect, CardR, CardR, Color.FromArgb(255, 30, 30, 120));
                ds.DrawRoundedRectangle(rect, CardR, CardR, Colors.White, 2f);
                // simple hatch
                for (float i = (float)rect.X + 6; i < rect.X + rect.Width - 6; i += 10)
                    ds.DrawLine(i, (float)rect.Y + 6, i, (float)rect.Y + (float)rect.Height - 6, Color.FromArgb(90, 255, 255, 255), 1f);
            }
        }
    }
}
