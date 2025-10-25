using System;
using System.IO;
using System.Linq;
using System.Timers;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Shapes;
using System.Windows.Threading;
using Fellowship_overlay.Core;
using Fellowship_overlay.Services;
using WinForms = System.Windows.Forms;

using WpfOrientation = System.Windows.Controls.Orientation;
using WpfRectangle = System.Windows.Shapes.Rectangle;
using WpfHAlign = System.Windows.HorizontalAlignment;

namespace Fellowship_overlay
{
    public partial class MainWindow : Window
    {
        private readonly AppSettings _settings = SettingsStore.Load();
        private readonly BuffStore _store = new();
        private readonly DispatcherTimer _uiTimer = new() { Interval = TimeSpan.FromMilliseconds(100) };
        private LogWatcher? _watcher;
        private string _playerName = string.Empty;
        private string? _playerGuid;

        public MainWindow()
        {
            InitializeComponent();

            PopulateSetupFields();

            if (!TryApplySettings(out var message))
            {
                ShowSetup(message);
            }

            _uiTimer.Tick += (_, __) => RefreshUI();
            _uiTimer.Start();

            // Optional: click-through (toggle with hotkey)
            // MakeClickThrough(true);
        }

        protected override void OnClosed(EventArgs e)
        {
            base.OnClosed(e);
            _uiTimer.Stop();
            _watcher?.Dispose();
        }

        private void PopulateSetupFields()
        {
            LogDirTextBox.Text = _settings.LogDirectory ?? string.Empty;
            PlayerNameTextBox.Text = _settings.PlayerName ?? string.Empty;
            PlayerGuidTextBox.Text = _settings.PlayerGuid ?? string.Empty;
        }

        private bool TryApplySettings(out string message)
        {
            message = string.Empty;

            if (string.IsNullOrWhiteSpace(_settings.LogDirectory) || !Directory.Exists(_settings.LogDirectory))
            {
                message = "We couldn't find your Fellowship combat-log folder. Pick the folder to continue.";
                return false;
            }

            if (string.IsNullOrWhiteSpace(_settings.PlayerName))
            {
                message = "Enter the in-game name of the character you want to track.";
                return false;
            }

            _playerName = _settings.PlayerName.Trim();
            _playerGuid = string.IsNullOrWhiteSpace(_settings.PlayerGuid) ? null : _settings.PlayerGuid.Trim();

            _watcher?.Dispose();
            _watcher = new LogWatcher(_settings.LogDirectory);
            _watcher.Line += OnLine;

            HideSetup();
            return true;
        }

        private void ShowSetup(string message)
        {
            SetupMessageTextBlock.Text = string.IsNullOrWhiteSpace(message)
                ? "Choose your combat-log folder and who you are playing to get started."
                : message;
            SetupOverlay.Visibility = Visibility.Visible;
        }

        private void HideSetup()
        {
            SetupOverlay.Visibility = Visibility.Collapsed;
        }

        private void OnLine(string line)
        {
            var e = LineParser.TryParseAura(line, _playerName, _playerGuid);
            if (e != null)
            {
                _store.Apply(e);
            }
        }

        private void RefreshUI()
        {
            _watcher?.Tick();

            var now = DateTimeOffset.Now;
            _store.Prune(now);

            BuffPanel.Children.Clear();
            foreach (var b in _store.Snapshot().OrderBy(x => (x.ExpiresAt ?? now).ToUnixTimeMilliseconds()))
            {
                var total = (b.ExpiresAt?.Subtract(b.AppliedAt)).GetValueOrDefault(TimeSpan.FromSeconds(1));
                var left  = b.ExpiresAt.HasValue ? b.ExpiresAt.Value - now : (TimeSpan?)null;
                var pct   = left.HasValue ? Math.Max(0, Math.Min(1, left.Value.TotalMilliseconds / total.TotalMilliseconds)) : 1.0;

                var sp = new StackPanel { Margin = new Thickness(6), Orientation = WpfOrientation.Vertical };
                sp.Children.Add(new DockPanel
                {
                    Children =
                    {
                        new TextBlock { Text=b.Name, FontWeight=FontWeights.SemiBold },
                        new TextBlock {
                            Text = (b.Stacks>1?($" x{b.Stacks} · "):"") + (left.HasValue?Fmt(left.Value):"∞"),
                            HorizontalAlignment = WpfHAlign.Right
                        }

                    }
                });
                var bar = new WpfRectangle
                {
                    Height = 10,
                    Fill = System.Windows.Media.Brushes.LightSkyBlue,
                    Width = 400 * pct,
                    RadiusX = 5,
                    RadiusY = 5
                };

                var bg  = new Border { Background = System.Windows.Media.Brushes.DimGray, CornerRadius = new CornerRadius(5), Padding=new Thickness(0), Child=bar };
                sp.Children.Add(bg);
                BuffPanel.Children.Add(sp);
            }
        }

        private static string Fmt(TimeSpan t) => t.TotalSeconds >= 10 ? $"{(int)t.TotalSeconds}s" : $"{t.TotalSeconds:F1}s";

        private void OnBrowseLogDir(object sender, RoutedEventArgs e)
        {
            using var dialog = new WinForms.FolderBrowserDialog
            {
                Description = "Choose your Fellowship combat-log folder"
            };

            if (Directory.Exists(LogDirTextBox.Text))
            {
                dialog.SelectedPath = LogDirTextBox.Text;
            }

            if (dialog.ShowDialog() == WinForms.DialogResult.OK && Directory.Exists(dialog.SelectedPath))
            {
                LogDirTextBox.Text = dialog.SelectedPath;
            }
        }

        private void OnSaveSettings(object sender, RoutedEventArgs e)
        {
            var logDir = LogDirTextBox.Text.Trim();
            var playerName = PlayerNameTextBox.Text.Trim();
            var playerGuid = string.IsNullOrWhiteSpace(PlayerGuidTextBox.Text) ? null : PlayerGuidTextBox.Text.Trim();

            if (string.IsNullOrWhiteSpace(logDir) || !Directory.Exists(logDir))
            {
                ShowSetup("Please choose a valid Fellowship combat-log folder.");
                return;
            }

            if (string.IsNullOrWhiteSpace(playerName))
            {
                ShowSetup("Please enter your character name to continue.");
                return;
            }

            _settings.LogDirectory = logDir;
            _settings.PlayerName = playerName;
            _settings.PlayerGuid = playerGuid;

            SettingsStore.Save(_settings);

            if (!TryApplySettings(out var message))
            {
                ShowSetup(message);
            }
        }

        // Click-through helper (optional)
        // using System.Runtime.InteropServices;
        // const int GWL_EXSTYLE = -20, WS_EX_TRANSPARENT = 0x20, WS_EX_LAYERED = 0x80000;
        // [DllImport("user32.dll")] static extern int GetWindowLong(IntPtr hWnd, int nIndex);
        // [DllImport("user32.dll")] static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);
        // protected override void OnSourceInitialized(EventArgs e) { base.OnSourceInitialized(e); ToggleClickThrough(false); }
        // void MakeClickThrough(bool on){ var hwnd=new System.Windows.Interop.WindowInteropHelper(this).Handle;
        //   int ex=GetWindowLong(hwnd,GWL_EXSTYLE); if(on) ex|=WS_EX_TRANSPARENT|WS_EX_LAYERED; else ex&=~WS_EX_TRANSPARENT;
        //   SetWindowLong(hwnd,GWL_EXSTYLE,ex);
        // }
    }
}