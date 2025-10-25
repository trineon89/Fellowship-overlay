using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Threading;
using Fellowship_overlay.Core;
using Fellowship_overlay.UI;
using Fellowship_overlay.ViewModels;

namespace Fellowship_overlay;

public sealed class AppController : INotifyPropertyChanged, IDisposable
{
    private readonly BuffStore _buffStore = new();
    private readonly Dispatcher _dispatcher = Application.Current.Dispatcher;
    private readonly DispatcherTimer _pruneTimer;
    private readonly ObservableCollection<BuffViewModel> _buffs = new();
    private readonly ObservableCollection<string> _debugLines = new();
    private readonly List<OverlayWindow> _overlays = new();

    private LogWatcher? _logWatcher;
    private string? _logDirectory;
    private string _playerName = string.Empty;
    private string? _playerGuid;
    private bool _overlaysLocked = true;
    private DebugWindow? _debugWindow;

    public event PropertyChangedEventHandler? PropertyChanged;

    public ReadOnlyObservableCollection<BuffViewModel> Buffs { get; }
    public ReadOnlyObservableCollection<string> DebugLines { get; }

    public string? LogDirectory
    {
        get => _logDirectory;
        private set => SetField(ref _logDirectory, value);
    }

    public string PlayerName
    {
        get => _playerName;
        private set => SetField(ref _playerName, value);
    }

    public string? PlayerGuid
    {
        get => _playerGuid;
        private set => SetField(ref _playerGuid, value);
    }

    public bool OverlaysLocked
    {
        get => _overlaysLocked;
        set
        {
            if (SetField(ref _overlaysLocked, value))
            {
                foreach (var overlay in _overlays.ToArray())
                    overlay.UpdateLockState();
            }
        }
    }

    public AppController()
    {
        Buffs = new ReadOnlyObservableCollection<BuffViewModel>(_buffs);
        DebugLines = new ReadOnlyObservableCollection<string>(_debugLines);

        _pruneTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(1)
        };
        _pruneTimer.Tick += (_, _) => PruneAndRefresh();
        _pruneTimer.Start();
    }

    public void ApplyConfiguration(AppConfiguration configuration)
    {
        PlayerName = configuration.PlayerName.Trim();
        PlayerGuid = string.IsNullOrWhiteSpace(configuration.PlayerGuid)
            ? null
            : configuration.PlayerGuid.Trim();

        var directory = string.IsNullOrWhiteSpace(configuration.LogDirectory)
            ? null
            : configuration.LogDirectory.Trim();

        if (!string.Equals(LogDirectory, directory, StringComparison.OrdinalIgnoreCase))
        {
            LogDirectory = directory;
            RestartWatcher();
        }

        if (configuration.ShowDebug)
            ShowDebugWindow();
        else
            HideDebugWindow();

        OverlaysLocked = configuration.OverlaysLocked;
    }

    public AppConfiguration CaptureConfiguration()
        => new()
        {
            LogDirectory = LogDirectory ?? string.Empty,
            PlayerName = PlayerName,
            PlayerGuid = PlayerGuid ?? string.Empty,
            OverlaysLocked = OverlaysLocked,
            ShowDebug = _debugWindow != null
        };

    public void AddOverlay()
    {
        var overlay = new OverlayWindow(new OverlayViewModel(this));
        int offset = _overlays.Count;
        if (Application.Current.MainWindow != null && Application.Current.MainWindow.IsVisible)
        {
            overlay.Owner = Application.Current.MainWindow;
        }
        overlay.Closed += (_, _) => RemoveOverlay(overlay);
        overlay.Left = 120 + offset * 30;
        overlay.Top = 120 + offset * 30;
        _overlays.Add(overlay);
        overlay.Show();
        overlay.UpdateLockState();
    }

    internal void RemoveOverlay(OverlayWindow overlay)
    {
        _overlays.Remove(overlay);
    }

    public void ShowDebugWindow()
    {
        if (_debugWindow != null)
        {
            _debugWindow.Focus();
            return;
        }

        _debugWindow = new DebugWindow
        {
            DataContext = new DebugViewModel(DebugLines)
        };
        if (Application.Current.MainWindow != null && Application.Current.MainWindow.IsVisible)
            _debugWindow.Owner = Application.Current.MainWindow;
        _debugWindow.Closed += (_, _) =>
        {
            _debugWindow = null;
            OnPropertyChanged(nameof(IsDebugVisible));
        };
        _debugWindow.Show();
        OnPropertyChanged(nameof(IsDebugVisible));
    }

    public void HideDebugWindow()
    {
        if (_debugWindow == null) return;
        _debugWindow.Close();
        _debugWindow = null;
        OnPropertyChanged(nameof(IsDebugVisible));
    }

    public bool IsDebugVisible => _debugWindow != null;

    public void AppendDebug(string message)
    {
        _dispatcher.InvokeAsync(() =>
        {
            while (_debugLines.Count > 500)
                _debugLines.RemoveAt(0);

            _debugLines.Add(message);
        });
    }

    private void RestartWatcher()
    {
        if (_logWatcher != null)
        {
            _logWatcher.Line -= HandleLogLine;
            _logWatcher.Dispose();
        }
        _logWatcher = null;

        if (string.IsNullOrEmpty(LogDirectory))
        {
            AppendDebug("No log directory configured.");
            return;
        }

        try
        {
            if (!Directory.Exists(LogDirectory))
            {
                AppendDebug($"Log directory '{LogDirectory}' does not exist.");
                return;
            }

            _logWatcher = new LogWatcher(LogDirectory);
            _logWatcher.Line += HandleLogLine;
            AppendDebug($"Watching {LogDirectory} for log changes.");
        }
        catch (Exception ex)
        {
            AppendDebug($"Failed to watch '{LogDirectory}': {ex.Message}");
        }
    }

    private void HandleLogLine(string line)
    {
        AppendDebug(line);

        if (string.IsNullOrWhiteSpace(PlayerName))
            return;

        var auraEvent = LineParser.TryParseAura(line, PlayerName, PlayerGuid);
        if (auraEvent == null)
            return;

        _buffStore.Apply(auraEvent);
        AppendDebug($"Aura {auraEvent.Type}: {auraEvent.SpellName} (#{auraEvent.SpellId}) for {auraEvent.TargetName}");
        PruneAndRefresh();
    }

    private void PruneAndRefresh()
    {
        _dispatcher.InvokeAsync(() =>
        {
            _buffStore.Prune(DateTimeOffset.UtcNow);
            var snapshot = _buffStore.Snapshot()
                .Select(buff => BuffViewModel.FromBuff(buff))
                .OrderBy(b => b.Remaining ?? TimeSpan.MaxValue)
                .ThenBy(b => b.DisplayName)
                .ToList();

            Synchronise(snapshot);
        });
    }

    private void Synchronise(IReadOnlyList<BuffViewModel> snapshot)
    {
        for (var i = 0; i < snapshot.Count; i++)
        {
            if (_buffs.Count <= i)
            {
                _buffs.Add(snapshot[i]);
                continue;
            }

            if (!_buffs[i].IsEquivalentTo(snapshot[i]))
                _buffs[i].UpdateFrom(snapshot[i]);
        }

        while (_buffs.Count > snapshot.Count)
            _buffs.RemoveAt(_buffs.Count - 1);

        foreach (var buff in _buffs)
            buff.Touch();
    }

    private bool SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
            return false;
        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

    public void Dispose()
    {
        _pruneTimer.Stop();
        if (_logWatcher != null)
        {
            _logWatcher.Line -= HandleLogLine;
            _logWatcher.Dispose();
        }
        foreach (var overlay in _overlays.ToArray())
            overlay.Close();
        _debugWindow?.Close();
    }
}

public sealed class AppConfiguration
{
    public string LogDirectory { get; set; } = string.Empty;
    public string PlayerName { get; set; } = string.Empty;
    public string? PlayerGuid { get; set; } = string.Empty;
    public bool OverlaysLocked { get; set; } = true;
    public bool ShowDebug { get; set; }
}