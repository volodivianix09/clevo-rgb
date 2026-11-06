namespace ClevoRgbLite.Core;

public readonly record struct RgbColor(byte R, byte G, byte B)
{
    public static RgbColor Black => new(0, 0, 0);

    public static RgbColor FromHex(string? hex)
    {
        if (string.IsNullOrWhiteSpace(hex)) return Black;
        hex = hex.Trim();
        if (hex.StartsWith('#')) hex = hex[1..];
        if (hex.Length == 3)
        {
            hex = string.Concat(hex[0], hex[0], hex[1], hex[1], hex[2], hex[2]);
        }
        if (hex.Length != 6 || !int.TryParse(hex, System.Globalization.NumberStyles.HexNumber, null, out var value))
        {
            return Black;
        }
        return new RgbColor((byte)((value >> 16) & 0xFF), (byte)((value >> 8) & 0xFF), (byte)(value & 0xFF));
    }

    public string ToHex() => $"#{R:X2}{G:X2}{B:X2}";

    public RgbColor Scale(double factor)
    {
        factor = Math.Clamp(factor, 0, 1);
        return new RgbColor(
            (byte)Math.Round(R * factor),
            (byte)Math.Round(G * factor),
            (byte)Math.Round(B * factor));
    }

    public static RgbColor Lerp(RgbColor a, RgbColor b, double t)
    {
        t = Math.Clamp(t, 0, 1);
        return new RgbColor(
            (byte)Math.Round(a.R + (b.R - a.R) * t),
            (byte)Math.Round(a.G + (b.G - a.G) * t),
            (byte)Math.Round(a.B + (b.B - a.B) * t));
    }

    public static RgbColor FromHsv(double hue, double saturation, double value)
    {
        hue = ((hue % 1) + 1) % 1;
        saturation = Math.Clamp(saturation, 0, 1);
        value = Math.Clamp(value, 0, 1);

        if (saturation <= 0)
        {
            var gray = (byte)Math.Round(value * 255);
            return new RgbColor(gray, gray, gray);
        }

        var h = hue * 6.0;
        var sector = (int)Math.Floor(h) % 6;
        var f = h - Math.Floor(h);
        var p = value * (1 - saturation);
        var q = value * (1 - saturation * f);
        var t = value * (1 - saturation * (1 - f));

        var (r, g, b) = sector switch
        {
            0 => (value, t, p),
            1 => (q, value, p),
            2 => (p, value, t),
            3 => (p, q, value),
            4 => (t, p, value),
            _ => (value, p, q)
        };

        return new RgbColor(
            (byte)Math.Round(r * 255),
            (byte)Math.Round(g * 255),
            (byte)Math.Round(b * 255));
    }
}
