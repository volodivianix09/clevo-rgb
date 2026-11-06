using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Threading;
using ClevoRgbLite.Core;
using ClevoRgbLite.Models;

namespace ClevoRgbLite;

public partial class MainWindow : Window
{
    private static readonly (string Hex, string Name)[] Palette =
    [
        ("#FFFFFF", "Белый"),
        ("#FFE7C2", "Тёплый белый"),
        ("#FF3B30", "Красный"),
        ("#FF6B22", "Оранжевый"),
        ("#FFB300", "Янтарный"),
        ("#FFD60A", "Жёлтый"),
        ("#C6FF00", "Лайм"),
        ("#34C759", "Зелёный"),
        ("#00E676", "Мята"),
        ("#00C7B7", "Бирюзовый"),
        ("#00E5FF", "Циан"),
        ("#29B6F6", "Небесный"),
        ("#0066FF", "Синий"),
        ("#3D5AFE", "Индиго"),
        ("#7C4DFF", "Фиолетовый"),
        ("#9D4EDD", "Пурпурный"),
        ("#D500F9", "Маджента"),
        ("#FF2D95", "Розовый"),
        ("#FF5C8A", "Роза"),
        ("#FF8A80", "Коралл"),
        ("#8D6E63", "Коричневый"),
        ("#607D8B", "Серо-синий"),
        ("#9E9E9E", "Серый"),
        ("#000000", "Чёрный")
    ];

    private readonly App _app;
    private readonly ObservableCollection<SequenceColorItem> _sequenceColors = [];
    private readonly List<Button> _paletteButtons = [];
    private readonly DispatcherTimer _previewTimer;
    private readonly DispatcherTimer _statusTimer;
    private readonly Stopwatch _previewClock = Stopwatch.StartNew();

    private AppSettings _settings;
    private RgbColor _mainColor;
    private bool _loading = true;
    private bool _applying;

    public MainWindow()
    {
        InitializeComponent();
        _app = (App)Application.Current;
        _settings = _app.Settings.Copy();
        _mainColor = RgbColor.FromHex(_settings.Color);

        SequenceList.ItemsSource = _sequenceColors;
        BuildPalette();

        _previewTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(80) };
        _previewTimer.Tick += (_, _) => UpdatePreview();
        _statusTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(1600) };
        _statusTimer.Tick += (_, _) =>
        {
            _statusTimer.Stop();
            SaveStatus.Text = "Изменения применяются и сохраняются автоматически.";
        };

        _app.SettingsChanged += App_SettingsChanged;
        _app.DriverStateChanged += App_DriverStateChanged;
        _loading = false;
    }

    private void Window_Loaded(object sender, RoutedEventArgs e)
    {
        LoadSettingsToUi();
        UpdateDriverStatus();
        _previewTimer.Start();
    }

    private void BuildPalette()
    {
        foreach (var (hex, name) in Palette)
        {
            var color = RgbColor.FromHex(hex);
            var button = new Button
            {
                Style = (Style)FindResource("PaletteButtonStyle"),
                Background = BrushFrom(color),
                Tag = hex,
                ToolTip = name
            };
            button.Click += Palette_Click;
            _paletteButtons.Add(button);
            PalettePanel.Children.Add(button);
        }
    }

    private void LoadSettingsToUi()
    {
        _loading = true;
        try
        {
            _settings = _app.Settings.Copy();
            _mainColor = RgbColor.FromHex(_settings.Color);

            EnabledCheck.IsChecked = _settings.Enabled;
            BrightnessSlider.Value = _settings.Brightness;
            BrightnessValue.Text = $"{_settings.Brightness}%";
            AutostartCheck.IsChecked = _settings.StartWithWindows;

            foreach (var radio in ModePanel.Children.OfType<RadioButton>())
            {
                radio.IsChecked = string.Equals(radio.Tag as string, _settings.Mode.ToString(),
                    StringComparison.OrdinalIgnoreCase);
            }

            _sequenceColors.Clear();
            foreach (var hex in _settings.SequenceColors)
            {
                _sequenceColors.Add(new SequenceColorItem(hex));
            }
            if (_sequenceColors.Count > 0)
            {
                SequenceList.SelectedIndex = 0;
            }

            SequenceHoldSlider.Value = _settings.SequenceHoldMs;
            TransitionSlider.Value = _settings.SequenceTransitionMs;
            SequenceBreathingCheck.IsChecked = _settings.SequenceBreathing;

            UpdateModeUi();
            UpdateColorPreview();
            UpdatePreview();
        }
        finally
        {
            _loading = false;
        }
    }

    private void UpdateModeUi()
    {
        var wasLoading = _loading;
        _loading = true;
        try
        {
            var mode = _settings.Mode;
            SequencePanel.Visibility = mode == EffectMode.Sequence ? Visibility.Visible : Visibility.Collapsed;
            TransitionPanel.Visibility = _settings.SequenceBreathing ? Visibility.Collapsed : Visibility.Visible;

            SequenceHoldValue.Text = FormatSeconds(_settings.SequenceHoldMs);
            TransitionValue.Text = FormatSeconds(_settings.SequenceTransitionMs);

            var showSpeed = mode is EffectMode.Breathing or EffectMode.Rainbow or EffectMode.Pulse or EffectMode.Blink;
            SpeedPanel.Visibility = showSpeed ? Visibility.Visible : Visibility.Collapsed;

            if (showSpeed)
            {
                var (min, max, label) = mode switch
                {
                    EffectMode.Breathing => (1000, 10000, "Период дыхания"),
                    EffectMode.Rainbow => (2000, 20000, "Скорость радуги"),
                    EffectMode.Pulse => (600, 5000, "Период пульса"),
                    _ => (200, 3000, "Период мигания")
                };
                SpeedSlider.Minimum = min;
                SpeedSlider.Maximum = max;
                SpeedSlider.Value = Math.Clamp(_settings.PeriodMs, min, max);
                SpeedLabel.Text = label;
                SpeedValue.Text = FormatSeconds((int)SpeedSlider.Value);
            }

            ColorHint.Text = mode == EffectMode.Sequence
                ? "Нажмите на цвет в списке, затем выберите другой в палитре."
                : "Выберите цвет из палитры.";

            ModeHint.Text = mode switch
            {
                EffectMode.Off => "Подсветка выключена.",
                EffectMode.Static => "Все клавиши светятся выбранным цветом.",
                EffectMode.Breathing => "Цвет плавно разгорается и затухает.",
                EffectMode.Rainbow => "Оттенки плавно сменяют друг друга.",
                EffectMode.Pulse => "Яркая вспышка с плавным затуханием.",
                EffectMode.Blink => "Клавиатура резко включается и выключается.",
                _ => "Цвета сменяются по кругу. Можно добавить до 6 цветов."
            };
        }
        finally
        {
            _loading = wasLoading;
        }
    }

    private void ApplySettings()
    {
        _settings = _settings.Normalize();
        _applying = true;
        try
        {
            _app.UpdateSettings(_settings);
            _settings = _app.Settings.Copy();
        }
        finally
        {
            _applying = false;
        }

        SaveStatus.Text = "Сохранено";
        _statusTimer.Stop();
        _statusTimer.Start();
        UpdatePreview();
    }

    private void App_SettingsChanged(object? sender, EventArgs e)
    {
        if (_applying || _loading) return;
        LoadSettingsToUi();
    }

    private void App_DriverStateChanged(object? sender, EventArgs e)
    {
        Dispatcher.BeginInvoke(UpdateDriverStatus);
    }

    private void UpdateDriverStatus()
    {
        if (_app.DriverAvailable)
        {
            StatusPill.Background = BrushFromHex("#12251A");
            StatusPill.BorderBrush = BrushFromHex("#1E4A2E");
            StatusDot.Fill = BrushFromHex("#42D392");
            StatusText.Foreground = BrushFromHex("#8BE8B5");
            StatusText.Text = "Подключено";
            StatusPill.ToolTip = "Управление подсветкой работает.";
        }
        else
        {
            StatusPill.Background = BrushFromHex("#2A1416");
            StatusPill.BorderBrush = BrushFromHex("#5A2226");
            StatusDot.Fill = BrushFromHex("#FF5A5F");
            StatusText.Foreground = BrushFromHex("#FFB3B6");
            StatusText.Text = "Не найдено";
            StatusPill.ToolTip = _app.DriverError ?? "Не удалось загрузить драйвер подсветки.";
        }
    }

    private void Enabled_Changed(object sender, RoutedEventArgs e)
    {
        if (_loading) return;
        var enabled = EnabledCheck.IsChecked == true;
        _settings = _settings with { Enabled = enabled };

        _applying = true;
        try
        {
            _app.SetEnabled(enabled);
            _settings = _app.Settings.Copy();
        }
        finally
        {
            _applying = false;
        }

        SaveStatus.Text = "Сохранено";
        _statusTimer.Stop();
        _statusTimer.Start();
        UpdatePreview();
    }

    private void Mode_Checked(object sender, RoutedEventArgs e)
    {
        if (_loading || sender is not RadioButton radio || radio.Tag is not string tag) return;
        if (!Enum.TryParse<EffectMode>(tag, out var mode)) return;

        _settings = _settings with { Mode = mode };
        if (mode == EffectMode.Sequence && _sequenceColors.Count == 0)
        {
            foreach (var hex in _settings.SequenceColors)
            {
                _sequenceColors.Add(new SequenceColorItem(hex));
            }
            SequenceList.SelectedIndex = 0;
        }

        ApplySettings();
        UpdateModeUi();
        UpdateColorPreview();
    }

    private void Brightness_Changed(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_loading) return;
        var value = (int)Math.Round(BrightnessSlider.Value);
        BrightnessValue.Text = $"{value}%";
        _settings = _settings with { Brightness = value };
        ApplySettings();
    }

    private void Speed_Changed(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_loading) return;
        var value = (int)Math.Round(SpeedSlider.Value);
        SpeedValue.Text = FormatSeconds(value);
        _settings = _settings with { PeriodMs = value };
        ApplySettings();
    }

    private void SequenceHold_Changed(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_loading) return;
        var value = (int)Math.Round(SequenceHoldSlider.Value);
        SequenceHoldValue.Text = FormatSeconds(value);
        _settings = _settings with { SequenceHoldMs = value };
        ApplySettings();
    }

    private void Transition_Changed(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_loading) return;
        var value = (int)Math.Round(TransitionSlider.Value);
        TransitionValue.Text = FormatSeconds(value);
        _settings = _settings with { SequenceTransitionMs = value };
        ApplySettings();
    }

    private void SequenceBreathing_Changed(object sender, RoutedEventArgs e)
    {
        if (_loading) return;
        var breathing = SequenceBreathingCheck.IsChecked == true;
        _settings = _settings with { SequenceBreathing = breathing };
        TransitionPanel.Visibility = breathing ? Visibility.Collapsed : Visibility.Visible;
        ApplySettings();
    }

    private void Autostart_Changed(object sender, RoutedEventArgs e)
    {
        if (_loading) return;
        var enabled = AutostartCheck.IsChecked == true;
        _settings = _settings with { StartWithWindows = enabled };

        _applying = true;
        try
        {
            _app.SetAutostart(enabled);
            _settings = _app.Settings.Copy();
        }
        finally
        {
            _applying = false;
        }

        SaveStatus.Text = "Сохранено";
        _statusTimer.Stop();
        _statusTimer.Start();
    }

    private void Palette_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button button || button.Tag is not string hex) return;
        var color = RgbColor.FromHex(hex);
        ApplyColor(color);
    }

    private void ApplyColor(RgbColor color)
    {
        _mainColor = color;

        if (_settings.Mode == EffectMode.Sequence)
        {
            if (SequenceList.SelectedItem is not SequenceColorItem item)
            {
                if (_sequenceColors.Count == 0) return;
                SequenceList.SelectedIndex = 0;
                item = _sequenceColors[0];
            }
            item.Color = color;
            _settings = _settings with
            {
                Color = color.ToHex(),
                SequenceColors = _sequenceColors.Select(c => c.Hex).ToList()
            };
        }
        else
        {
            _settings = _settings with { Color = color.ToHex() };
        }

        ApplySettings();
        UpdateColorPreview();
    }

    private void CustomColor_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new ColorPickerWindow(GetActiveColor()) { Owner = this };
        if (dialog.ShowDialog() == true)
        {
            ApplyColor(dialog.SelectedColor);
        }
    }

    private void SequenceList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_loading) return;
        if (SequenceList.SelectedItem is SequenceColorItem item)
        {
            _mainColor = item.Color;
            _settings = _settings with { Color = item.Hex };
            UpdateColorPreview();
        }
    }

    private void AddSequenceColor_Click(object sender, RoutedEventArgs e)
    {
        if (_sequenceColors.Count >= 6) return;
        var item = new SequenceColorItem(GetActiveColor().ToHex());
        _sequenceColors.Add(item);
        SequenceList.SelectedItem = item;
        _settings = _settings with { SequenceColors = _sequenceColors.Select(c => c.Hex).ToList() };
        ApplySettings();
    }

    private void RemoveSequenceColor_Click(object sender, RoutedEventArgs e)
    {
        if (_sequenceColors.Count <= 2) return;
        var index = Math.Max(0, SequenceList.SelectedIndex);
        _sequenceColors.RemoveAt(index);
        SequenceList.SelectedIndex = Math.Min(index, _sequenceColors.Count - 1);
        _settings = _settings with { SequenceColors = _sequenceColors.Select(c => c.Hex).ToList() };
        ApplySettings();
    }

    private RgbColor GetActiveColor()
    {
        if (_settings.Mode == EffectMode.Sequence && SequenceList.SelectedItem is SequenceColorItem item)
        {
            return item.Color;
        }
        return _mainColor;
    }

    private void UpdateColorPreview()
    {
        var color = GetActiveColor();
        SelectedColorPreview.Background = BrushFrom(color);
        UpdatePaletteSelection(color.ToHex());
    }

    private void UpdatePaletteSelection(string selectedHex)
    {
        foreach (var button in _paletteButtons)
        {
            var isSelected = string.Equals(button.Tag as string, selectedHex, StringComparison.OrdinalIgnoreCase);
            if (isSelected)
            {
                button.BorderBrush = Brushes.White;
            }
            else
            {
                button.ClearValue(Control.BorderBrushProperty);
            }
        }
    }

    private void UpdatePreview()
    {
        if (!IsVisible) return;

        var color = EffectRenderer.Render(_settings, _previewClock.Elapsed.TotalMilliseconds);
        PreviewGlow.Background = BrushFrom(color);
        if (PreviewGlow.Effect is DropShadowEffect shadow)
        {
            shadow.Color = Color.FromRgb(color.R, color.G, color.B);
        }

        PreviewModeText.Text = !_settings.Enabled
            ? "Выключено"
            : _settings.Mode switch
            {
                EffectMode.Off => "Выключено",
                EffectMode.Static => "Один цвет",
                EffectMode.Breathing => "Дыхание",
                EffectMode.Rainbow => "Радуга",
                EffectMode.Pulse => "Пульс",
                EffectMode.Blink => "Мигание",
                _ => "Смена цветов"
            };
    }

    private void Hide_Click(object sender, RoutedEventArgs e) => Hide();

    private void Window_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        if (_app.IsExiting) return;
        e.Cancel = true;
        Hide();
    }

    private static SolidColorBrush BrushFrom(RgbColor color)
    {
        var brush = new SolidColorBrush(Color.FromRgb(color.R, color.G, color.B));
        brush.Freeze();
        return brush;
    }

    private static SolidColorBrush BrushFromHex(string hex)
    {
        var color = (Color)ColorConverter.ConvertFromString(hex);
        var brush = new SolidColorBrush(color);
        brush.Freeze();
        return brush;
    }

    private static string FormatSeconds(int milliseconds) =>
        string.Format(CultureInfo.CurrentCulture, "{0:0.0} с", milliseconds / 1000.0);
}
