using System.Text.Json;
using System.Text.Json.Serialization;
using ClevoRgbLite.Models;

namespace ClevoRgbLite.Services;

public sealed class SettingsStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() }
    };

    public static string DirectoryPath => AppLogger.DirectoryPath;
    public static string SettingsPath => Path.Combine(DirectoryPath, "settings.json");
    public static string OldSettingsPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
        "ClevoLEDKeyboardControl",
        "settings.json");

    public AppSettings Load()
    {
        AppSettings settings;

        if (File.Exists(SettingsPath))
        {
            try
            {
                var json = File.ReadAllText(SettingsPath);
                settings = JsonSerializer.Deserialize<AppSettings>(json, JsonOptions) ?? new AppSettings();
            }
            catch (Exception ex)
            {
                AppLogger.Log("Не удалось прочитать настройки, используются значения по умолчанию.", ex);
                settings = new AppSettings();
            }
        }
        else
        {
            settings = TryImportOldSettings() ?? new AppSettings();
            settings = settings.Normalize();
            Save(settings);
        }

        settings = settings.Normalize() with { StartWithWindows = Autostart.IsEnabled() };
        return settings;
    }

    public void Save(AppSettings settings)
    {
        try
        {
            Directory.CreateDirectory(DirectoryPath);
            var json = JsonSerializer.Serialize(settings.Normalize(), JsonOptions);
            var temp = SettingsPath + ".tmp";
            File.WriteAllText(temp, json);
            File.Move(temp, SettingsPath, overwrite: true);
        }
        catch (Exception ex)
        {
            AppLogger.Log("Не удалось сохранить настройки.", ex);
        }
    }

    private static AppSettings? TryImportOldSettings()
    {
        try
        {
            if (!File.Exists(OldSettingsPath)) return null;
            using var document = JsonDocument.Parse(File.ReadAllText(OldSettingsPath));
            var root = document.RootElement;
            var effect = GetProperty(root, "Effect");
            if (effect is null) return null;

            var typeName = GetString(effect.Value, "Type") ?? "Sequence";
            var mode = MapMode(typeName);
            var sequence = GetArray(effect.Value, "Sequence");
            var colors = new List<string>();
            var hold = 2000;
            var transition = 700;
            var breathing = true;

            if (sequence is not null)
            {
                var first = true;
                foreach (var item in sequence.Value.EnumerateArray())
                {
                    var color = GetString(item, "Color");
                    if (string.IsNullOrWhiteSpace(color)) continue;
                    colors.Add(color);
                    if (first)
                    {
                        hold = GetInt(item, "HoldMs") ?? hold;
                        transition = GetInt(item, "TransitionMs") ?? transition;
                        breathing = GetBool(item, "Breathing") ?? breathing;
                        first = false;
                    }
                }
            }

            if (colors.Count == 0)
            {
                colors.Add(GetString(effect.Value, "Color") ?? "#0066FF");
                colors.Add("#00E5FF");
            }
            if (colors.Count == 1)
            {
                colors.Add(colors[0]);
            }

            var imported = new AppSettings
            {
                Enabled = GetBool(root, "Enabled") ?? true,
                Mode = mode,
                Brightness = GetInt(root, "Brightness") ?? 82,
                Color = colors[0],
                PeriodMs = GetInt(effect.Value, "PeriodMs") ?? 3000,
                SequenceColors = colors,
                SequenceHoldMs = hold,
                SequenceTransitionMs = transition,
                SequenceBreathing = breathing
            };

            AppLogger.Log($"Импортированы старые настройки: режим {mode}, цветов {colors.Count}.");
            return imported.Normalize();
        }
        catch (Exception ex)
        {
            AppLogger.Log("Не удалось импортировать старые настройки.", ex);
            return null;
        }
    }

    private static EffectMode MapMode(string type)
    {
        return type.Trim().ToLowerInvariant() switch
        {
            "off" => EffectMode.Off,
            "static" => EffectMode.Static,
            "breathing" => EffectMode.Breathing,
            "rainbow" => EffectMode.Rainbow,
            "pulse" or "heartbeat" => EffectMode.Pulse,
            "blink" => EffectMode.Blink,
            "sequence" or "music" => EffectMode.Sequence,
            _ => EffectMode.Sequence
        };
    }

    private static JsonElement? GetProperty(JsonElement element, string name)
    {
        if (element.ValueKind != JsonValueKind.Object) return null;
        foreach (var property in element.EnumerateObject())
        {
            if (string.Equals(property.Name, name, StringComparison.OrdinalIgnoreCase))
            {
                return property.Value;
            }
        }
        return null;
    }

    private static JsonElement? GetArray(JsonElement element, string name)
    {
        var property = GetProperty(element, name);
        return property is { ValueKind: JsonValueKind.Array } ? property : null;
    }

    private static string? GetString(JsonElement element, string name)
    {
        var property = GetProperty(element, name);
        return property is { ValueKind: JsonValueKind.String } ? property.Value.GetString() : null;
    }

    private static int? GetInt(JsonElement element, string name)
    {
        var property = GetProperty(element, name);
        return property is { ValueKind: JsonValueKind.Number } && property.Value.TryGetInt32(out var value)
            ? value
            : null;
    }

    private static bool? GetBool(JsonElement element, string name)
    {
        var property = GetProperty(element, name);
        return property?.ValueKind switch
        {
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            _ => null
        };
    }
}
