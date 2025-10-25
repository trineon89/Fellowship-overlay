using System;
using System.Collections.ObjectModel;
using System.Windows;
using Fellowship_overlay.Core;

namespace Fellowship_overlay.Services;

public sealed class DebugLogService : IDebugLog
{
    private const int MaxEntries = 500;
    private readonly ObservableCollection<DebugEntry> _entries = new();
    private readonly ReadOnlyObservableCollection<DebugEntry> _readonlyEntries;

    public DebugLogService()
    {
        _readonlyEntries = new ReadOnlyObservableCollection<DebugEntry>(_entries);
    }

    public ReadOnlyObservableCollection<DebugEntry> Entries => _readonlyEntries;

    public void Log(DateTimeOffset timestamp, string source, string message)
    {
        if (System.Windows.Application.Current.Dispatcher.CheckAccess())
        {
            AppendEntry(timestamp, source, message);
        }
        else
        {
            System.Windows.Application.Current.Dispatcher.BeginInvoke(new Action(() => AppendEntry(timestamp, source, message)));
        }
    }

    private void AppendEntry(DateTimeOffset timestamp, string source, string message)
    {
        _entries.Add(new DebugEntry(timestamp, source, message));
        while (_entries.Count > MaxEntries)
        {
            _entries.RemoveAt(0);
        }
    }

    public void Clear()
    {
        if (System.Windows.Application.Current.Dispatcher.CheckAccess())
        {
            _entries.Clear();
        }
        else
        {
            System.Windows.Application.Current.Dispatcher.BeginInvoke(new Action(() => _entries.Clear()));
        }
    }
}

public sealed record DebugEntry(DateTimeOffset Timestamp, string Source, string Message)
{
    public string TimeDisplay => Timestamp.ToLocalTime().ToString("HH:mm:ss.fff");
}