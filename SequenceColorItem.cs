using System.ComponentModel;
using System.Windows.Media;
using ClevoRgbLite.Core;

namespace ClevoRgbLite;

public sealed class SequenceColorItem : INotifyPropertyChanged
{
    private RgbColor _color;
    private SolidColorBrush _brush;

    public SequenceColorItem(string hex)
    {
        _color = RgbColor.FromHex(hex);
        _brush = MakeBrush(_color);
    }

    public RgbColor Color
    {
        get => _color;
        set
        {
            if (_color == value) return;
            _color = value;
            _brush = MakeBrush(value);
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Color)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Brush)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Hex)));
        }
    }

    public string Hex => _color.ToHex();
    public SolidColorBrush Brush => _brush;

    public event PropertyChangedEventHandler? PropertyChanged;

    private static SolidColorBrush MakeBrush(RgbColor color)
    {
        var brush = new SolidColorBrush(System.Windows.Media.Color.FromRgb(color.R, color.G, color.B));
        brush.Freeze();
        return brush;
    }
}
