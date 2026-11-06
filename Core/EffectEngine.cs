using System.Diagnostics;
using ClevoRgbLite.Models;

namespace ClevoRgbLite.Core;

public sealed class EffectEngine : IDisposable
{
    private readonly LedDriver _driver;
    private readonly object _driverLock = new();
    private AppSettings _settings;
    private CancellationTokenSource? _cts;
    private Task? _loop;
    private RgbColor? _lastWritten;
    private DateTime _lastForcedWrite = DateTime.MinValue;
    private string? _lastReportedError;

    public EffectEngine(LedDriver driver, AppSettings settings)
    {
        _driver = driver;
        _settings = settings.Copy();
    }

    public event EventHandler<string>? DriverError;

    public bool IsRunning => _loop is { IsCompleted: false };

    public void Start()
    {
        if (_loop is not null) return;
        _cts = new CancellationTokenSource();
        _loop = Task.Run(() => LoopAsync(_cts.Token));
    }

    public void Update(AppSettings settings)
    {
        Volatile.Write(ref _settings, settings.Copy());
        _lastForcedWrite = DateTime.MinValue;
    }

    public void ForceRefresh()
    {
        _lastForcedWrite = DateTime.MinValue;
    }

    public RgbColor CurrentColor()
    {
        var settings = Volatile.Read(ref _settings);
        return EffectRenderer.Render(settings, Environment.TickCount64);
    }

    private async Task LoopAsync(CancellationToken token)
    {
        var clock = Stopwatch.StartNew();
        while (!token.IsCancellationRequested)
        {
            var settings = Volatile.Read(ref _settings);
            var color = EffectRenderer.Render(settings, clock.Elapsed.TotalMilliseconds);

            var force = (DateTime.UtcNow - _lastForcedWrite).TotalSeconds >= 2;
            if (color != _lastWritten || force)
            {
                bool ok;
                lock (_driverLock)
                {
                    ok = _driver.SetColor(color);
                }

                if (!ok)
                {
                    if (!string.Equals(_lastReportedError, _driver.LastError, StringComparison.Ordinal))
                    {
                        _lastReportedError = _driver.LastError;
                        DriverError?.Invoke(this, _driver.LastError ?? "Ошибка управления подсветкой.");
                    }
                    await Task.Delay(1000, token);
                    continue;
                }

                _lastWritten = color;
                _lastForcedWrite = DateTime.UtcNow;
            }

            try
            {
                await Task.Delay(40, token);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }

    public void TurnOff()
    {
        lock (_driverLock)
        {
            _driver.SetColor(RgbColor.Black);
        }
        _lastWritten = RgbColor.Black;
    }

    public void Dispose()
    {
        _cts?.Cancel();
        try
        {
            _loop?.Wait(800);
        }
        catch
        {
            // ignored
        }
        _cts?.Dispose();
        _cts = null;
        _loop = null;
    }
}
