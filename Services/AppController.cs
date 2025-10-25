using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Fellowship_overlay.Core;

namespace Fellowship_overlay.Services;

public sealed class AppController : IDisposable
{
    private AppSettings _settings;
    private readonly Dictionary<Guid, OverlayHost> _overlays = new();
    private BuffMonitor? _monitor;

    public AppSettings Settings => _settings;

    public AppController()
    {
        _settings = SettingsStore.Load();
        EnsureDefaults();
        BuildOverlays();
        RefreshMonitor();
    }

    private void EnsureDefaults()
    {
        if (!_settings.Overlays.Any())
        {
            var defaultPreset = BuffCatalog.Presets.FirstOrDefault();
            var overlay = new OverlaySettings
            {
                Name = "Raid Buffs",
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

            var host = new OverlayHost(overlay);
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
        var host = new OverlayHost(overlay);
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
    }

    public void UpdateGeneralSettings(string? logDir, string? playerName, string? playerGuid)
    {
        _settings.LogDirectory = string.IsNullOrWhiteSpace(logDir) ? null : logDir.Trim();
        _settings.PlayerName = string.IsNullOrWhiteSpace(playerName) ? null : playerName.Trim();
        _settings.PlayerGuid = string.IsNullOrWhiteSpace(playerGuid) ? null : playerGuid.Trim();
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
        _monitor = new BuffMonitor(logDir, playerName, playerGuid);
        foreach (var host in _overlays.Values)
        {
            host.AttachMonitor(_monitor);
            host.SetStatus(null);
        }
    }

    public void SaveSettings() => SettingsStore.Save(_settings);

    public void Dispose()
    {
        _monitor?.Dispose();
        foreach (var host in _overlays.Values)
        {
            host.Dispose();
        }
        _overlays.Clear();
    }
}