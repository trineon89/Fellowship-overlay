using System;
using System.Windows.Threading;
using Fellowship_overlay.Core;

namespace Fellowship_overlay.Services;

public sealed class BuffMonitor : IDisposable
{
    private readonly LogWatcher _watcher;
    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromMilliseconds(100) };
    private readonly string _playerName;
    private readonly string? _playerGuid;
    private IDebugLog? _debug;

    public event Action<AuraEvent>? Aura;
    public event Action<DateTimeOffset>? Tick;

    public BuffMonitor(string logDirectory, string playerName, string? playerGuid, IDebugLog? debugLog = null)
    {
        _playerName = playerName;
        _playerGuid = playerGuid;
        _watcher = new LogWatcher(logDirectory);
        _watcher.Line += OnLine;
        _timer.Tick += OnTimerTick;
        _timer.Start();
        _debug = debugLog;
    }

    private void OnTimerTick(object? sender, EventArgs e) => OnTick();

    private void OnTick()
    {
        _watcher.Tick();
        Tick?.Invoke(DateTimeOffset.Now);
    }

    private void OnLine(string line)
    {
        _debug?.Log(DateTimeOffset.Now, "log", line);
        var ev = LineParser.TryParseAura(line, _playerName, _playerGuid);
        if (ev != null)
        {
            _debug?.Log(ev.Timestamp, "aura", $"{ev.Type}: {ev.SpellName} (#{ev.SpellId}) x{ev.Stacks} for {ev.DurationSeconds:F1}s");
            Aura?.Invoke(ev);
        }
    }

    public void SetDebugLog(IDebugLog? debugLog)
    {
        _debug = debugLog;
    }

    public void Dispose()
    {
        _timer.Stop();
        _timer.Tick -= OnTimerTick;
        _watcher.Line -= OnLine;
        _watcher.Dispose();
    }
}