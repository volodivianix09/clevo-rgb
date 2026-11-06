using ClevoRgbLite.Models;

namespace ClevoRgbLite.Core;

public static class EffectRenderer
{
    public static RgbColor Render(AppSettings settings, double elapsedMs)
    {
        if (!settings.Enabled) return RgbColor.Black;

        var brightness = settings.Brightness / 100.0;
        if (brightness <= 0) return RgbColor.Black;

        var color = RgbColor.FromHex(settings.Color);

        switch (settings.Mode)
        {
            case EffectMode.Off:
                return RgbColor.Black;

            case EffectMode.Static:
                return color.Scale(brightness);

            case EffectMode.Breathing:
            {
                var period = Math.Max(300, settings.PeriodMs);
                var phase = PositiveModulo(elapsedMs, period) / period;
                var factor = 0.5 - 0.5 * Math.Cos(phase * Math.PI * 2);
                return color.Scale(factor * brightness);
            }

            case EffectMode.Rainbow:
            {
                var period = Math.Max(1000, settings.PeriodMs);
                var hue = PositiveModulo(elapsedMs, period) / period;
                return RgbColor.FromHsv(hue, 1, brightness);
            }

            case EffectMode.Pulse:
            {
                var period = Math.Max(400, settings.PeriodMs);
                var phase = PositiveModulo(elapsedMs, period) / period;
                var factor = Math.Exp(-4.4 * phase);
                return color.Scale(factor * brightness);
            }

            case EffectMode.Blink:
            {
                var period = Math.Max(200, settings.PeriodMs);
                var on = PositiveModulo(elapsedMs, period) < period / 2.0;
                return on ? color.Scale(brightness) : RgbColor.Black;
            }

            case EffectMode.Sequence:
                return RenderSequence(settings, elapsedMs, brightness);

            default:
                return RgbColor.Black;
        }
    }

    private static RgbColor RenderSequence(AppSettings settings, double elapsedMs, double brightness)
    {
        var colors = settings.SequenceColors
            .Select(RgbColor.FromHex)
            .ToList();

        if (colors.Count == 0) return RgbColor.Black;
        if (colors.Count == 1) return colors[0].Scale(brightness);

        if (settings.SequenceBreathing)
        {
            var segment = Math.Max(300, settings.SequenceHoldMs);
            var total = segment * colors.Count;
            var local = PositiveModulo(elapsedMs, total);
            var index = (int)(local / segment);
            var phase = (local % segment) / segment;
            var factor = 0.5 - 0.5 * Math.Cos(phase * Math.PI * 2);
            return colors[index].Scale(factor * brightness);
        }

        var hold = Math.Max(300, settings.SequenceHoldMs);
        var transition = Math.Max(0, settings.SequenceTransitionMs);
        var fullSegment = hold + transition;
        var fullTotal = fullSegment * colors.Count;
        var position = PositiveModulo(elapsedMs, fullTotal);
        var segmentIndex = (int)(position / fullSegment);
        var within = position % fullSegment;
        var current = colors[segmentIndex];
        var next = colors[(segmentIndex + 1) % colors.Count];

        if (within < hold || transition == 0)
        {
            return current.Scale(brightness);
        }

        var mix = SmoothStep((within - hold) / transition);
        return RgbColor.Lerp(current, next, mix).Scale(brightness);
    }

    private static double PositiveModulo(double value, double modulus)
    {
        var result = value % modulus;
        return result < 0 ? result + modulus : result;
    }

    private static double SmoothStep(double t)
    {
        t = Math.Clamp(t, 0, 1);
        return t * t * (3 - 2 * t);
    }
}
