using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;
using UWPBlackjack.Core;
using UWPBlackjack.Game;
using UWPBlackjack.UI;
using Windows.Media.Core;
using Windows.Media.Playback;
using Windows.UI;
using Windows.UI.Core;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Media.Imaging;

//Project: Lab 1A - UWP Game
//Student Name: Andrew Dionne
//Date: 2025/10/03

/*
Note: My collectibles of choice are owned backs and owned felts which you can unlock for $ in the shop
*/

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

        private List<BackOption> _backOptions = new List<BackOption>();
        private List<FeltOption> _feltOptions = new List<FeltOption>();

        private bool _shopOpen = false;
        private List<string> _ownedBacks = new List<string> { "default" };
        private string currentBack = "default";

        private List<string> _ownedFeltColors = new List<string> { "default" };
        private Color currentColor = Color.FromArgb(255, 12, 90, 40);

        private MediaPlayer _bgmPlayer;
        private static readonly Random _rng = new Random();

        bool _backgroundMusicPlaying = false;
        private MediaPlayer _sfxPlayer;
        private MediaSource _flip1Src;
        private MediaSource _flip2Src;

        private ShopTab _shopTab = ShopTab.Backs;

        private bool _doubling = false;

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

            // initialize sfx player and store media source to commonly used sounds
            _sfxPlayer = new MediaPlayer
            {
                AutoPlay = false,
                Volume = 0.6,
                AudioCategory = MediaPlayerAudioCategory.SoundEffects
            };
            _sfxPlayer.CommandManager.IsEnabled = false; 
            _flip1Src = MediaSource.CreateFromUri(new Uri("ms-appx:///Assets/Sfx/card_flip_1.mp3"));
            _flip2Src = MediaSource.CreateFromUri(new Uri("ms-appx:///Assets/Sfx/card_flip_2.mp3"));


            InitializeCardBacks();
            InitializeFeltColors();
            PlayBackgroundMusic(_backgroundMusicPlaying);

            var cw = Window.Current.CoreWindow;
            cw.KeyDown += OnCoreKeyDown;

            _game.StartSession();

            RefreshUI();
        }
        private void GamePage_Unloaded(object sender, RoutedEventArgs e)
        {
            var cw = Window.Current.CoreWindow;
            cw.KeyDown -= OnCoreKeyDown;
            _sfxPlayer?.Dispose();
            _flip1Src?.Dispose();
            _flip2Src?.Dispose();
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

            Root.Background = new SolidColorBrush(currentColor);

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
            hudRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            hudRow.Children.Add(BuildHudPanel());
            Button shop = BuildShopButton();
            Button pause = BuildPauseButton();
            Button music = BuildMusicButton();

            Grid.SetColumn(shop, 1);
            Grid.SetColumn(pause, 2);
            Grid.SetColumn(music, 3);

            hudRow.Children.Add(pause);
            hudRow.Children.Add(shop);
            hudRow.Children.Add(music);

            Grid.SetRow(hudRow, 0);
            layout.Children.Add(hudRow);

            // if game is in dealing phase, execute animation task with a discard
            if (_game.Phase == Phase.Dealing && !_isDealingAnimationInProgress)
            {
                _ = AnimateInitialDealAsync();
            }

            // if doubling flag is true then animate dealer turn
            if (_doubling)
            {
                _doubling = false;
                _ = AnimateDealerTurnAsync();
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

            // if flag _isPaused is true then build pause overlay 
            if (_isPaused)
            {
                var pauseOverlay = BuildPauseOverlay();
                Canvas.SetZIndex(pauseOverlay, 1000);
                Root.Children.Add(pauseOverlay);
            }

            if (_shopOpen)
            {
                Grid shopOverlay = BuildShopOverlay();
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

            // if game is over show game over overlay
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

            // draws 2 cards for player and 2 cards for dealer with 500ms delay between each draw for visual sake
            // refreshing the ui between each draw of card before Task.Delay so we can see cards as they are drawn
            for (int i = 0; i < 2; i++)
            {
                _game.Player.Add(_game.Deck.Draw());
                RefreshUI();
                PlaySoundEffect(_flip1Src);
                await Task.Delay(500);

                _game.Dealer.Add(_game.Deck.Draw());
                PlaySoundEffect(_flip2Src);
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
        /// <summary>
        /// This method is used after player stands
        /// Animates the dealers drawing of their cards with a slight delay and sound effect
        /// </summary>
        /// <returns></returns>
        private async Task AnimateDealerTurnAsync()
        {
            if (_isDealerAnimationInProgress) return;
            _isDealerAnimationInProgress = true;

            RefreshUI();
            await Task.Delay(600);

            // while the dealers value is less than 17
            while (_game.DealerShouldHit)
            {
                // hit one > play sound effect > refresh the ui > wait 500ms > repeat 
                _game.DealerHitOne();
                PlaySoundEffect(_flip2Src);

                RefreshUI();
                await Task.Delay(500);

                // wait until game is not paused
                while (_isPaused)
                    await Task.Delay(100);
            }

            await Task.Delay(400);
            _game.FinishDealer(); // settles game, checks values, and changes game phase

            _isDealerAnimationInProgress = false;
            RefreshUI();
        }
        /// <summary>
        /// Small content dialog displaying what the user unlocked
        /// </summary>
        /// <param name="opt"></param>
        private async void ShowUnlockDialog(string opt)
        {
            var dialog = new ContentDialog
            {
                Title = "Unlocked!",
                Content = $"You unlocked: {opt}",
                CloseButtonText = "OK"
            };

            await dialog.ShowAsync();
        }        
        #endregion

        #region Card Asset Helpers
        private static string FaceCode(Card c) => RankCode(c.Rank) + SuitCode(c.Suit); /// Face code is rank, then suit, for example 2S, 2C, 6D, ... to help grabbing assets
        private static string RankCode(int r)
        {
            switch (r)
            {
                case 1: return "A";
                case 10: return "T";
                case 11: return "J";
                case 12: return "Q";
                case 13: return "K";
                default: return r.ToString();
            }
        }

        private static string SuitCode(Suit s)
        {
            switch (s)
            {
                case Suit.Clubs: return "C";
                case Suit.Diamonds: return "D";
                case Suit.Hearts: return "H";
                default: return "S";
            }
        }
        private void InitializeCardBacks()
        {
            _backOptions.Add(new BackOption { Key = "default", DisplayName = "Classic", AssetFile = "backs/back_default.png", Cost = 0 });
            _backOptions.Add(new BackOption { Key = "red", DisplayName = "Ruby Red", AssetFile = "backs/back_red.png", Cost = 250 });
            _backOptions.Add(new BackOption { Key = "orange", DisplayName = "Sunset Orange", AssetFile = "backs/back_orange.png", Cost = 1_000 });
            _backOptions.Add(new BackOption { Key = "purple", DisplayName = "Royal Purple", AssetFile = "backs/back_purple.png", Cost = 1_500 });
        }
        private void InitializeFeltColors()
        {
            _feltOptions.Add(new FeltOption { Key = "default", DisplayName = "Classic Green", Color = Color.FromArgb(255, 12, 90, 40), Cost = 0} );
            _feltOptions.Add(new FeltOption { Key = "blue", DisplayName = "Deep Blue", Color = Color.FromArgb(255, 18, 40, 110), Cost = 250} );
            _feltOptions.Add(new FeltOption { Key = "burgundy", DisplayName = "Burgundy", Color = Color.FromArgb(255, 100, 20, 30), Cost = 250 });
            _feltOptions.Add(new FeltOption { Key = "charcoal", DisplayName = "Charcoal", Color = Color.FromArgb(255, 30, 30, 30), Cost = 250 });
        }
        #endregion

        #region UI Factories
        /// <summary>
        /// Builds out HUD panel (which is used at top of screen, showing title, bankroll, and bet value + tips for keys to press
        /// </summary>
        /// <returns></returns>
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
            panel.Children.Add(MakeRow("Highest Bankroll", $"${_game.HighestScore:N0}"));
            //panel.Children.Add(MakeRow("Phase", $"{_game.Phase}"));

            var hint = MakeText("Keys: N=new • H=hit • S=stand • ↑/↓ bet", 12, Colors.LightGray);
            hint.Margin = new Thickness(0, 6, 0, 0);
            panel.Children.Add(hint);

            return panel;
        }
        /// <summary>
        /// Builds out simple pause button, subscribes click event to method PauseButton_Click
        /// </summary>
        /// <returns></returns>
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
        /// <summary>
        /// Builds out simple shop button, subscribes click event to method ShopButton_Click
        /// </summary>
        /// <returns></returns>
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
        /// <summary>
        /// Builds out a simple music button with action to pause and play background music
        /// </summary>
        /// <returns>Button object</returns>
        private Button BuildMusicButton()
        {
            var btn = new Button
            {
                Content = _backgroundMusicPlaying ? "Music: On" : "Music: Off",
                Padding = new Thickness(12, 6, 12, 6),
                Background = new SolidColorBrush(Color.FromArgb(180, 45, 45, 45)),
                Foreground = new SolidColorBrush(Colors.White),
                BorderBrush = new SolidColorBrush(Color.FromArgb(200, 255, 255, 255)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(6),
                Margin = new Thickness(8, 0, 0, 0)
            };

            // click event, to pause and play music
            btn.Click += (s, e) =>
            {
                _backgroundMusicPlaying = !_backgroundMusicPlaying; // flip tracking variable
                if (_backgroundMusicPlaying)
                {
                    _bgmPlayer?.Play();
                }
                else
                {
                    _bgmPlayer?.Pause();
                }
                RefreshUI(); // after clicked, refresh ui
            };

            return btn;
        }

        /// <summary>
        /// Method which builds out dealer's area with dealer's value, and cards
        /// </summary>
        /// <returns></returns>
        private StackPanel BuildDealerArea()
        {
            var stack = new StackPanel
            {
                Orientation = Orientation.Vertical,
                HorizontalAlignment = HorizontalAlignment.Center
            };

            // when player's turn or phase is dealing, hide hole is true
            bool hideHole = _game.Phase == Phase.PlayerTurn || _game.Phase == Phase.Dealing;

            TextBlock lbl;

            // if hidehole is true, then only show value of dealer's first card
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
            // otherwise show full value
            else
            {
                var val = _game.Dealer?.Value ?? 0;
                lbl = MakeText($"Dealer ({val})", 18, Colors.White, bold: true);
            }
            lbl.Margin = new Thickness(0, 6, 0, 6);
            stack.Children.Add(lbl);

            // add dealer's cards to the stack under values
            if (_game.Dealer != null) stack.Children.Add(BuildHandFromGame(_game.Dealer, hideHole));
            return stack;
        }
        /// <summary>
        /// Builds players area with all players card images
        /// </summary>
        /// <returns>Vertical stackpanel showing player's are (value, card images, and small text showing outcome if round is over)</returns>
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

            if (_game.Player != null) 
                stack.Children.Add(BuildHandFromGame(_game.Player, hideHole: false));

            if (_game.Phase == Phase.RoundOver && !string.IsNullOrEmpty(_game.LastOutcome))
            {
                var outcome = MakeText(_game.LastOutcome, 24, Colors.Gold, bold: true, centered: true);
                outcome.Margin = new Thickness(0, 8, 0, 0);
                stack.Children.Add(outcome);
            }

            return stack;
        }
        /// <summary>
        /// Builds action bar which is used on bottom row of screen
        /// </summary>
        /// <returns>Horizontal stackpanel of bet panel</returns>
        private StackPanel BuildActionBar()
        {
            var wrap = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Center,
                Spacing = 8
            };

            // changes button from deal, to next depending on game state
            if (_game.Phase == Phase.Betting)
                wrap.Children.Add(MakeAction("Deal", () => _game.NewRound(), enabled: true));
            else if (_game.Phase == Phase.RoundOver)
                wrap.Children.Add(MakeAction("Next", () => _game.NextHand(), enabled: true));
            else
                wrap.Children.Add(MakeAction("Deal", () => { }, enabled: false));

            // hit and stand actions, only active if it is player's turn
            bool canAct = _game.Phase == Phase.PlayerTurn;

            bool canDouble = canAct && _game.Player.Cards.Count == 2 && (_game.Bankroll-_game.Bet) >= _game.Bet;

            wrap.Children.Add(MakeAction("Hit", () => { if (canAct) { _game.Hit(); PlaySoundEffect(_flip1Src); } }, enabled: canAct));
            wrap.Children.Add(MakeAction("Stand", () => { if (canAct) { _game.Stand(); PlaySoundEffect(_flip2Src); } }, enabled: canAct));
            wrap.Children.Add(MakeAction("Double", () => { if (canDouble) { _game.Double(); PlaySoundEffect(_flip1Src); _doubling = true; } }, enabled: canDouble));  

            return wrap;
        }
        /// <summary>
        /// Builds small bet panel
        /// </summary>
        /// <returns>Vertical stackpanel of bet panel</returns>
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
            // Adjust bet 
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
        /// <summary>
        /// Builds the pause overlay
        /// </summary>
        /// <returns>Stackpanel of pause overlay</returns>
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
        /// <summary>
        /// Overlay for shop, uses tab button, backs grid content, and color grid content
        /// </summary>
        /// <returns></returns>
        private Grid BuildShopOverlay()
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

            // tabs
            var tabsRow = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Center,
                Spacing = 6
            };
            tabsRow.Children.Add(MakeTabButton("Card Backs", ShopTab.Backs, _shopTab == ShopTab.Backs));
            tabsRow.Children.Add(MakeTabButton("Table Color", ShopTab.TableColor, _shopTab == ShopTab.TableColor));
            panel.Children.Add(tabsRow);

            // title + bankroll
            var title = _shopTab == ShopTab.Backs ? "Card Backs" : "Table Color";
            panel.Children.Add(MakeText(title, 32, Colors.White, bold: true, centered: true));
            panel.Children.Add(MakeText($"Bankroll: ${_game.Bankroll:N0}", 18, Colors.Gold, bold: true, centered: true));

            // tab content
            UIElement content = _shopTab == ShopTab.Backs
                ? BuildBacksGridContent()
                : BuildColorGridContent();
            panel.Children.Add(content);

            // close
            panel.Children.Add(MakeAction("Close", () => { _shopOpen = false; }, enabled: true, stretchWidth: true));

            overlay.Children.Add(panel);
            return overlay;
        }
        /// <summary>
        /// Makes the tab button for the item shop 
        /// </summary>
        /// <param name="caption"></param>
        /// <param name="tab"></param>
        /// <param name="isActive"></param>
        /// <returns></returns>
        private Button MakeTabButton(string caption, ShopTab tab, bool isActive)
        {
            var b = new Button
            {
                Content = caption,
                Padding = new Thickness(12, 6, 12, 6),
                Margin = new Thickness(2),
                CornerRadius = new CornerRadius(6),
                Background = new SolidColorBrush(isActive ? Color.FromArgb(220, 70, 70, 70) : Color.FromArgb(140, 40, 40, 40)),
                Foreground = new SolidColorBrush(Colors.White),
                BorderBrush = new SolidColorBrush(isActive ? Colors.Gold : Color.FromArgb(200, 255, 255, 255)),
                BorderThickness = new Thickness(1)
            };
            b.Click += (_, __) => { _shopTab = tab; RefreshUI(); };
            return b;
        }
        /// <summary>
        /// This method builds out all the options for the back options in the shop
        /// </summary>
        /// <returns></returns>
        private Grid BuildBacksGridContent()
        {
            var itemsHost = new Grid { Margin = new Thickness(0, 8, 0, 8) };

            itemsHost.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            itemsHost.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            int cols = 2;
            int rows = (_backOptions.Count + cols - 1) / cols;
            for (int r = 0; r < rows; r++)
                itemsHost.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            for (int i = 0; i < _backOptions.Count; i++)
            {
                int r = i / cols;
                int c = i % cols;
                var tile = MakeBackTile(_backOptions[i]);
                Grid.SetRow(tile, r);
                Grid.SetColumn(tile, c);
                itemsHost.Children.Add(tile);
            }

            return itemsHost;
        }
        /// <summary>
        /// Builds out the entire grid for the color felt options
        /// </summary>
        /// <returns></returns>
        private Grid BuildColorGridContent()
        {
            var itemsHost = new Grid { Margin = new Thickness(0, 8, 0, 8) };

            itemsHost.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            itemsHost.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            int cols = 2;
            int rows = (_feltOptions.Count + cols - 1) / cols;
            for (int r = 0; r < rows; r++)
                itemsHost.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            for (int i = 0; i < _feltOptions.Count; i++)
            {
                int r = i / cols;
                int c = i % cols;
                var tile = MakeFeltColorTile(_feltOptions[i]);
                Grid.SetRow(tile, r);
                Grid.SetColumn(tile, c);
                itemsHost.Children.Add(tile);
            }

            return itemsHost;
        }
        /// <summary>
        /// This method builds out a single color felt option 
        /// </summary>
        /// <param name="opt">Takes in a paramter opt which is a FeltOption object</param>
        /// <returns>Returns a small grid containing the color option</returns>
        private Grid MakeFeltColorTile(FeltOption opt)
        {
            var g = new Grid
            {
                Width = 240,
                Height = 280,
                Margin = new Thickness(8),
                Background = new SolidColorBrush(Color.FromArgb(140, 20, 20, 20))
            };

            var tile = new StackPanel
            {
                Spacing = 6,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };

            var colorSample = new Grid
            {
                Width = 180,
                Height = 130,
                Background = new SolidColorBrush(opt.Color),
                CornerRadius = new CornerRadius(8)
            };
            tile.Children.Add(colorSample);

            tile.Children.Add(MakeText(opt.DisplayName, 16, Colors.White, bold: true, centered: true));

            bool owned = _ownedFeltColors.Contains(opt.Key);
            bool equipped = currentColor.Equals(opt.Color);

            string status = owned ? (equipped ? "Equipped" : "Owned") : $"Price: ${opt.Cost:N0}";
            tile.Children.Add(MakeText(status, 14, owned ? Colors.LightGreen : Colors.LightGray, centered: true));

            Button btn;
            if (!owned)
            {
                bool canBuy = _game.Bankroll >= opt.Cost;
                btn = MakeAction("Buy", () =>
                {
                    _game.Bankroll -= opt.Cost;
                    _ownedFeltColors.Add(opt.Key);
                    currentColor = opt.Color;
                    ShowUnlockDialog(opt.DisplayName);
                }, enabled: canBuy, width: 120);
            }
            else
            {
                btn = MakeAction(equipped ? "Selected" : "Apply", () =>
                {
                    currentColor = opt.Color;
                }, enabled: !equipped, width: 120);
            }

            btn.HorizontalAlignment = HorizontalAlignment.Center;
            tile.Children.Add(btn);

            g.Children.Add(tile);
            return g;
        }
        /// <summary>
        /// This method builds out a single back option 
        /// </summary>
        /// <param name="opt">Takes in a parameter opt which is a BackOption object</param>
        /// <returns>Returns an individual grid with a single back option</returns>
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

            var img = MakeCardImage(opt.AssetFile, 100, 160);
            tile.Children.Add(img);

            var name = MakeText(opt.DisplayName, 16, Colors.White, bold: true, centered: true);
            tile.Children.Add(name);

            bool owned = _ownedBacks.Contains(opt.Key);
            bool equipped = currentBack == opt.Key;

            string status = owned ? (equipped ? "Equipped" : "Owned") : $"Price: ${opt.Cost:N0}";
            tile.Children.Add(MakeText(status, 14, owned ? Colors.LightGreen : Colors.LightGray, centered: true));

            Button actionBtn;
            if (!owned)
            {
                bool canBuy = _game.Bankroll >= opt.Cost;
                actionBtn = MakeAction("Buy", () =>
                {
                    _game.Bankroll -= opt.Cost;
                    _ownedBacks.Add(opt.Key);
                    currentBack = opt.Key;
                    ShowUnlockDialog(opt.DisplayName);
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
        /// <summary>
        /// Builds out game over screen 
        /// </summary>
        /// <returns>Grid object for overlay</returns>
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
                _ownedFeltColors = new List<string> { "default" };
                var defaultFelt = _feltOptions.Find(f => f.Key == "default");
                if (defaultFelt != null) currentColor = defaultFelt.Color;
                currentBack = "default";
            }, enabled: true, stretchWidth: true));
            panel.Children.Add(MakeAction("Quit", () =>
            {
                Application.Current.Exit();
            }, enabled: true, stretchWidth: true));
            overlay.Children.Add(panel);
            return overlay;
        }
        /// <summary>
        /// Makes a textblock with specified requirements
        /// </summary>
        /// <param name="text"></param>
        /// <param name="size"></param>
        /// <param name="color"></param>
        /// <param name="bold">Default to false</param>
        /// <param name="centered">Default to false</param>
        /// <returns></returns>
        private TextBlock MakeText(string text, double size, Color color, bool bold = false, bool centered = false)
        {
            return new TextBlock
            {
                Text = text,
                Foreground = new SolidColorBrush(color),
                FontSize = size,
                FontWeight = bold ? Windows.UI.Text.FontWeights.SemiBold : Windows.UI.Text.FontWeights.Normal,
                TextAlignment = centered ? TextAlignment.Center : TextAlignment.Left,
            };
        }
        /// <summary>
        /// Creates a reusable UI row with a label on the left and a bold value aligned on the right.
        /// </summary>
        /// <param name="label"></param>
        /// <param name="value"></param>
        /// <returns></returns>
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
        /// <summary>
        /// General method which can be used to make actions
        /// </summary>
        /// <param name="caption"></param>
        /// <param name="onClick"></param>
        /// <param name="small"></param>
        /// <param name="enabled"></param>
        /// <param name="width"></param>
        /// <param name="stretchWidth"></param>
        /// <returns>A button with whatever action is specified</returns>
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
        /// <summary>
        /// Builds out a hand
        /// </summary>
        /// <param name="hand">Takes the hand object</param>
        /// <param name="hideHole">hideHole is when dealer's second card is face down</param>
        /// <returns>Stackpanel of card images</returns>
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
                var isHole = hideHole && i == 1; // if hidehole is true and if card is at idx 1 (second card), show back of card, otherwise show card
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
        /// <summary>
        /// Builds out the image for the card
        /// </summary>
        /// <param name="relativePath">Relative path to image</param>
        /// <param name="w"><Width/param>
        /// <param name="h">Height</param>
        /// <returns>Image object of the card</returns>
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
       /// <summary>
       /// Runs background music in a loop 
       /// </summary>
       /// <param name="playing">Takes boolean 'playing' if you don't want to play it initially but is defaulted to true</param>
        private void PlayBackgroundMusic(bool playing = true)
        {
            try
            {
                if (_bgmPlayer == null)
                {
                    _bgmPlayer = new MediaPlayer
                    {
                        IsLoopingEnabled = true,
                        Volume = 0.3
                    };
                }

                var uri = new Uri("ms-appx:///Assets/Music/default.mp3");
                _bgmPlayer.Source = MediaSource.CreateFromUri(uri);
                if (playing) _bgmPlayer.Play();
            }
            catch (Exception ex)
            {
                Debug.WriteLine("BGM error: " + ex.Message);
            }
        }
        /// <summary>
        /// Void method to play sound effect
        /// </summary>
        /// <param name="src">Takes paramater of type MediaSource</param>
        private void PlaySoundEffect(MediaSource src)
        {
            try
            {
                if (_sfxPlayer == null || src == null) return;

                _sfxPlayer.Source = src; 
                _sfxPlayer.PlaybackSession.Position = TimeSpan.Zero;
                _sfxPlayer.Play();
            }
            catch (Exception ex)
            {
                Debug.WriteLine("SFX error: " + ex.Message);
            }
        }

        #endregion
    }
}