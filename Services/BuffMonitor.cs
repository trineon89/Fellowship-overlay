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

    public event Action<AuraEvent>? Aura;
    public event Action<DateTimeOffset>? Tick;

    public BuffMonitor(string logDirectory, string playerName, string? playerGuid)
    {
        _playerName = playerName;
        _playerGuid = playerGuid;
        _watcher = new LogWatcher(logDirectory);
        _watcher.Line += OnLine;
        _timer.Tick += OnTimerTick;
        _timer.Start();
    }

    private void OnTimerTick(object? sender, EventArgs e) => OnTick();

    private void OnTick()
    {
        _watcher.Tick();
        Tick?.Invoke(DateTimeOffset.Now);
    }

    private void OnLine(string line)
    {
        var ev = LineParser.TryParseAura(line, _playerName, _playerGuid);
        if (ev != null)
        {
            Aura?.Invoke(ev);
        }
    }

    public void Dispose()
    {
        _timer.Stop();
        _timer.Tick -= OnTimerTick;
        _watcher.Line -= OnLine;
        _watcher.Dispose();
    }
}
