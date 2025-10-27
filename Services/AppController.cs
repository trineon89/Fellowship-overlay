using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using Fellowship_overlay.Core;

namespace Fellowship_overlay.Services;

public sealed class AppController : IDisposable
{
    private AppSettings _settings;
    private readonly Dictionary<Guid, OverlayHost> _overlays = new();
    private BuffMonitor? _monitor;
	private readonly GlobalInputMonitor _inputMonitor;
    private readonly DebugLogService _debugLog = new();
    private DebugWindow? _debugWindow;

    public AppSettings Settings => _settings;

    public event EventHandler<OverlaySettings>? OverlaySettingsChanged;
    public event EventHandler<bool>? DebugEnabledChanged;

    public AppController()
    {
        _settings = SettingsStore.Load();
        EnsureDefaults();
        BuildOverlays();
		_inputMonitor = new GlobalInputMonitor();
        _inputMonitor.InputActivity += OnInputActivity;
        RefreshMonitor();
        if (_settings.DebugEnabled)
        {
            // ensure monitor forwards debug data even before the settings window opens
            _monitor?.SetDebugLog(_debugLog);
        }
    }

    private void EnsureDefaults()
    {
        if (!_settings.ClickThrough)
        {
            // legacy builds stored only the click-through flag
            _settings.OverlaysLocked = false;
        }

        _settings.ClickThrough = _settings.OverlaysLocked;

        if (!_settings.Overlays.Any())
        {
            var defaultPreset = BuffCatalog.Presets.FirstOrDefault();
            var overlay = new OverlaySettings
            {
                Name = "Fellowship Buffs",
                TrackedSpellIds = defaultPreset?.SpellIds.ToList() ?? new List<int>(),
                Left = 100,
                Top = 100,
                Width = 420,
                Height = 280
            };
            _settings.Overlays.Add(overlay);
        }

        // ensure tracked spells exist in catalog? allow custom values.
        foreach (var overlay in _settings.Overlays)
        {
            if (overlay.TrackedSpellIds == null)
            {
                overlay.TrackedSpellIds = new List<int>();
            }
        }
    }

    private void BuildOverlays()
    {
        foreach (var overlay in _settings.Overlays.ToArray())
        {
            if (overlay.Id == Guid.Empty)
            {
                overlay.Id = Guid.NewGuid();
            }

            var host = new OverlayHost(overlay, _settings.OverlaysLocked, OnOverlaySettingsMutated);
            _overlays[overlay.Id] = host;
        }
    }

    public OverlaySettings AddOverlay(BuffPreset? preset)
    {
        var overlay = new OverlaySettings
        {
            Name = preset?.Name ?? $"Overlay {_settings.Overlays.Count + 1}",
            TrackedSpellIds = preset?.SpellIds.ToList() ?? new List<int>(),
            Left = 120 + _settings.Overlays.Count * 40,
            Top = 120 + _settings.Overlays.Count * 40,
            Width = 420,
            Height = 280
        };

        _settings.Overlays.Add(overlay);
        var host = new OverlayHost(overlay, _settings.OverlaysLocked, OnOverlaySettingsMutated);
        _overlays[overlay.Id] = host;
        if (_monitor != null)
        {
            host.AttachMonitor(_monitor);
            host.SetStatus(null);
        }
        else
        {
            host.SetStatus(GetValidationMessage());
        }

        OverlaySettingsChanged?.Invoke(this, overlay);
        return overlay;
    }

    public bool RemoveOverlay(Guid id)
    {
        if (_settings.Overlays.Count <= 1)
        {
            return false;
        }

        _settings.Overlays.RemoveAll(o => o.Id == id);
        if (_overlays.TryGetValue(id, out var host))
        {
            host.Dispose();
            _overlays.Remove(id);
        }

        return true;
    }

    public void UpdateOverlay(OverlaySettings overlay)
    {
        if (_overlays.TryGetValue(overlay.Id, out var host))
        {
            host.UpdateFromSettings();
        }
        OverlaySettingsChanged?.Invoke(this, overlay);
    }

    public void UpdateGeneralSettings(string? logDir, string? playerName, string? playerGuid, string? playerClass)
    {
        _settings.LogDirectory = string.IsNullOrWhiteSpace(logDir) ? null : logDir.Trim();
        _settings.PlayerName = string.IsNullOrWhiteSpace(playerName) ? null : playerName.Trim();
        _settings.PlayerGuid = string.IsNullOrWhiteSpace(playerGuid) ? null : playerGuid.Trim();
		_settings.PlayerClass = string.IsNullOrWhiteSpace(playerClass) ? null : playerClass.Trim();
        RefreshMonitor();
    }

    public bool ValidateGeneralSettings(out string message)
    {
        var error = GetValidationMessage();
        if (!string.IsNullOrEmpty(error))
        {
            message = error;
            return false;
        }

        message = string.Empty;
        return true;
    }

    private string? GetValidationMessage()
    {
        if (string.IsNullOrWhiteSpace(_settings.LogDirectory) || !Directory.Exists(_settings.LogDirectory))
        {
            return "Choose a valid combat-log folder to start tracking.";
        }

        if (string.IsNullOrWhiteSpace(_settings.PlayerName))
        {
            return "Enter the character name to track.";
        }

        return null;
    }

    private void RefreshMonitor()
    {
        var message = GetValidationMessage();
        if (message != null)
        {
            _monitor?.Dispose();
            _monitor = null;
            foreach (var host in _overlays.Values)
            {
                host.DetachMonitor();
                host.SetStatus(message);
            }
            return;
        }

        var logDir = _settings.LogDirectory!;
        var playerName = _settings.PlayerName!;
        var playerGuid = _settings.PlayerGuid;

        _monitor?.Dispose();
        _monitor = new BuffMonitor(logDir, playerName, playerGuid, _settings.DebugEnabled ? _debugLog : null);
        foreach (var host in _overlays.Values)
        {
            host.AttachMonitor(_monitor);
            host.SetStatus(null);
        }
    }

    public void SaveSettings() => SettingsStore.Save(_settings);

    public void SetOverlaysLocked(bool locked)
    {
        _settings.OverlaysLocked = locked;
        _settings.ClickThrough = locked;
        foreach (var host in _overlays.Values)
        {
            host.SetLocked(locked);
        }
    }

    public void SetDebugEnabled(bool enabled, Window? owner = null)
    {
        if (_settings.DebugEnabled == enabled && (enabled ? _debugWindow != null : true))
        {
            if (enabled && owner != null && _debugWindow != null && _debugWindow.Owner == null)
            {
                _debugWindow.Owner = owner;
            }
            if (enabled && _debugWindow != null)
            {
                if (_debugWindow.Visibility != Visibility.Visible)
                {
                    _debugWindow.Show();
                }
                _debugWindow.Activate();
            }
            return;
        }

        _settings.DebugEnabled = enabled;
        if (enabled)
        {
            EnsureDebugWindow(owner);
            _monitor?.SetDebugLog(_debugLog);
        }
        else
        {
            _monitor?.SetDebugLog(null);
            CloseDebugWindow();
            _debugLog.Clear();
        }

        DebugEnabledChanged?.Invoke(this, enabled);
    }

    private void EnsureDebugWindow(Window? owner)
    {
        if (_debugWindow == null)
        {
            _debugWindow = new DebugWindow(_debugLog)
            {
                Owner = owner
            };
            _debugWindow.Closed += OnDebugWindowClosed;
            _debugWindow.Show();
        }
        else
        {
            if (owner != null && _debugWindow.Owner == null)
            {
                _debugWindow.Owner = owner;
            }
            if (_debugWindow.Visibility != Visibility.Visible)
            {
                _debugWindow.Show();
            }
            _debugWindow.Activate();
        }
    }

    private void CloseDebugWindow()
    {
        if (_debugWindow != null)
        {
            var window = _debugWindow;
            _debugWindow = null;
            window.Closed -= OnDebugWindowClosed;
            window.Close();
        }
    }

    private void OnDebugWindowClosed(object? sender, EventArgs e)
    {
        if (sender is Window window)
        {
            window.Closed -= OnDebugWindowClosed;
        }

        _debugWindow = null;
        if (_settings.DebugEnabled)
        {
            _settings.DebugEnabled = false;
            _monitor?.SetDebugLog(null);
            _debugLog.Clear();
            DebugEnabledChanged?.Invoke(this, false);
            SaveSettings();
        }
    }

    private void OnOverlaySettingsMutated(OverlaySettings overlay)
    {
        OverlaySettingsChanged?.Invoke(this, overlay);
    }

    public void Dispose()
    {
        _monitor?.Dispose();
        foreach (var host in _overlays.Values)
        {
            host.Dispose();
        }
        _overlays.Clear();
        CloseDebugWindow();
		_inputMonitor.InputActivity -= OnInputActivity;
        _inputMonitor.Dispose();
    }

    private void OnInputActivity(object? sender, EventArgs e)
    {
        System.Windows.Application.Current.Dispatcher.BeginInvoke(new Action(() =>
        {
            var hosts = _overlays.Values.ToArray();
            _ = Task.Run(() =>
            {
                foreach (var host in hosts)
                {
                    host.HandleInputActivity();
                }
            });
        }));
    }
}