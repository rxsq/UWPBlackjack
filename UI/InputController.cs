using UWPBlackjack.Game;
using Windows.UI.Core;
using Windows.System;

namespace UWPBlackjack.UI
{
    public sealed class InputController
    {
        private readonly BlackjackGame _game;
        public InputController(BlackjackGame game) => _game = game;

        public void OnKeyDown(CoreWindow sender, KeyEventArgs args)
        {
            switch (args.VirtualKey)
            {
                case VirtualKey.N:
                    if (_game.Phase == Phase.RoundOver) _game.NextHand();
                    else _game.NewRound();
                    break;

                case VirtualKey.H:
                    _game.Hit();
                    break;

                case VirtualKey.S:
                    _game.Stand();
                    break;

                case VirtualKey.Add:       
                    _game.AdjustBet(+10);
                    break;

                case VirtualKey.Subtract:  
                    _game.AdjustBet(-10);
                    break;
            }
        }

        public void OnPointerPressed(CoreWindow sender, PointerEventArgs args)
        {
            // (Optional) Later: hit-test on drawn buttons if you add them.
        }
    }
}
