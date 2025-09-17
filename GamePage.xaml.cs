using Microsoft.Graphics.Canvas.UI.Xaml;
using UWPBlackjack.Game;
using UWPBlackjack.Rendering;
using UWPBlackjack.UI;
using Windows.UI;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace UWPBlackjack
{
    /// <summary>
    /// Hosts the Win2D canvas and wires game loop (Update/Draw) and input.
    /// All game visuals are rendered in code via <see cref="GameRenderer"/> .
    /// </summary>
    public sealed partial class GamePage : Page
    {
        /// <summary>
        /// The animated Win2D surface. Triggers Update at a steady cadence and Draw every frame.
        /// </summary>
        private readonly CanvasAnimatedControl _canvas = default!; 

        /// <summary>
        /// Core game state / rules (no rendering here).
        /// </summary>
        private BlackjackGame _game = default!;

        /// <summary>
        /// Renders the current <see cref="BlackjackGame"/> to the canvas.
        /// </summary>
        private GameRenderer _renderer = default!;

        /// <summary>
        /// Translates keyboard/mouse into game actions.
        /// </summary>
        private InputController _input = default!;

        /// <summary>
        /// Main constructor, creates canvas and hooks events.
        /// Game objects are created later in <see cref="OnLoaded"/> to ensure the page
        /// is in the visual tree before we attach input and start the game.
        /// </summary>
        public GamePage()
        {
            InitializeComponent();

            _canvas = new CanvasAnimatedControl();

            // Wire game loop callbacks (Update/Draw)
            _canvas.Update += OnUpdate;
            _canvas.Draw += OnDraw;

            Root.Children.Add(_canvas);

            // Defer game construction and input hookup until page is loaded
            Loaded += OnLoaded;
            Unloaded += OnUnloaded;
        }

        /// <summary>
        /// Runs when the page enters the visual tree. Safe point to create game
        /// objects, attach CoreWindow input, and start the first round.
        /// </summary>
        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            _game = new BlackjackGame();
            _renderer = new GameRenderer(_game);
            _input = new InputController(_game);

            // Attach keyboard + pointer to current CoreWindow
            var cw = Window.Current.CoreWindow;
            cw.KeyDown += _input.OnKeyDown;
            cw.PointerPressed += _input.OnPointerPressed;

            // Start a new player session and deal the first round
            _game.StartSession();
            _game.NewRound();
        }

        /// <summary>
        /// Per-frame update hook from CanvasAnimatedControl.
        /// Put timers/animations/state transitions inside the game.
        /// </summary>
        private void OnUpdate(ICanvasAnimatedControl sender, CanvasAnimatedUpdateEventArgs args)
        {
            _game?.Update(args.Timing.ElapsedTime);
        }

        /// <summary>
        /// Per-frame draw hook from CanvasAnimatedControl.
        /// Clears the background and delegates to the renderer.
        /// </summary>
        private void OnDraw(ICanvasAnimatedControl sender, CanvasAnimatedDrawEventArgs args)
        {
            // During initialization/ first frames, _renderer might not be ready yet.
            if (_renderer == null)
            {
                args.DrawingSession.Clear(Colors.DarkGreen);
                args.DrawingSession.DrawText("Initializing…", 20, 20, Colors.White);
                return;
            }

            _renderer.Draw(args.DrawingSession);
        }

        /// <summary>
        /// Runs when the page leaves the visual tree. Always detach CoreWindow
        /// handlers and dispose the Win2D control to avoid device/context leaks.
        /// </summary>
        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            var cw = Window.Current.CoreWindow;
            if (_input != null)
            {
                cw.KeyDown -= _input.OnKeyDown;
                cw.PointerPressed -= _input.OnPointerPressed;
            }

            _canvas?.RemoveFromVisualTree();
        }
    }
}
