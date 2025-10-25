using System;
using System.Collections.Generic;
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
    private bool _suppressBuffEvents;
    private static readonly Regex NumericRegex = new(@"^[0-9.\-]+$");

    public SettingsWindow(AppController controller)
    {
        InitializeComponent();
        _controller = controller;

        PresetComboBox.ItemsSource = BuffCatalog.Presets;
        BuffsItemsControl.ItemsSource = BuffCatalog.Buffs;

        LoadSettings();
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

        RefreshStatus();
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

    private void OnOverlaySelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        _selectedOverlay = OverlayListBox.SelectedItem as OverlaySettings;
        UpdateOverlayDetails();
    }

    private void UpdateOverlayDetails()
    {
        if (_selectedOverlay == null)
        {
            OverlayNameTextBox.Text = string.Empty;
            OverlayLeftTextBox.Text = string.Empty;
            OverlayTopTextBox.Text = string.Empty;
            OverlayWidthTextBox.Text = string.Empty;
            OverlayHeightTextBox.Text = string.Empty;
            SetBuffChecks(Array.Empty<int>());
            return;
        }

        OverlayNameTextBox.Text = _selectedOverlay.Name;
        OverlayLeftTextBox.Text = _selectedOverlay.Left.ToString("F0", CultureInfo.InvariantCulture);
        OverlayTopTextBox.Text = _selectedOverlay.Top.ToString("F0", CultureInfo.InvariantCulture);
        OverlayWidthTextBox.Text = _selectedOverlay.Width.ToString("F0", CultureInfo.InvariantCulture);
        OverlayHeightTextBox.Text = _selectedOverlay.Height.ToString("F0", CultureInfo.InvariantCulture);
        SetBuffChecks(_selectedOverlay.TrackedSpellIds);
    }

    private void SetBuffChecks(IEnumerable<int> spellIds)
    {
        var set = new HashSet<int>(spellIds);
        BuffsItemsControl.UpdateLayout();
        _suppressBuffEvents = true;
        try
        {
            foreach (var child in FindVisualChildren<System.Windows.Controls.CheckBox>(BuffsItemsControl))
            {
                if (child.Tag is int id)
                {
                    child.IsChecked = set.Contains(id);
                }
            }
        }
        finally
        {
            _suppressBuffEvents = false;
        }
    }

    private static IEnumerable<T> FindVisualChildren<T>(DependencyObject parent) where T : DependencyObject
    {
        if (parent == null) yield break;
        for (int i = 0; i < System.Windows.Media.VisualTreeHelper.GetChildrenCount(parent); i++)
        {
            var child = System.Windows.Media.VisualTreeHelper.GetChild(parent, i);
            if (child is T t)
            {
                yield return t;
            }

            foreach (var descendant in FindVisualChildren<T>(child))
            {
                yield return descendant;
            }
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
        }
    }

    private void OnNumericTextInput(object sender, TextCompositionEventArgs e)
    {
        e.Handled = !NumericRegex.IsMatch(e.Text);
    }
}