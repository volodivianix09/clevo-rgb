using System.Windows;
using System.Windows.Media;
using ClevoRgbLite.Core;

namespace ClevoRgbLite;

public partial class ColorPickerWindow : Window
{
    private bool _updating;

    public ColorPickerWindow(RgbColor initial)
    {
        InitializeComponent();
        SelectedColor = initial;
        SetControls(initial);
        UpdatePreview();
    }

    public RgbColor SelectedColor { get; private set; }

    private void SetControls(RgbColor color)
    {
        _updating = true;
        try
        {
            RSlider.Value = color.R;
            GSlider.Value = color.G;
            BSlider.Value = color.B;
            HexBox.Text = color.ToHex();
            RValue.Text = color.R.ToString();
            GValue.Text = color.G.ToString();
            BValue.Text = color.B.ToString();
        }
        finally
        {
            _updating = false;
        }
    }

    private void RgbSlider_Changed(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_updating) return;

        var color = new RgbColor(
            (byte)Math.Round(RSlider.Value),
            (byte)Math.Round(GSlider.Value),
            (byte)Math.Round(BSlider.Value));

        SelectedColor = color;
        _updating = true;
        try
        {
            HexBox.Text = color.ToHex();
            RValue.Text = color.R.ToString();
            GValue.Text = color.G.ToString();
            BValue.Text = color.B.ToString();
        }
        finally
        {
            _updating = false;
        }
        UpdatePreview();
    }

    private void HexBox_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
    {
        if (_updating) return;
        if (!TryParseHex(HexBox.Text, out var color)) return;

        SelectedColor = color;
        _updating = true;
        try
        {
            RSlider.Value = color.R;
            GSlider.Value = color.G;
            BSlider.Value = color.B;
            RValue.Text = color.R.ToString();
            GValue.Text = color.G.ToString();
            BValue.Text = color.B.ToString();
        }
        finally
        {
            _updating = false;
        }
        UpdatePreview();
    }

    private void UpdatePreview()
    {
        var color = Color.FromRgb(SelectedColor.R, SelectedColor.G, SelectedColor.B);
        var brush = new SolidColorBrush(color);
        brush.Freeze();
        ColorPreview.Background = brush;
    }

    private static bool TryParseHex(string? text, out RgbColor color)
    {
        color = RgbColor.Black;
        if (string.IsNullOrWhiteSpace(text)) return false;
        text = text.Trim().TrimStart('#');
        if (text.Length != 6) return false;
        if (!int.TryParse(text, System.Globalization.NumberStyles.HexNumber, null, out var value)) return false;
        color = new RgbColor((byte)((value >> 16) & 0xFF), (byte)((value >> 8) & 0xFF), (byte)(value & 0xFF));
        return true;
    }

    private void Ok_Click(object sender, RoutedEventArgs e) => DialogResult = true;

    private void Cancel_Click(object sender, RoutedEventArgs e) => DialogResult = false;
}
