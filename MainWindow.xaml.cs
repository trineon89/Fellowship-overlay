using System;
using System.Linq;
using System.Timers;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Shapes;
using System.Windows.Threading;
using Fellowship_overlay.Core;

namespace Fellowship_overlay
{
    public partial class MainWindow : Window
    {
        private readonly LogWatcher _watcher;
        private readonly BuffStore _store = new();
        private readonly DispatcherTimer _uiTimer = new() { Interval = TimeSpan.FromMilliseconds(100) };
        private readonly string _playerName = "Lumpes";      // TODO: bind from Settings
        private readonly string? _playerGuid = null;         // optional

        public MainWindow()
        {
            InitializeComponent();

            _watcher = new LogWatcher(@"C:\Path\To\Fellowship\Saved\CombatLogs"); // TODO: settings
            _watcher.Line += OnLine;

            _uiTimer.Tick += (_, __) => RefreshUI();
            _uiTimer.Start();

            // Optional: click-through (toggle with hotkey)
            // MakeClickThrough(true);
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
            var now = DateTimeOffset.Now;
            _store.Prune(now);

            BuffPanel.Children.Clear();
            foreach (var b in _store.Snapshot().OrderBy(x => (x.ExpiresAt ?? now).ToUnixTimeMilliseconds()))
            {
                var total = (b.ExpiresAt?.Subtract(b.AppliedAt)).GetValueOrDefault(TimeSpan.FromSeconds(1));
                var left  = b.ExpiresAt.HasValue ? b.ExpiresAt.Value - now : (TimeSpan?)null;
                var pct   = left.HasValue ? Math.Max(0, Math.Min(1, left.Value.TotalMilliseconds / total.TotalMilliseconds)) : 1.0;

                var sp = new StackPanel { Margin = new Thickness(6), Orientation = Orientation.Vertical };
                sp.Children.Add(new DockPanel
                {
                    Children =
                    {
                        new TextBlock { Text=b.Name, FontWeight=FontWeights.SemiBold },
                        new TextBlock { Text=(b.Stacks>1?($" x{b.Stacks} · "):"") + (left.HasValue?Fmt(left.Value):"∞"),
                                        HorizontalAlignment=HorizontalAlignment.Right }
                    }
                });
                var bar = new Rectangle { Height=10, Fill=System.Windows.Media.Brushes.LightSkyBlue, Width=400 * pct, RadiusX=5, RadiusY=5 };
                var bg  = new Border { Background = System.Windows.Media.Brushes.DimGray, CornerRadius = new CornerRadius(5), Padding=new Thickness(0), Child=bar };
                sp.Children.Add(bg);
                BuffPanel.Children.Add(sp);
            }
        }

        private static string Fmt(TimeSpan t) => t.TotalSeconds >= 10 ? $"{(int)t.TotalSeconds}s" : $"{t.TotalSeconds:F1}s";

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
