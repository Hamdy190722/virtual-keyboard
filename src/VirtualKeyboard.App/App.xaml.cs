using System.IO;
using System.Threading;
using System.Windows;
using VirtualKeyboard.App.Services;
using VirtualKeyboard.Core.Prediction;
using VirtualKeyboard.Core.Settings;

namespace VirtualKeyboard.App;

public partial class App : Application
{
    private const string MutexName = "VirtualKeyboard.SingleInstance";
    private const string WakeEventName = "VirtualKeyboard.WakeEvent";

    private Mutex? _mutex;
    private bool _ownsMutex;
    private EventWaitHandle? _wakeEvent;
    private RegisteredWaitHandle? _wakeRegistration;
    private TrayService? _tray;

    public static App Instance => (App)Current;

    public SettingsStore Store { get; private set; } = null!;
    public AppSettings Settings { get; private set; } = null!;
    public WordPredictor Predictor { get; private set; } = null!;
    public MainWindow KeyboardWindow { get; private set; } = null!;
    public bool IsShuttingDown { get; private set; }

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        _mutex = new Mutex(initiallyOwned: false, MutexName);
        try
        {
            _ownsMutex = _mutex.WaitOne(0, exitContext: false);
        }
        catch (AbandonedMutexException)
        {
            _ownsMutex = true;
        }

        if (!_ownsMutex)
        {
            if (EventWaitHandle.TryOpenExisting(WakeEventName, out var wake))
            {
                using (wake)
                {
                    wake.Set();
                }

                Environment.Exit(0);
                return;
            }

            MessageBox.Show(
                "Virtual Keyboard is already running.\n\nPress Ctrl+Alt+K or use the tray icon.",
                "Virtual Keyboard",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            Environment.Exit(0);
            return;
        }

        DispatcherUnhandledException += (_, args) =>
        {
            Log(args.Exception);
            if (IsShuttingDown)
            {
                args.Handled = true;
                CleanupSingleInstance();
                Environment.Exit(0);
                return;
            }

            MessageBox.Show(
                "Virtual Keyboard hit an unexpected error:\n\n" + args.Exception.Message,
                "Virtual Keyboard",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            args.Handled = true;
        };

        Store = new SettingsStore();
        Settings = Store.Load();
        Predictor = WordPredictor.Create(Store.WordlistsDirectory, Store.LearnedWordsPath);

        if (Settings.StartWithWindows && AutostartService.IsSupported && !AutostartService.IsEnabled())
        {
            AutostartService.SetEnabled(true);
        }

        ThemeService.Apply(Settings.Theme);

        KeyboardWindow = new MainWindow(Settings, Predictor);
        MainWindow = KeyboardWindow;

        _tray = new TrayService(
            toggle: () => Dispatcher.Invoke(() => KeyboardWindow.ToggleVisibility()),
            setLayout: id => Dispatcher.Invoke(() => KeyboardWindow.SetLayout(id)),
            setTheme: theme => Dispatcher.Invoke(() => ApplyTheme(theme)),
            openSettings: () => Dispatcher.Invoke(() => KeyboardWindow.OpenSettings()),
            toggleAutostart: () => Dispatcher.Invoke(() => KeyboardWindow.ToggleAutostart()),
            exit: () => Dispatcher.Invoke(ShutdownApp),
            getLayout: () => Settings.LayoutId,
            getTheme: () => Settings.Theme,
            isAutostart: AutostartService.IsEnabled);

        KeyboardWindow.Show();

        _wakeEvent = new EventWaitHandle(false, EventResetMode.AutoReset, WakeEventName);
        _wakeRegistration = ThreadPool.RegisterWaitForSingleObject(
            _wakeEvent,
            (_, _) => Dispatcher.BeginInvoke(() => KeyboardWindow?.RestoreFromTaskbar()),
            null,
            Timeout.Infinite,
            executeOnlyOnce: false);
    }

    public void PreviewTheme(string theme)
    {
        ThemeService.Apply(theme);
        KeyboardWindow.RefreshTheme();
    }

    public void ApplyTheme(string theme)
    {
        Settings.Theme = Core.Settings.ThemeCatalog.Normalize(theme);
        ThemeService.Apply(Settings.Theme);
        KeyboardWindow.RefreshTheme();
        SaveSettings();
    }

    public void SaveSettings() => Store.Save(Settings);

    public void ShutdownApp()
    {
        if (IsShuttingDown)
        {
            return;
        }

        IsShuttingDown = true;

        try { Predictor?.SaveLearnedIfDirty(); } catch { }
        try { Store?.Save(Settings); } catch { }
        try { _tray?.Dispose(); } catch { }
        _tray = null;

        CleanupSingleInstance();

        try
        {
            KeyboardWindow?.AllowClose();
            KeyboardWindow?.Close();
        }
        catch
        {
        }

        try
        {
            Shutdown(0);
        }
        catch
        {
            Environment.Exit(0);
        }

        Environment.Exit(0);
    }

    protected override void OnExit(ExitEventArgs e)
    {
        try { _tray?.Dispose(); } catch { }
        _tray = null;
        CleanupSingleInstance();
        base.OnExit(e);
    }

    private void CleanupSingleInstance()
    {
        var wakeReg = _wakeRegistration;
        _wakeRegistration = null;
        if (wakeReg is not null)
        {
            try { wakeReg.Unregister(null); } catch { }
        }

        var wakeEvent = _wakeEvent;
        _wakeEvent = null;
        if (wakeEvent is not null)
        {
            try { wakeEvent.Dispose(); } catch { }
        }

        var mutex = _mutex;
        _mutex = null;
        var owns = _ownsMutex;
        _ownsMutex = false;
        if (mutex is not null)
        {
            if (owns)
            {
                try { mutex.ReleaseMutex(); } catch { }
            }

            try { mutex.Dispose(); } catch { }
        }
    }

    private static void Log(Exception exception)
    {
        try
        {
            File.AppendAllText(
                Path.Combine(AppContext.BaseDirectory, "error.log"),
                $"[{DateTime.Now:O}] {exception}{Environment.NewLine}{Environment.NewLine}");
        }
        catch
        {
            // Logging is best-effort.
        }
    }
}
