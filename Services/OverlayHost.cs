using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using Fellowship_overlay.Core;

namespace Fellowship_overlay.Services;

public sealed class OverlayHost : IDisposable
{
    private readonly OverlaySettings _settings;
    private readonly OverlayWindow _window;
    private readonly BuffStore _store = new();
    private readonly HashSet<int> _trackedSpellIds;
    private readonly Dictionary<int, int> _orderLookup;
    private readonly Action<OverlaySettings>? _onSettingsChanged;
    private bool _applyingSettings;
    private BuffMonitor? _monitor;

    public Guid Id => _settings.Id;

    public OverlayHost(OverlaySettings settings, bool locked, Action<OverlaySettings>? onSettingsChanged)
    {
        _settings = settings;
        _trackedSpellIds = new HashSet<int>(settings.TrackedSpellIds);
        _orderLookup = settings.TrackedSpellIds
            .Select((spellId, index) => (spellId, index))
            .ToDictionary(pair => pair.spellId, pair => pair.index);
        _onSettingsChanged = onSettingsChanged;

        _window = new OverlayWindow(settings);
        _window.Show();
        _window.SetLockState(locked);
        _window.LocationChanged += OnWindowLocationChanged;
        _window.SizeChanged += OnWindowSizeChanged;
    }

    public void AttachMonitor(BuffMonitor monitor)
    {
        if (_monitor == monitor) return;

        DetachMonitor();
        _monitor = monitor;
        _monitor.Aura += OnAura;
        _monitor.Tick += OnTick;
    }

    public void DetachMonitor()
    {
        if (_monitor == null) return;
        _monitor.Aura -= OnAura;
        _monitor.Tick -= OnTick;
        _monitor = null;
        ClearStore();
        System.Windows.Application.Current.Dispatcher.Invoke(() => _window.UpdateBuffs(Array.Empty<(Buff, BuffDefinition?)>(), DateTimeOffset.Now));
    }

    private void OnAura(AuraEvent e)
    {
        if (!_trackedSpellIds.Contains(e.SpellId)) return;
        _store.Apply(e);
    }

    private void OnTick(DateTimeOffset now)
    {
        _store.Prune(now);
        var snapshot = _store.Snapshot();
        var ordered = snapshot
            .OrderBy(b => _orderLookup.TryGetValue(b.SpellId, out var index) ? index : int.MaxValue)
            .ThenBy(b => (b.ExpiresAt ?? now).ToUnixTimeMilliseconds())
            .Select(b => (b, BuffCatalog.TryGet(b.SpellId, out var def) ? def : null))
            .ToArray();

        var nowSnapshot = now;
        System.Windows.Application.Current.Dispatcher.Invoke(() => _window.UpdateBuffs(ordered, nowSnapshot));
    }

    public void UpdateFromSettings()
    {
        _applyingSettings = true;
        try
        {
            _window.ApplySettings(_settings);
        }
        finally
        {
            _applyingSettings = false;
        }
        _trackedSpellIds.Clear();
        _trackedSpellIds.UnionWith(_settings.TrackedSpellIds);
        _orderLookup.Clear();
        foreach (var (spellId, index) in _settings.TrackedSpellIds.Select((id, i) => (id, i)))
        {
            _orderLookup[spellId] = index;
        }

        // Remove buffs that are no longer tracked
        foreach (var buff in _store.Snapshot())
        {
            if (!_trackedSpellIds.Contains(buff.SpellId))
            {
                _store.Apply(new AuraEvent(buff.AppliedAt, AuraEventType.Removed, "", "", buff.SpellId, buff.Name, 0, 0));
            }
        }
    }

    public void SetStatus(string? message) => System.Windows.Application.Current.Dispatcher.Invoke(() => _window.SetStatus(message));

    public void SetLocked(bool locked)
    {
        System.Windows.Application.Current.Dispatcher.Invoke(() => _window.SetLockState(locked));
    }

    private void ClearStore()
    {
        foreach (var buff in _store.Snapshot())
        {
            _store.Apply(new AuraEvent(buff.AppliedAt, AuraEventType.Removed, "", "", buff.SpellId, buff.Name, 0, 0));
        }
    }

    private void OnWindowLocationChanged(object? sender, EventArgs e)
    {
        if (_applyingSettings) return;
        _settings.Left = _window.Left;
        _settings.Top = _window.Top;
        _onSettingsChanged?.Invoke(_settings);
    }

    private void OnWindowSizeChanged(object? sender, SizeChangedEventArgs e)
    {
        if (_applyingSettings) return;
        _settings.Width = e.NewSize.Width;
        _settings.Height = e.NewSize.Height;
        _onSettingsChanged?.Invoke(_settings);
    }

    public void Dispose()
    {
        DetachMonitor();
        _window.LocationChanged -= OnWindowLocationChanged;
        _window.SizeChanged -= OnWindowSizeChanged;
        _window.Close();
    }
}