using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Fellowship_overlay.Core;
using Fellowship_overlay.Services;
using WinForms = System.Windows.Forms;

namespace Fellowship_overlay;

public partial class SettingsWindow : Window
{
    private readonly AppController _controller;
    private OverlaySettings? _selectedOverlay;
	private readonly List<BuffSelection> _buffSelections;
    private bool _suppressBuffEvents;
    private bool _suppressOverlayEvents;
    private bool _suppressGeneralEvents;
    private static readonly Regex NumericRegex = new(@"^[0-9.\-]+$");

    public SettingsWindow(AppController controller)
    {
        InitializeComponent();
        _controller = controller;

        PresetComboBox.ItemsSource = BuffCatalog.Presets;
        _buffSelections = BuffCatalog.Buffs.Select(definition => new BuffSelection(definition)).ToList();
        BuffsItemsControl.ItemsSource = _buffSelections;

        LoadSettings();
        _controller.OverlaySettingsChanged += OnControllerOverlaySettingsChanged;
        _controller.DebugEnabledChanged += OnDebugEnabledChanged;
        Loaded += OnSettingsWindowLoaded;
    }

    private void LoadSettings()
    {
        LogDirTextBox.Text = _controller.Settings.LogDirectory ?? string.Empty;
        PlayerNameTextBox.Text = _controller.Settings.PlayerName ?? string.Empty;
        PlayerGuidTextBox.Text = _controller.Settings.PlayerGuid ?? string.Empty;

        OverlayListBox.ItemsSource = _controller.Settings.Overlays;
        if (_controller.Settings.Overlays.Count > 0)
        {
            OverlayListBox.SelectedIndex = 0;
        }

        _suppressGeneralEvents = true;
        LockOverlaysCheckBox.IsChecked = _controller.Settings.OverlaysLocked;
        EnableDebugCheckBox.IsChecked = _controller.Settings.DebugEnabled;
        _suppressGeneralEvents = false;

        RefreshStatus();
    }
	
	private void OnShowAbout(object sender, RoutedEventArgs e)
    {
        var aboutWindow = new AboutWindow
        {
            Owner = this
        };

        aboutWindow.ShowDialog();
    }

    private void RefreshStatus()
    {
        if (_controller.ValidateGeneralSettings(out var message))
        {
            StatusTextBlock.Text = "Ready";
        }
        else
        {
            StatusTextBlock.Text = message;
        }
    }

    private void OnBrowseLogDir(object sender, RoutedEventArgs e)
    {
        using var dialog = new WinForms.FolderBrowserDialog
        {
            Description = "Choose your Fellowship combat-log folder"
        };

        if (!string.IsNullOrEmpty(LogDirTextBox.Text) && System.IO.Directory.Exists(LogDirTextBox.Text))
        {
            dialog.SelectedPath = LogDirTextBox.Text;
        }

        if (dialog.ShowDialog() == WinForms.DialogResult.OK && System.IO.Directory.Exists(dialog.SelectedPath))
        {
            LogDirTextBox.Text = dialog.SelectedPath;
        }
    }

    private void OnApply(object sender, RoutedEventArgs e)
    {
        _controller.UpdateGeneralSettings(LogDirTextBox.Text, PlayerNameTextBox.Text, PlayerGuidTextBox.Text);
        _controller.SaveSettings();
        RefreshStatus();
        System.Windows.MessageBox.Show(this, "Settings saved.", "Fellowship Overlay", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void OnSettingsWindowLoaded(object sender, RoutedEventArgs e)
    {
        if (_controller.Settings.DebugEnabled)
        {
            _controller.SetDebugEnabled(true, this);
        }
    }

    private void OnOverlaySelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        _selectedOverlay = OverlayListBox.SelectedItem as OverlaySettings;
        UpdateOverlayDetails();
    }

    private void UpdateOverlayDetails()
    {
            _suppressOverlayEvents = true;
        try
        {
            if (_selectedOverlay == null)
            {
                OverlayNameTextBox.Text = string.Empty;
                OverlayLeftTextBox.Text = string.Empty;
                OverlayTopTextBox.Text = string.Empty;
                OverlayWidthTextBox.Text = string.Empty;
                OverlayHeightTextBox.Text = string.Empty;
                ShowIconsOnlyCheckBox.IsChecked = false;
                BuffAreaTextBlock.Text = "Not set";
                UpdateBuffSelections(Array.Empty<int>());
                return;
            }

            OverlayNameTextBox.Text = _selectedOverlay.Name;
            OverlayLeftTextBox.Text = _selectedOverlay.Left.ToString("F0", CultureInfo.InvariantCulture);
            OverlayTopTextBox.Text = _selectedOverlay.Top.ToString("F0", CultureInfo.InvariantCulture);
            OverlayWidthTextBox.Text = _selectedOverlay.Width.ToString("F0", CultureInfo.InvariantCulture);
            OverlayHeightTextBox.Text = _selectedOverlay.Height.ToString("F0", CultureInfo.InvariantCulture);
            ShowIconsOnlyCheckBox.IsChecked = _selectedOverlay.ShowIconsOnly;
            UpdateBuffAreaText();
            UpdateBuffSelections(_selectedOverlay.TrackedSpellIds);
        }
        finally
        {
            _suppressOverlayEvents = false;
        }
    }

    private void UpdateBuffSelections(IEnumerable<int> spellIds)
    {
        var set = new HashSet<int>(spellIds);
        _suppressBuffEvents = true;
        try
        {
            foreach (var selection in _buffSelections)
            {
                selection.IsTracked = set.Contains(selection.Definition.SpellId);
            }
        }
        finally
        {
            _suppressBuffEvents = false;
        }
    }

    private void OnAddOverlay(object sender, RoutedEventArgs e)
    {
        var preset = PresetComboBox.SelectedItem as BuffPreset;
        var overlay = _controller.AddOverlay(preset);
        OverlayListBox.Items.Refresh();
        OverlayListBox.SelectedItem = overlay;
        RefreshStatus();
    }

    private void OnRemoveOverlay(object sender, RoutedEventArgs e)
    {
        if (_selectedOverlay == null)
        {
            return;
        }

        if (!_controller.RemoveOverlay(_selectedOverlay.Id))
        {
            System.Windows.MessageBox.Show(this, "At least one overlay must remain.", "Fellowship Overlay", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        OverlayListBox.Items.Refresh();
        _selectedOverlay = OverlayListBox.SelectedItem as OverlaySettings;
        UpdateOverlayDetails();
        RefreshStatus();
    }

    private void OnOverlayNameChanged(object sender, TextChangedEventArgs e)
    {
        if (_selectedOverlay == null) return;
        _selectedOverlay.Name = OverlayNameTextBox.Text.Trim();
        _controller.UpdateOverlay(_selectedOverlay);
        OverlayListBox.Items.Refresh();
    }

    private void OnOverlayPositionChanged(object sender, RoutedEventArgs e)
    {
        if (_suppressOverlayEvents) return;
        if (_selectedOverlay == null) return;
        if (double.TryParse(OverlayLeftTextBox.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out var left))
        {
            _selectedOverlay.Left = left;
        }

        if (double.TryParse(OverlayTopTextBox.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out var top))
        {
            _selectedOverlay.Top = top;
        }

        _controller.UpdateOverlay(_selectedOverlay);
    }

    private void OnOverlaySizeChanged(object sender, RoutedEventArgs e)
    {
        if (_suppressOverlayEvents) return;
        if (_selectedOverlay == null) return;
        if (double.TryParse(OverlayWidthTextBox.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out var width))
        {
            _selectedOverlay.Width = Math.Max(200, width);
        }

        if (double.TryParse(OverlayHeightTextBox.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out var height))
        {
            _selectedOverlay.Height = Math.Max(120, height);
        }

        _controller.UpdateOverlay(_selectedOverlay);
    }
	
	private void OnOverlayDisplayModeChanged(object sender, RoutedEventArgs e)
    {
        if (_suppressOverlayEvents) return;
        if (_selectedOverlay == null) return;
        _selectedOverlay.ShowIconsOnly = ShowIconsOnlyCheckBox.IsChecked == true;
        _controller.UpdateOverlay(_selectedOverlay);
    }
	
	private void UpdateBuffAreaText()
    {
        if (_selectedOverlay?.BuffCaptureRegion is { } region && region.IsValid)
        {
            BuffAreaTextBlock.Text = $"{region.Left:F0},{region.Top:F0} — {region.Width:F0}×{region.Height:F0}";
        }
        else
        {
            BuffAreaTextBlock.Text = "Not set";
        }
    }

    private void OnCastBuffArea(object sender, RoutedEventArgs e)
    {
        if (_selectedOverlay == null)
        {
            return;
        }

        var selector = new CaptureRegionWindow
        {
            Owner = this
        };

        if (selector.ShowDialog() == true && selector.SelectedRegion is { } rect)
        {
            _selectedOverlay.BuffCaptureRegion = new CaptureRegionSettings
            {
                Left = rect.X,
                Top = rect.Y,
                Width = rect.Width,
                Height = rect.Height
            };
            UpdateBuffAreaText();
            _controller.UpdateOverlay(_selectedOverlay);
        }
    }

    private void OnBuffSelectionChanged(object sender, RoutedEventArgs e)
    {
        if (_selectedOverlay == null) return;
        if (sender is System.Windows.Controls.CheckBox cb && cb.Tag is int spellId)
        {
            if (_suppressBuffEvents) return;
            if (cb.IsChecked == true)
            {
                if (!_selectedOverlay.TrackedSpellIds.Contains(spellId))
                {
                    _selectedOverlay.TrackedSpellIds.Add(spellId);
                }
            }
            else
            {
                _selectedOverlay.TrackedSpellIds.Remove(spellId);
            }

            _controller.UpdateOverlay(_selectedOverlay);
            _controller.SaveSettings();
        }
    }

    private void OnNumericTextInput(object sender, TextCompositionEventArgs e)
    {
        e.Handled = !NumericRegex.IsMatch(e.Text);
    }

    private void OnLockOverlaysChanged(object sender, RoutedEventArgs e)
    {
        if (_suppressGeneralEvents) return;
        _controller.SetOverlaysLocked(LockOverlaysCheckBox.IsChecked == true);
        _controller.SaveSettings();
    }

    private void OnDebugModeChanged(object sender, RoutedEventArgs e)
    {
        if (_suppressGeneralEvents) return;
        _controller.SetDebugEnabled(EnableDebugCheckBox.IsChecked == true, this);
        _controller.SaveSettings();
    }

    private void OnControllerOverlaySettingsChanged(object? sender, OverlaySettings e)
    {
        if (!Dispatcher.CheckAccess())
        {
            Dispatcher.Invoke(() => OnControllerOverlaySettingsChanged(sender, e));
            return;
        }

        if (_selectedOverlay == null || e.Id != _selectedOverlay.Id)
        {
            return;
        }

        _suppressOverlayEvents = true;
        try
        {
            OverlayLeftTextBox.Text = e.Left.ToString("F0", CultureInfo.InvariantCulture);
            OverlayTopTextBox.Text = e.Top.ToString("F0", CultureInfo.InvariantCulture);
            OverlayWidthTextBox.Text = e.Width.ToString("F0", CultureInfo.InvariantCulture);
            OverlayHeightTextBox.Text = e.Height.ToString("F0", CultureInfo.InvariantCulture);
            ShowIconsOnlyCheckBox.IsChecked = e.ShowIconsOnly;
            UpdateBuffAreaText();
            UpdateBuffSelections(e.TrackedSpellIds);
        }
        finally
        {
            _suppressOverlayEvents = false;
        }
    }

    private void OnDebugEnabledChanged(object? sender, bool enabled)
    {
        if (!Dispatcher.CheckAccess())
        {
            Dispatcher.Invoke(() => OnDebugEnabledChanged(sender, enabled));
            return;
        }

        _suppressGeneralEvents = true;
        EnableDebugCheckBox.IsChecked = enabled;
        _suppressGeneralEvents = false;
    }

    protected override void OnClosed(EventArgs e)
    {
        _controller.OverlaySettingsChanged -= OnControllerOverlaySettingsChanged;
        _controller.DebugEnabledChanged -= OnDebugEnabledChanged;
        base.OnClosed(e);
        System.Windows.Application.Current.Shutdown();
    }
	
    private sealed class BuffSelection : INotifyPropertyChanged
    {
        public BuffDefinition Definition { get; }

        private bool _isTracked;

        public bool IsTracked
        {
            get => _isTracked;
            set
            {
                if (_isTracked == value) return;
                _isTracked = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsTracked)));
            }
        }

        public BuffSelection(BuffDefinition definition)
        {
            Definition = definition;
        }

        public event PropertyChangedEventHandler? PropertyChanged;
    }
}