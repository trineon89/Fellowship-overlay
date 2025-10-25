using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using Fellowship_overlay.Core;

namespace Fellowship_overlay.Services;

public sealed class OverlayHost : IDisposable
{
	private static readonly TimeSpan CaptureHoldDuration = TimeSpan.FromSeconds(1.5);
	
    private readonly OverlaySettings _settings;
    private readonly OverlayWindow _window;
    private readonly BuffStore _store = new();
    private readonly HashSet<int> _trackedSpellIds;
    private readonly Dictionary<int, int> _orderLookup;
    private readonly Action<OverlaySettings>? _onSettingsChanged;
    private bool _applyingSettings;
    private BuffMonitor? _monitor;
	private (Buff Buff, BuffDefinition? Definition)[] _lastBuffs = Array.Empty<(Buff, BuffDefinition?)>();
    private DateTimeOffset _lastUpdateTimestamp = DateTimeOffset.Now;
	private bool _screenCaptureActive;
    private DateTimeOffset _captureHoldUntil = DateTimeOffset.MinValue;
    private readonly object _stateLock = new();

    public Guid Id => _settings.Id;

    public OverlayHost(OverlaySettings settings, bool locked, Action<OverlaySettings>? onSettingsChanged)
    {
        _settings = settings;
        _trackedSpellIds = new HashSet<int>(settings.TrackedSpellIds);
        _orderLookup = settings.TrackedSpellIds
            .Select((spellId, index) => (spellId, index))
            .ToDictionary(pair => pair.spellId, pair => pair.index);
        _onSettingsChanged = onSettingsChanged;
		
		_screenCaptureActive = settings.BuffCaptureRegion?.IsValid == true;

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
    }

    private void OnAura(AuraEvent e)
    {
        bool tracked;
        lock (_stateLock)
        {
            tracked = _trackedSpellIds.Contains(e.SpellId);
        }
        if (!tracked) return;
        _store.Apply(e);
    }

    private void OnTick(DateTimeOffset now)
    {
		bool screenCaptureActive;
        DateTimeOffset captureHoldUntil;
        Dictionary<int, int> orderLookupSnapshot;
        lock (_stateLock)
        {
            screenCaptureActive = _screenCaptureActive;
			captureHoldUntil = _captureHoldUntil;
            orderLookupSnapshot = new Dictionary<int, int>(_orderLookup);
        }
        if (screenCaptureActive && now <= captureHoldUntil)
        {
            return;
        }
        _store.Prune(now);
        var snapshot = _store.Snapshot();
        var ordered = snapshot
            .OrderBy(b => orderLookupSnapshot.TryGetValue(b.SpellId, out var index) ? index : int.MaxValue)
            .ThenBy(b => (b.ExpiresAt ?? now).ToUnixTimeMilliseconds())
            .Select(b => (b, BuffCatalog.TryGet(b.SpellId, out var def) ? def : null))
            .ToArray();

        var nowSnapshot = now;
		lock (_stateLock)
        {
            _lastBuffs = ordered;
            _lastUpdateTimestamp = nowSnapshot;
        }
        System.Windows.Application.Current.Dispatcher.Invoke(() => _window.UpdateBuffs(ordered, nowSnapshot));
    }

    public void UpdateFromSettings()
    {
		var wasScreenCaptureActive = _screenCaptureActive;
        _applyingSettings = true;
        try
        {
            _window.ApplySettings(_settings);
        }
        finally
        {
            _applyingSettings = false;
        }
        bool screenCaptureChanged;
        (Buff Buff, BuffDefinition? Definition)[] snapshot;
        DateTimeOffset timestamp;
        lock (_stateLock)
        {
            _trackedSpellIds.Clear();
            _trackedSpellIds.UnionWith(_settings.TrackedSpellIds);
            _orderLookup.Clear();
            foreach (var (spellId, index) in _settings.TrackedSpellIds.Select((id, i) => (id, i)))
            {
                _orderLookup[spellId] = index;
            }

            _screenCaptureActive = _settings.BuffCaptureRegion?.IsValid == true;
            screenCaptureChanged = _screenCaptureActive != wasScreenCaptureActive;
			if (!_screenCaptureActive)
            {
                _captureHoldUntil = DateTimeOffset.MinValue;
            }
            snapshot = _lastBuffs;
            timestamp = _lastUpdateTimestamp == DateTimeOffset.MinValue ? DateTimeOffset.Now : _lastUpdateTimestamp;
        }
		if (screenCaptureChanged)
        {
            ClearStore();
            lock (_stateLock)
            {
                snapshot = _lastBuffs;
                timestamp = _lastUpdateTimestamp;
            }
        }

        System.Windows.Application.Current.Dispatcher.Invoke(() => _window.UpdateBuffs(snapshot, timestamp));

        // Remove buffs that are no longer tracked
		HashSet<int> trackedSnapshot;
        lock (_stateLock)
        {
            trackedSnapshot = new HashSet<int>(_trackedSpellIds);
        }
        foreach (var buff in _store.Snapshot())
        {
            if (!trackedSnapshot.Contains(buff.SpellId))
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
		(Buff Buff, BuffDefinition? Definition)[] snapshot;
        DateTimeOffset timestamp;
        lock (_stateLock)
        {
            _lastBuffs = Array.Empty<(Buff, BuffDefinition?)>();
            _lastUpdateTimestamp = DateTimeOffset.Now;
			_captureHoldUntil = DateTimeOffset.MinValue;
            snapshot = _lastBuffs;
            timestamp = _lastUpdateTimestamp;
        }

        System.Windows.Application.Current.Dispatcher.Invoke(() => _window.UpdateBuffs(snapshot, timestamp));
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
	
	public void HandleInputActivity()
    {
        CaptureRegionSettings? regionCopy;
        int[] trackedIds;
        Dictionary<int, int> orderLookupSnapshot;

        lock (_stateLock)
        {
            if (!_screenCaptureActive)
            {
                return;
            }

            var region = _settings.BuffCaptureRegion;
            if (region == null || !region.IsValid)
            {
                return;
            }

            regionCopy = region.Clone();

            trackedIds = _trackedSpellIds.ToArray();
            orderLookupSnapshot = new Dictionary<int, int>(_orderLookup);
        }

        using var capture = ScreenCaptureService.Instance.Capture(regionCopy);
        if (capture == null)
        {
            return;
        }

        var recognized = BuffIconRecognizer.Instance.Recognize(capture, trackedIds);
        var now = DateTimeOffset.Now;
        if (recognized.Count == 0)
        {
            lock (_stateLock)
            {
                _captureHoldUntil = DateTimeOffset.MinValue;
            }
            return;
        }

        var ordered = recognized
            .OrderBy(r => orderLookupSnapshot.TryGetValue(r.Definition.SpellId, out var index) ? index : int.MaxValue)
            .ThenBy(r => r.Definition.Name)
            .ToArray();

        var mapped = ordered
            .Select(r =>
            {
                var buff = new Buff
                {
                    SpellId = r.Definition.SpellId,
                    Name = r.Definition.Name,
                    Stacks = Math.Max(1, r.Stacks),
                    AppliedAt = now,
                    ExpiresAt = null
                };
                return (buff, (BuffDefinition?)r.Definition);
            })
            .ToArray();

        lock (_stateLock)
        {
            _lastBuffs = mapped;
            _lastUpdateTimestamp = now;
			_captureHoldUntil = now.Add(CaptureHoldDuration);
        }
        System.Windows.Application.Current.Dispatcher.Invoke(() => _window.UpdateBuffs(mapped, now));
    }
}