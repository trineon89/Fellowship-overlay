using System;
using System.Collections.Specialized;
using System.Windows;
using System.Windows.Controls;
using Fellowship_overlay.Services;
using System.Collections.Specialized;

namespace Fellowship_overlay;

public partial class DebugWindow : Window
{
    private readonly DebugLogService _log;
    private readonly NotifyCollectionChangedEventHandler _onEntriesChanged;

    public DebugWindow(DebugLogService log)
    {
        InitializeComponent();
        _log = log;
        DataContext = _log;
        _onEntriesChanged = (_, args) =>
        {
            if (args?.NewItems == null || args.NewItems.Count == 0) return;
            Dispatcher.BeginInvoke(new Action(() =>
            {
                if (LogListView.Items.Count > 0)
                {
                    LogListView.ScrollIntoView(LogListView.Items[^1]);
                }
            }));
        };

        ((INotifyCollectionChanged)_log.Entries).CollectionChanged += _onEntriesChanged;
    }

    private void OnClear(object sender, RoutedEventArgs e) => _log.Clear();

    protected override void OnClosed(EventArgs e)
    {
        ((INotifyCollectionChanged)_log.Entries).CollectionChanged -= _onEntriesChanged;
        base.OnClosed(e);
    }
}