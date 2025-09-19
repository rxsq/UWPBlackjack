using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UWPBlackjack.Core;
using UWPBlackjack.Game;
using Windows.UI;
using Windows.UI.Core;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Documents;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Media.Imaging;

namespace UWPBlackjack
{
    public sealed partial class GamePage : Page
    {
        private BlackjackGame _game;
        private InputController _input;

        private const double CARD_WIDTH = 100;
        private const double CARD_HEIGHT = 150;

        private bool _isPaused = false;

        public GamePage()
        {
            InitializeComponent();
            Loaded += GamePage_Loaded;
            Unloaded += GamePage_Unloaded;
        }
        private void GamePage_Loaded(object sender, RoutedEventArgs e)
        {
            _game = new BlackjackGame();
            _input = new InputController(_game);

            var cw = Window.Current.CoreWindow;

            cw.KeyDown += OnCoreKeyDown;
            //cw.PointerPressed += OnCorePointerPressed;

            _game.StartSession();

            RefreshUI();
        }
        private void GamePage_Unloaded(object sender, RoutedEventArgs e)
        {
            var cw = Window.Current.CoreWindow;
            cw.KeyDown -= OnCoreKeyDown;
            //cw.PointerPressed -= OnCorePointerPressed;
        }
        private void OnCoreKeyDown(CoreWindow sender, KeyEventArgs args)
        {
            _input?.OnKeyDown(sender, args);
            RefreshUI();
        }
        private void RefreshUI()
        {
            Root.Children.Clear();

            Root.Background = new SolidColorBrush(Color.FromArgb(255, 12, 90, 40));

            var layout = new Grid();
            layout.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // hud
            layout.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // dealer
            layout.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) }); // spacer
            layout.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // player
            layout.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // actions

            var hudRow = new Grid { Margin = new Thickness(12, 12, 12, 6) };
            hudRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            hudRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            hudRow.Children.Add(BuildHudPanel());
            var pause = BuildPauseButton();
            Grid.SetColumn((FrameworkElement)pause, 1);
            hudRow.Children.Add(pause);

            Grid.SetRow(hudRow, 0);
            layout.Children.Add(hudRow);

            // dealer hand
            var dealer = BuildDealerArea();
            Grid.SetRow((FrameworkElement)dealer, 1);
            layout.Children.Add(dealer);

            // player hand
            var payer = BuildPlayerArea();
            Grid.SetRow((FrameworkElement)payer, 3);
            layout.Children.Add(payer);

            // actions
            var bottomRow = new Grid { Margin = new Thickness(12, 6, 12, 12) };
            bottomRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            bottomRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            bottomRow.Children.Add(BuildActionBar());
            var betPanel = BuildBetPanel();
            Grid.SetColumn((FrameworkElement)betPanel, 1);
            bottomRow.Children.Add(betPanel);

            Grid.SetRow(bottomRow, 4);
            layout.Children.Add(bottomRow);

            Root.Children.Add(layout);

            if (_isPaused)
            {
                var overlay = new Grid
                {
                    Background = new SolidColorBrush(Color.FromArgb(180, 0, 0, 0)),
                    HorizontalAlignment = HorizontalAlignment.Stretch,
                    VerticalAlignment = VerticalAlignment.Stretch
                };

                var panel = new StackPanel
                {
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center,
                    Spacing = 12,
                };

                panel.Children.Add(MakeText("Paused", 32, Colors.White, bold: true));
                panel.Children.Add(MakeAction("Resume", () =>
                {
                    _isPaused = false;
                    RefreshUI();
                }, enabled: true, width: 120));
                panel.Children.Add(MakeAction("Quit", () =>
                {
                    Application.Current.Exit();
                }, enabled: true, width: 120));

                overlay.Children.Add(panel);
                Root.Children.Add(overlay);
            }
        }
        private UIElement BuildHudPanel()
        {
            var panel = new StackPanel
            {
                Orientation = Orientation.Vertical,
                Background = new SolidColorBrush(Color.FromArgb(128, 0, 0, 0)),
                Padding = new Thickness(12),
                Margin = new Thickness(0, 0, 20, 0),
                CornerRadius = new CornerRadius(8)
            };

            panel.Children.Add(MakeText($"Blackjack", 24, Colors.White, bold: true));
            panel.Children.Add(MakeRow("Bankroll", $"${_game.Bankroll:N0}"));
            panel.Children.Add(MakeRow("Bet", $"${_game.Bet:N0}"));
            panel.Children.Add(MakeRow("Phase", $"{_game.Phase}"));

            var hint = MakeText("Keys: N=new • H=hit • S=stand • +/- bet", 12, Colors.LightGray);
            hint.Margin = new Thickness(0, 6, 0, 0);
            panel.Children.Add(hint);

            return panel;
        }

        private UIElement BuildPauseButton()
        {
            var btn = new Button
            {
                Content = "Pause",
                HorizontalAlignment = HorizontalAlignment.Right,
                Padding = new Thickness(12, 6, 12, 6),
                Background = new SolidColorBrush(Color.FromArgb(180, 45, 45, 45)),
                Foreground = new SolidColorBrush(Colors.White),
                BorderBrush = new SolidColorBrush(Color.FromArgb(200, 255, 255, 255)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(6)
            };
            btn.Click += PauseButton_Click;
            return btn;
        }

        private UIElement BuildDealerArea()
        {
            var stack = new StackPanel
            {
                Orientation = Orientation.Vertical,
                HorizontalAlignment = HorizontalAlignment.Center
            };
            var val = _game.Dealer?.Value ?? 0;
            var lbl = MakeText($"Dealer ({val})", 18, Colors.White, bold: true);
            lbl.Margin = new Thickness(0, 6, 0, 6);
            stack.Children.Add(lbl);

            bool hideHole = _game.Phase == Phase.PlayerTurn;
            stack.Children.Add(BuildHandFromGame(_game.Dealer, hideHole));

            return stack;
        }

        private UIElement BuildPlayerArea()
        {
            var stack = new StackPanel
            {
                Orientation = Orientation.Vertical,
                HorizontalAlignment = HorizontalAlignment.Center
            };

            var val = _game.Player?.Value ?? 0;
            var lbl = MakeText($"Player ({val})", 18, Colors.White, bold: true);
            lbl.Margin = new Thickness(0, 6, 0, 6);
            stack.Children.Add(lbl);

            stack.Children.Add(BuildHandFromGame(_game.Player, hideHole: false));

            if (_game.Phase == Phase.RoundOver && !string.IsNullOrEmpty(_game.LastOutcome))
            {
                var outcome = MakeText(_game.LastOutcome, 24, Colors.Gold, bold: true);
                outcome.Margin = new Thickness(0, 8, 0, 0);
                stack.Children.Add(outcome);
            }

            return stack;
        }

        private UIElement BuildActionBar()
        {
            var wrap = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Center,
                Spacing = 8
            };

            if (_game.Phase == Phase.Betting)
                wrap.Children.Add(MakeAction("Deal", () => _game.NewRound(), enabled: true));
            else if (_game.Phase == Phase.RoundOver)
                wrap.Children.Add(MakeAction("Next", () => _game.NextHand(), enabled: true));
            else
                wrap.Children.Add(MakeAction("Deal", () => { }, enabled: false));

            bool canAct = _game.Phase == Phase.PlayerTurn;
            wrap.Children.Add(MakeAction("Hit", () => { if (canAct) _game.Hit(); }, enabled: canAct));
            wrap.Children.Add(MakeAction("Stand", () => { if (canAct) _game.Stand(); }, enabled: canAct));

            return wrap;
        }

        private UIElement BuildBetPanel()
        {
            var panel = new StackPanel
            {
                Orientation = Orientation.Vertical,
                Background = new SolidColorBrush(Color.FromArgb(140, 0, 0, 0)),
                Padding = new Thickness(12),
                CornerRadius = new CornerRadius(8),
                HorizontalAlignment = HorizontalAlignment.Right
            };

            panel.Children.Add(MakeText("Bet", 16, Colors.White, bold: true));
            panel.Children.Add(MakeText($"${_game.Bet:N0}", 20, Colors.White));

            var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, Margin = new Thickness(0, 8, 0, 0) };
            row.Children.Add(MakeAction("−", () =>
            {
                _game.AdjustBet(-5);
            }, small: true));
            row.Children.Add(MakeAction("+", () =>
            {
                _game.AdjustBet(+5);
            }, small: true));

            panel.Children.Add(row);

            var tip = MakeText("Tip: use +/- keys", 12, Colors.LightGray);
            tip.Margin = new Thickness(0, 6, 0, 0);
            panel.Children.Add(tip);

            return panel;
        }

        private TextBlock MakeText(string t, double size, Color color, bool bold = false)
        {
            return new TextBlock
            {
                Text = t,
                Foreground = new SolidColorBrush(color),
                FontSize = size,
                FontWeight = bold ? Windows.UI.Text.FontWeights.SemiBold : Windows.UI.Text.FontWeights.Normal
            };
        }

        private Grid MakeRow(string label, string value)
        {
            var g = new Grid();
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var l = MakeText(label, 16, Colors.WhiteSmoke);
            var v = MakeText(value, 18, Colors.White, bold: true);

            g.Children.Add(l);
            Grid.SetColumn(v, 2);
            g.Children.Add(v);

            return g;
        }

        private Button MakeAction(string caption, Action onClick, bool small = false, bool enabled = true, double? width = null)
        {
            var b = new Button
            {
                Content = caption,
                IsEnabled = enabled,
                Padding = small ? new Thickness(10, 6, 10, 6) : new Thickness(16, 10, 16, 10),
                Margin = new Thickness(2),
                Background = new SolidColorBrush(Color.FromArgb(200, 50, 50, 50)),
                Foreground = new SolidColorBrush(Colors.White),
                BorderBrush = new SolidColorBrush(Color.FromArgb(200, 255, 255, 255)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(6)
            };
            if (width.HasValue) b.Width = width.Value;
            b.Click += (_, __) => { onClick(); RefreshUI(); };
            return b;
        }

        private StackPanel BuildHandFromGame(Hand hand, bool hideHole)
        {
            List<string> backPaths = new() { "backs/back_default.png", "backs/back_red.png" };

            var panel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = 8,
                HorizontalAlignment = HorizontalAlignment.Center
            };

            for (int i = 0; i < hand.Cards.Count; i++)
            {
                var isHole = hideHole && i == 1;
                if (isHole)
                {
                    panel.Children.Add(MakeCardImage(backPaths[1], CARD_WIDTH, CARD_HEIGHT));
                }
                else
                {
                    var code = FaceCode(hand.Cards[i]); // e.g. "AS"
                    panel.Children.Add(MakeCardImage($"faces/{code}.png", CARD_WIDTH, CARD_HEIGHT));
                }
            }
            return panel;
        }

        private FrameworkElement MakeCardImage(string relativePath, double w, double h)
        {
            var uri = new Uri($"ms-appx:///Assets/Cards/{relativePath}");
            var img = new Image
            {
                Width = w,
                Height = h,
                Stretch = Stretch.Uniform,
                Source = new BitmapImage(uri)
            };
            img.ImageFailed += (s, e) =>
                System.Diagnostics.Debug.WriteLine($"ImageFailed: {uri} -> {e.ErrorMessage}");
            return img;
        }

        private static string FaceCode(Card c) => RankCode(c.Rank) + SuitCode(c.Suit);

        private static string RankCode(int r) => r switch
        {
            1 => "A",
            10 => "T",
            11 => "J",
            12 => "Q",
            13 => "K",
            _ => r.ToString()
        };

        private static string SuitCode(Suit s) => s switch
        {
            Suit.Clubs => "C",
            Suit.Diamonds => "D",
            Suit.Hearts => "H",
            _ => "S"
        };

        private void PauseButton_Click(object sender, RoutedEventArgs e)
        {
            _isPaused = !_isPaused;
            RefreshUI();
        }
    }
}