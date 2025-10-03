using Windows.UI.Core;
using Windows.System;

namespace UWPBlackjack.Game
{
    public sealed class InputController
    {
        private readonly BlackjackGame _game;
        public InputController(BlackjackGame game) => _game = game;
        /// <summary>
        /// Interacts with game based on key presses 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="args"></param>
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

                case VirtualKey.Up:       
                    _game.AdjustBet(+5);
                    break;

                case VirtualKey.Down:  
                    _game.AdjustBet(-5);
                    break;
            }
        }
    }
}
