using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UWPBlackjack.Core;
using UWPBlackjack.Game;
using Windows.UI;
using Windows.UI.Core;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Media.Imaging;
using Windows.Media.Playback;
using Windows.Media.Core;
using System.Diagnostics;
using System.ComponentModel.DataAnnotations;

//Project: Lab 1A - UWP Game
//Student Name: Andrew Dionne
//Date: 2025/09/27


namespace UWPBlackjack
{
    public sealed partial class GamePage : Page
    {
        private BlackjackGame _game;
        private InputController _input;

        private const double CARD_WIDTH = 100;
        private const double CARD_HEIGHT = 150;

        private bool _isPaused = false;
        private bool _isDealingAnimationInProgress = false;
        private bool _isDealerAnimationInProgress = false;

        public List<BackOption> _backOptions = [];

        private bool _shopOpen = false;
        private List<string> _ownedBacks = new List<string> { "default" };
        private string currentBack = "default";

        private MediaPlayer _bgmPlayer;
        private static readonly Random _rng = new Random();

        public GamePage()
        {
            InitializeComponent();
            Loaded += GamePage_Loaded;
            Unloaded += GamePage_Unloaded;
        }
        #region Events 
        private void GamePage_Loaded(object sender, RoutedEventArgs e)
        {
            _game = new BlackjackGame();
            _input = new InputController(_game);

            InitializeCardBacks();
            PlayBackgroundMusic();

            var cw = Window.Current.CoreWindow;
            cw.KeyDown += OnCoreKeyDown;

            _game.StartSession();

            RefreshUI();
        }
        private void GamePage_Unloaded(object sender, RoutedEventArgs e)
        {
            var cw = Window.Current.CoreWindow;
            cw.KeyDown -= OnCoreKeyDown;
            _bgmPlayer?.Dispose();
            _bgmPlayer = null;
        }
        private void OnCoreKeyDown(CoreWindow sender, KeyEventArgs args)
        {
            if (args.VirtualKey == Windows.System.VirtualKey.Escape) 
                _isPaused = !_isPaused;
            _input?.OnKeyDown(sender, args);
            RefreshUI();
        }
        private void PauseButton_Click(object sender, RoutedEventArgs e)
        {
            _isPaused = !_isPaused;
            RefreshUI();
        }
        private void ShopButton_Click(object sender, RoutedEventArgs e)
        {
            _shopOpen = !_shopOpen;
            RefreshUI();
        }
        #endregion

        #region UI Rendering and Animation
        /// <summary>
        /// Method which rebuilds entire game screen so it always matches game's state
        /// Gets triggered after user input or game events
        /// </summary>
        private void RefreshUI()
        {
            Root.Children.Clear();

            Root.Background = new SolidColorBrush(Color.FromArgb(255, 12, 90, 40)); // dark green background (felt)

            Grid layout = new();
            layout.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // hud
            layout.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // dealer
            layout.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) }); // spacer
            layout.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // player
            layout.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // actions

            Grid hudRow = new() { Margin = new Thickness(12, 12, 12, 6) };
            hudRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            hudRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            hudRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            hudRow.Children.Add(BuildHudPanel());
            Button shop = BuildShopButton();
            Button pause = BuildPauseButton();
            Grid.SetColumn(shop, 1);
            Grid.SetColumn(pause, 2);
            hudRow.Children.Add(pause);
            hudRow.Children.Add(shop);

            Grid.SetRow(hudRow, 0);
            layout.Children.Add(hudRow);

            // if game is in dealing phase, execute animation task with a discard
            if (_game.Phase == Phase.Dealing && !_isDealingAnimationInProgress)
            {
                _ = AnimateInitialDealAsync();
            }

            if (_game.Phase == Phase.DealerTurn && !_isDealerAnimationInProgress)
            {
                _ = AnimateDealerTurnAsync();
            }

            // only show dealer and player hands if not in Betting phase
            if (_game.Phase != Phase.Betting)
            {
                // dealer hand
                StackPanel dealer = BuildDealerArea();
                Grid.SetRow(dealer, 1);
                layout.Children.Add(dealer);

                // player hand
                StackPanel payer = BuildPlayerArea();
                Grid.SetRow(payer, 3);
                layout.Children.Add(payer);
            }

            // actions
            Grid bottomRow = new() { Margin = new Thickness(12, 6, 12, 12) };
            bottomRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            bottomRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            // bet panel
            // Build action bar and center it across BOTH columns
            var actions = BuildActionBar();
            actions.HorizontalAlignment = HorizontalAlignment.Center;
            Grid.SetColumn(actions, 0);
            Grid.SetColumnSpan(actions, 2);                   
            bottomRow.Children.Add(actions);

            var betPanel = BuildBetPanel();
            Grid.SetColumn(betPanel, 1);
            betPanel.HorizontalAlignment = HorizontalAlignment.Right;
            bottomRow.Children.Add(betPanel);

            Grid.SetRow(bottomRow, 4);
            layout.Children.Add(bottomRow);

            Root.Children.Add(layout);

            // if flag _isPaused is true then build overlay (doesn't currently affect actual game state, it is purely visual)
            if (_isPaused)
            {
                var pauseOverlay = BuildPauseOverlay();
                Canvas.SetZIndex(pauseOverlay, 1000);
                Root.Children.Add(pauseOverlay);
            }

            if (_shopOpen)
            {
                Grid shopOverlay = BuildBackShopOverlay();
                Canvas.SetZIndex(shopOverlay, 1001);
                Root.Children.Add(shopOverlay);
            }

            // betting phase
            if (_game.Phase == Phase.Betting && !_isPaused)
            {
                TextBlock tip = MakeText("Place your bet", 40, Colors.Yellow, bold: true, centered: true);
                tip.Margin = new Thickness(0, 0, 0, 20);
                tip.HorizontalAlignment = HorizontalAlignment.Center;
                tip.VerticalAlignment = VerticalAlignment.Center;
                Root.Children.Add(tip);
            }

            // if game is over ask user to replay (resets bankroll to 1000)
            if (_game.Phase == Phase.RoundOver && _game.Bankroll <= 0)
            {
                Grid gameOverOverlay = BuildGameOverOverlay();
                Root.Children.Add(gameOverOverlay);
            }
        }
        /// <summary>
        /// This method performs initial draw of cards 
        /// Order goes player -> dealer -> player -> dealer
        /// </summary>
        private async Task AnimateInitialDealAsync()
        {
            // safety check to prevent overlapping
            if (_isDealingAnimationInProgress) 
                return;

            _isDealingAnimationInProgress = true;

            // draws 2 cards for player and 2 cards for dealer with slight delay between each draw for visual sake
            // refreshing the ui between each draw of card before Task.Delay so we can see cards as they are drawn
            for (int i = 0; i < 2; i++)
            {
                _game.Player.Add(_game.Deck.Draw());
                RefreshUI();
                PlaySoundEffect("Assets/Sfx/card_flip_1.mp3");
                await Task.Delay(500);

                _game.Dealer.Add(_game.Deck.Draw());
                PlaySoundEffect("Assets/Sfx/card_flip_2.mp3");
                RefreshUI();

                await Task.Delay(500);
            }

            // player gets first turn after draw
            _game.Phase = Phase.PlayerTurn;

            // check for immediate blackjack - natural 21
            if (_game.Player.IsBlackjack || _game.Dealer.IsBlackjack)
            {
                _game.Phase = Phase.RoundOver;
                int pv = _game.Player.Value;
                int dv = _game.Dealer.Value;
                if (_game.Player.IsBlackjack && !_game.Dealer.IsBlackjack)
                {
                    _game.LastPayout = (int)Math.Round(_game.Bet * 1.5);
                    _game.Bankroll += _game.LastPayout;
                    _game.LastOutcome = "Blackjack! You win 3:2";
                }
                else if (_game.Dealer.IsBlackjack && !_game.Player.IsBlackjack)
                {
                    _game.LastPayout = -_game.Bet;
                    _game.Bankroll += _game.LastPayout;
                    _game.LastOutcome = "Dealer blackjack — you lose";
                }
                else if (_game.Dealer.IsBlackjack && _game.Player.IsBlackjack)
                {
                    _game.LastPayout = 0;
                    _game.LastOutcome = "Push";
                }
            }

            _isDealingAnimationInProgress = false;
            RefreshUI();
        }
        private async Task AnimateDealerTurnAsync()
        {
            if (_isDealerAnimationInProgress) return;
            _isDealerAnimationInProgress = true;

            RefreshUI();
            await Task.Delay(600);

            while (_game.DealerShouldHit)
            {
                _game.DealerHitOne();
                PlaySoundEffect("Assets/Sfx/card_flip_2.mp3");

                RefreshUI();
                await Task.Delay(500);

                while (_isPaused)
                    await Task.Delay(100);
            }

            await Task.Delay(400);
            _game.FinishDealer();

            _isDealerAnimationInProgress = false;
            RefreshUI();
        }
        private async void ShowUnlockDialog(BackOption opt)
        {
            var dialog = new ContentDialog
            {
                Title = "Unlocked!",
                Content = $"You unlocked: {opt.DisplayName}",
                CloseButtonText = "OK"
            };

            await dialog.ShowAsync();
        }        
        #endregion

        #region Card Asset Helpers
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
        private void InitializeCardBacks()
        {
            _backOptions.Add(new BackOption { Key = "default", DisplayName = "Classic", AssetFile = "backs/back_default.png", Cost = 0 });
            _backOptions.Add(new BackOption { Key = "red", DisplayName = "Ruby Red", AssetFile = "backs/back_red.png", Cost = 250 });
            _backOptions.Add(new BackOption { Key = "orange", DisplayName = "Sunset Orange", AssetFile = "backs/back_orange.png", Cost = 1_000 });
            _backOptions.Add(new BackOption { Key = "purple", DisplayName = "Royal Purple", AssetFile = "backs/back_purple.png", Cost = 1_500 });
        }
        #endregion

        #region UI Factories
        private StackPanel BuildHudPanel()
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
            //panel.Children.Add(MakeRow("Phase", $"{_game.Phase}"));

            var hint = MakeText("Keys: N=new • H=hit • S=stand • ↑/↓ bet", 12, Colors.LightGray);
            hint.Margin = new Thickness(0, 6, 0, 0);
            panel.Children.Add(hint);

            return panel;
        }
        private Button BuildPauseButton()
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
        private Button BuildShopButton()
        {
            var shopBtn = new Button
            {
                Content = "Shop",
                Padding = new Thickness(12, 6, 12, 6),
                Background = new SolidColorBrush(Color.FromArgb(180, 45, 45, 45)),
                Foreground = new SolidColorBrush(Colors.White),
                BorderBrush = new SolidColorBrush(Color.FromArgb(200, 255, 255, 255)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(6),
                Margin = new Thickness(8, 0, 8, 0)
            };
            shopBtn.Click += ShopButton_Click;
            return shopBtn;
        }
        private StackPanel BuildDealerArea()
        {
            var stack = new StackPanel
            {
                Orientation = Orientation.Vertical,
                HorizontalAlignment = HorizontalAlignment.Center
            };

            bool hideHole = _game.Phase == Phase.PlayerTurn || _game.Phase == Phase.Dealing;

            TextBlock lbl;
            if (hideHole)
            {
                if (_game.Dealer.Cards.Count > 0)
                {
                    string up = _game.Dealer.Cards[0].DisplayValue;
                    lbl = MakeText($"Dealer ({up})", 18, Colors.White, bold: true);
                }
                else
                {
                    lbl = MakeText($"Dealer", 18, Colors.White, bold: true);
                }
            }
            else
            {
                var val = _game.Dealer?.Value ?? 0;
                lbl = MakeText($"Dealer ({val})", 18, Colors.White, bold: true);
            }
            lbl.Margin = new Thickness(0, 6, 0, 6);
            stack.Children.Add(lbl);

            stack.Children.Add(BuildHandFromGame(_game.Dealer, hideHole));
            return stack;
        }
        private StackPanel BuildPlayerArea()
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
        private StackPanel BuildActionBar()
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
            wrap.Children.Add(MakeAction("Hit", () => { if (canAct) { _game.Hit(); PlaySoundEffect("Assets/Sfx/card_flip_1.mp3"); } }, enabled: canAct));
            wrap.Children.Add(MakeAction("Stand", () => { if (canAct) _game.Stand(); PlaySoundEffect("Assets/Sfx/card_flip_2.mp3"); }, enabled: canAct));

            return wrap;
        }
        private StackPanel BuildBetPanel()
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

            var tip = MakeText("Tip: use ↑/↓ keys", 12, Colors.LightGray);
            tip.Margin = new Thickness(0, 6, 0, 0);
            panel.Children.Add(tip);

            return panel;
        }
        private Grid BuildPauseOverlay()
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

            panel.Children.Add(MakeText("Paused", 32, Colors.White, bold: true, centered: true));
            panel.Children.Add(MakeAction("Resume", () =>
            {
                _isPaused = false;
            }, enabled: true, width: 180));
            panel.Children.Add(MakeAction("Quit", () =>
            {
                Application.Current.Exit();
            }, enabled: true, width: 180));

            overlay.Children.Add(panel);
            return overlay;
        }
        private Grid BuildBackShopOverlay()
        {
            var overlay = new Grid
            {
                Background = new SolidColorBrush(Color.FromArgb(220, 0, 0, 0)),
                HorizontalAlignment = HorizontalAlignment.Stretch,
                VerticalAlignment = VerticalAlignment.Stretch
            };

            var panel = new StackPanel
            {
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Spacing = 12,
                Width = 520
            };

            panel.Children.Add(MakeText("Card Backs", 32, Colors.White, bold: true, centered: true));
            panel.Children.Add(MakeText($"Bankroll: ${_game.Bankroll:N0}", 18, Colors.Gold, bold: true, centered: true));

            // simple grid of options
            var itemsWrap = new WrapGrid
            {
                Orientation = Orientation.Horizontal,
                MaximumRowsOrColumns = 2
            };

            foreach (var opt in _backOptions)
                itemsWrap.Children.Add(MakeBackTile(opt));


            var itemsHost = new Grid { Margin = new Thickness(0, 8, 0, 8) };

            // 2 columns
            itemsHost.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            itemsHost.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            // how many rows we need for 2 columns
            int cols = 2;
            int rows = (_backOptions.Count + cols - 1) / cols;
            for (int r = 0; r < rows; r++)
            {
                itemsHost.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            }

            // add tiles
            for (int i = 0; i < _backOptions.Count; i++)
            {
                int r = i / cols;
                int c = i % cols;

                var tile = MakeBackTile(_backOptions[i]);
                Grid.SetRow(tile, r);
                Grid.SetColumn(tile, c);
                itemsHost.Children.Add(tile);
            }

            panel.Children.Add(itemsHost);

            // Close button
            panel.Children.Add(MakeAction("Close", () =>
            {
                _shopOpen = false;
            }, enabled: true, stretchWidth: true));

            overlay.Children.Add(panel);
            return overlay;
        }
        private Grid MakeBackTile(BackOption opt)
        {
            var g = new Grid
            {
                Width = 240,
                Height = 280,
                Margin = new Thickness(8),
                Background = new SolidColorBrush(Color.FromArgb(140, 20, 20, 20))
            };

            var tile = new StackPanel { Spacing = 6, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };

            // Preview image
            var img = MakeCardImage(opt.AssetFile, 100, 160);
            tile.Children.Add(img);

            // Name + price/status
            var name = MakeText(opt.DisplayName, 16, Colors.White, bold: true, centered: true);
            tile.Children.Add(name);

            bool owned = _ownedBacks.Contains(opt.Key);
            bool equipped = currentBack == opt.Key;

            string status = owned ? (equipped ? "Equipped" : "Owned") : $"Price: ${opt.Cost:N0}";
            var statusText = MakeText(status, 14, owned ? Colors.LightGreen : Colors.LightGray, centered: true);
            tile.Children.Add(statusText);

            // Action button
            Button actionBtn;

            if (!owned)
            {
                bool canBuy = _game.Bankroll >= opt.Cost;
                actionBtn = MakeAction("Buy", () =>
                {
                    _game.Bankroll -= opt.Cost;
                    _ownedBacks.Add(opt.Key);
                    currentBack = opt.Key;
                    ShowUnlockDialog(opt);
                }, enabled: canBuy, width: 120);
            }
            else
            {
                actionBtn = MakeAction(equipped ? "Equipped" : "Equip", () =>
                {
                    currentBack = opt.Key;
                }, enabled: !equipped, width: 120);
            }

            actionBtn.HorizontalAlignment = HorizontalAlignment.Center;
            tile.Children.Add(actionBtn);

            g.Children.Add(tile);
            return g;
        }
        private Grid BuildGameOverOverlay()
        {
            var overlay = new Grid
            {
                Background = new SolidColorBrush(Color.FromArgb(220, 0, 0, 0)),
                HorizontalAlignment = HorizontalAlignment.Stretch,
                VerticalAlignment = VerticalAlignment.Stretch
            };
            var panel = new StackPanel
            {
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Spacing = 12,
            };
            panel.Children.Add(MakeText("Game Over", 32, Colors.Red, bold: true, centered: true));
            panel.Children.Add(MakeText("You have run out of funds.", 18, Colors.White, centered: true));
            panel.Children.Add(MakeAction("Restart", () =>
            {
                _game.StartSession();
                _isPaused = false;
                _ownedBacks = new List<string> { "default" };
            }, enabled: true, stretchWidth: true));
            panel.Children.Add(MakeAction("Quit", () =>
            {
                Application.Current.Exit();
            }, enabled: true, stretchWidth: true));
            overlay.Children.Add(panel);
            return overlay;
        }
        private TextBlock MakeText(string t, double size, Color color, bool bold = false, bool centered = false)
        {
            return new TextBlock
            {
                Text = t,
                Foreground = new SolidColorBrush(color),
                FontSize = size,
                FontWeight = bold ? Windows.UI.Text.FontWeights.SemiBold : Windows.UI.Text.FontWeights.Normal,
                TextAlignment = centered ? TextAlignment.Center : TextAlignment.Left,
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
        private Button MakeAction(string caption, Action onClick, bool small = false, bool enabled = true, double? width = null, bool stretchWidth = false)
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
            if (stretchWidth)
            {
                b.HorizontalAlignment = HorizontalAlignment.Stretch;
            }
            b.Click += (_, __) => { onClick(); RefreshUI(); };
            return b;
        }
        private StackPanel BuildHandFromGame(Hand hand, bool hideHole)
        {
            string backAsset = _backOptions?.Find(b => b.Key == currentBack)?.AssetFile
                               ?? "backs/back_default.png";

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
                    panel.Children.Add(MakeCardImage(backAsset, CARD_WIDTH, CARD_HEIGHT));
                }
                else
                {
                    var code = FaceCode(hand.Cards[i]); 
                    panel.Children.Add(MakeCardImage($"faces/{code}.png", CARD_WIDTH, CARD_HEIGHT));
                }
            }
            return panel;
        }
        private Image MakeCardImage(string relativePath, double w, double h)
        {
            Uri uri = new Uri($"ms-appx:///Assets/Cards/{relativePath}");
            Image img = new Image
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
        #endregion

        #region Audio & Audio Helpers
        private void PlayBackgroundMusic()
        {
            try
            {
                if (_bgmPlayer == null)
                {
                    _bgmPlayer = new MediaPlayer
                    {
                        IsLoopingEnabled = true,
                        Volume = 0.05
                    };
                }

                var uri = new Uri("ms-appx:///Assets/Music/default.mp3");
                _bgmPlayer.Source = MediaSource.CreateFromUri(uri);
                _bgmPlayer.Play();
            }
            catch (Exception ex)
            {
                Debug.WriteLine("BGM error: " + ex.Message);
            }
        }
        private void PlaySoundEffect(string packageRelativePath)
        {
            try
            {
                var uri = new Uri($"ms-appx:///{packageRelativePath}"); 
                var player = new MediaPlayer { AutoPlay = true, Volume = 0.1 };
                player.Source = MediaSource.CreateFromUri(uri);
                player.MediaEnded += (s, e) => player.Dispose();
                player.Play();
            }
            catch (Exception ex)
            {
                Debug.WriteLine("SFX error: " + ex.Message);
            }
        }

        #endregion
    }
}