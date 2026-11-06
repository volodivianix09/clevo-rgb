using System.Runtime.InteropServices;
using ClevoRgbLite.Services;

namespace ClevoRgbLite.Core;

public sealed class LedDriver
{
    private const int DchuCommand = 0x67;
    private const int SuccessCode = 103;
    private const string DriverFileName = "InsydeDCHU.dll";

    private static readonly object ResolverLock = new();
    private static bool _resolverInstalled;
    private static string? _resolvedDllPath;
    private static bool _dllDirectorySet;

    private bool _loggedFirstSuccess;

    public LedDriver()
    {
        DllPath = ResolveDllPath();
        IsAvailable = DllPath is not null;
        LastError = IsAvailable
            ? null
            : "InsydeDCHU.dll не найден. Установите Control Center 3.0 или положите DLL рядом с программой.";

        if (!IsAvailable) return;

        try
        {
            EnsureDllDirectory();
            InstallResolver();
        }
        catch (Exception ex)
        {
            IsAvailable = false;
            LastError = $"Не удалось подготовить InsydeDCHU.dll: {ex.Message}";
        }
    }

    public string? DllPath { get; }
    public bool IsAvailable { get; private set; }
    public string? LastError { get; private set; }

    public bool SetColor(RgbColor color)
    {
        if (!IsAvailable) return false;

        try
        {
            EnsureDllDirectory();

            var encoded = (color.B << 16) | (color.R << 8) | color.G;
            for (var slot = 0; slot < 3; slot++)
            {
                var args = ((0xF0 | slot) << 24) | encoded;
                var buffer = BitConverter.GetBytes(args);
                var result = SetDCHU_Data(DchuCommand, buffer, buffer.Length);
                if (result != SuccessCode)
                {
                    LastError = $"Контроллер подсветки вернул код {result}.";
                    return false;
                }
            }

            LastError = null;
            if (!_loggedFirstSuccess)
            {
                _loggedFirstSuccess = true;
                AppLogger.Log($"DCHU: первая запись цвета успешна ({color.ToHex()}).");
            }
            return true;
        }
        catch (Exception ex) when (ex is DllNotFoundException or BadImageFormatException or EntryPointNotFoundException)
        {
            IsAvailable = false;
            LastError = "Не удалось загрузить драйвер подсветки InsydeDCHU.dll.";
            return false;
        }
        catch (Exception ex)
        {
            LastError = ex.Message;
            return false;
        }
    }

    private static string? ResolveDllPath()
    {
        var local = Path.Combine(AppContext.BaseDirectory, DriverFileName);
        if (File.Exists(local)) return Path.GetFullPath(local);

        var programFilesX86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);
        var programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
        var candidates = new[]
        {
            Path.Combine(programFilesX86, "ControlCenter", "DCHU", DriverFileName),
            Path.Combine(programFiles, "ControlCenter", "DCHU", DriverFileName),
            Path.Combine(programFilesX86, "ControlCenter", DriverFileName),
            Path.Combine(programFiles, "ControlCenter", DriverFileName)
        };

        foreach (var candidate in candidates)
        {
            if (!string.IsNullOrWhiteSpace(candidate) && File.Exists(candidate))
                return Path.GetFullPath(candidate);
        }

        return null;
    }

    private void EnsureDllDirectory()
    {
        if (_dllDirectorySet || DllPath is null) return;

        var directory = Path.GetDirectoryName(DllPath);
        if (!string.IsNullOrWhiteSpace(directory)) SetDllDirectory(directory);
        _dllDirectorySet = true;
    }

    private void InstallResolver()
    {
        lock (ResolverLock)
        {
            _resolvedDllPath = DllPath;
            if (_resolverInstalled) return;

            NativeLibrary.SetDllImportResolver(typeof(LedDriver).Assembly, (name, _, _) =>
            {
                if (!string.Equals(name, DriverFileName, StringComparison.OrdinalIgnoreCase)) return IntPtr.Zero;
                var path = _resolvedDllPath;
                return path is not null && File.Exists(path) ? NativeLibrary.Load(path) : IntPtr.Zero;
            });
            _resolverInstalled = true;
        }
    }

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool SetDllDirectory(string? lpPathName);

    [DllImport(DriverFileName, CallingConvention = CallingConvention.Cdecl)]
    private static extern int SetDCHU_Data(int command, byte[] buffer, int length);
}
