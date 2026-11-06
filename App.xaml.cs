using System.Windows;
using System.Windows.Threading;
using ClevoRgbLite.Core;
using ClevoRgbLite.Models;
using ClevoRgbLite.Services;
using Drawing = System.Drawing;
using Forms = System.Windows.Forms;

namespace ClevoRgbLite;

public partial class App : Application
{
    private const string MutexName = @"Local\ClevoRGBLite.SingleInstance";
    private const string ShowEventName = @"Local\ClevoRGBLite.Show";

    private readonly SettingsStore _store = new();
    private Mutex? _mutex;
    private EventWaitHandle? _showEvent;
    private Forms.NotifyIcon? _tray;
    private Forms.ToolStripMenuItem? _enabledItem;
    private MainWindow? _window;
    private LedDriver? _driver;
    private EffectEngine? _engine;
    private DispatcherTimer? _saveTimer;
    private bool _exiting;

    public AppSettings Settings { get; private set; } = new();
    public bool IsExiting => _exiting;
    public bool DriverAvailable => _driver?.IsAvailable == true;
    public string? DriverError => _driver?.LastError;

    public event EventHandler? SettingsChanged;
    public event EventHandler? DriverStateChanged;

    public App()
    {
        System.Windows.Media.RenderOptions.ProcessRenderMode = System.Windows.Interop.RenderMode.SoftwareOnly;
        DispatcherUnhandledException += (_, args) =>
        {
            AppLogger.Log("Необработанная ошибка интерфейса.", args.Exception);
            MessageBox.Show(
                "Произошла ошибка:\n\n" + args.Exception.Message,
                "Clevo RGB",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            args.Handled = true;
        };
        AppDomain.CurrentDomain.UnhandledException += (_, args) =>
            AppLogger.Log("Необработанная ошибка.", args.ExceptionObject as Exception);
    }

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        AppLogger.Log("Старт приложения.");

        _mutex = new Mutex(initiallyOwned: true, MutexName, out var createdNew);
        if (!createdNew)
        {
            try
            {
                EventWaitHandle.OpenExisting(ShowEventName).Set();
            }
            catch
            {
                // The other instance may be shutting down.
            }
            Shutdown();
            return;
        }

        _showEvent = new EventWaitHandle(false, EventResetMode.AutoReset, ShowEventName);
        var showThread = new Thread(() =>
        {
            try
            {
                while (true)
                {
                    _showEvent.WaitOne();
                    Dispatcher.BeginInvoke(ShowMainWindow);
                }
            }
            catch (ObjectDisposedException)
            {
                // App is exiting.
            }
        })
        {
            IsBackground = true,
            Name = "Clevo RGB single-instance listener"
        };
        showThread.Start();

        Settings = _store.Load();
        AppLogger.Log("Настройки загружены.");
        _driver = new LedDriver();
        if (_driver.IsAvailable)
        {
            _engine = new EffectEngine(_driver, Settings);
            _engine.DriverError += OnEngineDriverError;
            _engine.Start();
            AppLogger.Log("Движок подсветки запущен.");
        }

        _saveTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(350) };
        _saveTimer.Tick += (_, _) =>
        {
            _saveTimer.Stop();
            _store.Save(Settings);
        };

        CreateTray();
        AppLogger.Log("Иконка в трее создана.");

        var minimized = e.Args.Any(arg => string.Equals(arg, "--minimized", StringComparison.OrdinalIgnoreCase));
        if (!minimized)
        {
            AppLogger.Log("Открываю главное окно.");
            ShowMainWindow();
            AppLogger.Log("Главное окно открыто.");
        }

        AppLogger.Log($"Запуск Clevo RGB завершён. Драйвер: {(_driver.IsAvailable ? "доступен" : _driver.LastError)}");
    }

    public void ShowMainWindow()
    {
        if (_exiting) return;

        if (_window is null)
        {
            _window = new MainWindow();
            _window.Closed += (_, _) => _window = null;
        }

        if (!_window.IsVisible) _window.Show();
        if (_window.WindowState == WindowState.Minimized) _window.WindowState = WindowState.Normal;
        _window.Activate();
        _window.Topmost = true;
        _window.Topmost = false;
        _window.Focus();
    }

    public void UpdateSettings(AppSettings settings, bool save = true)
    {
        Settings = settings.Normalize();
        _engine?.Update(Settings);
        SettingsChanged?.Invoke(this, EventArgs.Empty);
        if (save) ScheduleSave();
    }

    public void SetEnabled(bool enabled)
    {
        UpdateSettings(Settings with { Enabled = enabled });
        if (_enabledItem is not null) _enabledItem.Checked = enabled;
    }

    public void SetAutostart(bool enabled)
    {
        Autostart.SetEnabled(enabled);
        UpdateSettings(Settings with { StartWithWindows = enabled });
    }

    public void ForceRefresh()
    {
        _engine?.ForceRefresh();
    }

    public void ExitApplication()
    {
        if (_exiting) return;
        _exiting = true;
        _saveTimer?.Stop();
        _store.Save(Settings);
        _engine?.TurnOff();
        _engine?.Dispose();
        _engine = null;
        _tray?.Dispose();
        _tray = null;
        Shutdown();
    }

    private void ScheduleSave()
    {
        if (_saveTimer is null) return;
        _saveTimer.Stop();
        _saveTimer.Start();
    }

    private void CreateTray()
    {
        _tray = new Forms.NotifyIcon
        {
            Icon = LoadTrayIcon(),
            Text = "Clevo RGB — подсветка клавиатуры",
            Visible = true
        };

        var menu = new Forms.ContextMenuStrip();
        var open = new Forms.ToolStripMenuItem("Открыть настройки");
        open.Click += (_, _) => Dispatcher.BeginInvoke(ShowMainWindow);

        _enabledItem = new Forms.ToolStripMenuItem("Подсветка включена")
        {
            Checked = Settings.Enabled
        };
        _enabledItem.Click += (_, _) => SetEnabled(!Settings.Enabled);

        var exit = new Forms.ToolStripMenuItem("Выход");
        exit.Click += (_, _) => ExitApplication();

        menu.Items.Add(open);
        menu.Items.Add(new Forms.ToolStripSeparator());
        menu.Items.Add(_enabledItem);
        menu.Items.Add(new Forms.ToolStripSeparator());
        menu.Items.Add(exit);

        _tray.ContextMenuStrip = menu;
        _tray.DoubleClick += (_, _) => Dispatcher.BeginInvoke(ShowMainWindow);
    }

    private static Drawing.Icon LoadTrayIcon()
    {
        try
        {
            var icon = Drawing.Icon.ExtractAssociatedIcon(Environment.ProcessPath!);
            return icon ?? Drawing.SystemIcons.Application;
        }
        catch
        {
            return Drawing.SystemIcons.Application;
        }
    }

    private void OnEngineDriverError(object? sender, string message)
    {
        Dispatcher.BeginInvoke(() =>
        {
            AppLogger.Log("Ошибка драйвера: " + message);
            DriverStateChanged?.Invoke(this, EventArgs.Empty);
        });
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _saveTimer?.Stop();
        _engine?.Dispose();
        _engine = null;
        _tray?.Dispose();
        _tray = null;
        _showEvent?.Dispose();
        _showEvent = null;

        if (_mutex is not null)
        {
            try
            {
                _mutex.ReleaseMutex();
            }
            catch
            {
                // ignored
            }
            _mutex.Dispose();
            _mutex = null;
        }

        base.OnExit(e);
    }
}
