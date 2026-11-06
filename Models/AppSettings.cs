using ClevoRgbLite.Core;

namespace ClevoRgbLite.Models;

public enum EffectMode
{
    Off,
    Static,
    Breathing,
    Rainbow,
    Pulse,
    Blink,
    Sequence
}

public sealed record AppSettings
{
    public bool Enabled { get; init; } = true;
    public EffectMode Mode { get; init; } = EffectMode.Sequence;
    public int Brightness { get; init; } = 82;
    public string Color { get; init; } = "#0066FF";
    public int PeriodMs { get; init; } = 3000;
    public List<string> SequenceColors { get; init; } =
    [
        "#0066FF",
        "#00E5FF",
        "#9D4EDD",
        "#FF2D95"
    ];
    public int SequenceHoldMs { get; init; } = 2000;
    public int SequenceTransitionMs { get; init; } = 700;
    public bool SequenceBreathing { get; init; } = true;
    public bool StartWithWindows { get; init; }

    public AppSettings Normalize()
    {
        var colors = (SequenceColors ?? [])
            .Select(RgbColor.FromHex)
            .Select(c => c.ToHex())
            .ToList();

        if (colors.Count < 2)
        {
            colors = ["#0066FF", "#00E5FF", "#9D4EDD", "#FF2D95"];
        }
        if (colors.Count > 6)
        {
            colors = colors.Take(6).ToList();
        }

        var mode = Enum.IsDefined(Mode) ? Mode : EffectMode.Sequence;

        return this with
        {
            Mode = mode,
            Brightness = Math.Clamp(Brightness, 0, 100),
            Color = RgbColor.FromHex(Color).ToHex(),
            PeriodMs = Math.Clamp(PeriodMs, 500, 30000),
            SequenceColors = colors,
            SequenceHoldMs = Math.Clamp(SequenceHoldMs, 300, 30000),
            SequenceTransitionMs = Math.Clamp(SequenceTransitionMs, 0, 5000)
        };
    }

    public AppSettings Copy() => this with { SequenceColors = [.. SequenceColors] };
}
