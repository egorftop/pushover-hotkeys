using System.Windows;
using PushoverHotkeys.Models;
using PushoverHotkeys.Services;
using MessageBox = System.Windows.MessageBox;

namespace PushoverHotkeys;

public partial class App : System.Windows.Application
{
    private readonly SettingsStore _settingsStore = new();
    private readonly AutostartService _autostartService = new();
    private readonly GlobalHotkeyListener _hotkeyListener = new();
    private readonly PushoverClient _pushoverClient = new();
    private TrayService? _trayService;
    private MainWindow? _mainWindow;
    private bool _isExiting;
    private string? _settingsLoadError;
    private string? _autostartWarning;

    public AppSettings Settings { get; private set; } = new();
    public string RuntimeStatus { get; private set; } = "Запуск…";
    public bool IsExiting => _isExiting;

    public event EventHandler? StateChanged;

    protected override void OnStartup(StartupEventArgs eventArgs)
    {
        base.OnStartup(eventArgs);

        try
        {
            Settings = _settingsStore.Load();
        }
        catch (SettingsStoreException exception)
        {
            _settingsLoadError = exception.Message;
            Settings = new AppSettings { HotkeysEnabled = false };
        }

        _hotkeyListener.HotkeyPressed += OnHotkeyPressed;
        _trayService = new TrayService();
        _trayService.OpenRequested += (_, _) => Dispatcher.Invoke(ShowMainWindow);
        _trayService.ToggleHotkeysRequested += (_, _) => Dispatcher.Invoke(ToggleHotkeys);
        _trayService.ExitRequested += (_, _) => Dispatcher.Invoke(ExitApplication);

        if (_settingsLoadError is null)
        {
            TryApplyAutostart();
        }

        RefreshRuntime();
        _mainWindow = new MainWindow(this);
        MainWindow = _mainWindow;

        var startMinimized = eventArgs.Args.Any(argument =>
            string.Equals(argument, "--minimized", StringComparison.OrdinalIgnoreCase));
        if (!startMinimized)
        {
            ShowMainWindow();
        }
    }

    public async Task<bool> ReplaceSettingsAsync(AppSettings updatedSettings)
    {
        if (_settingsLoadError is not null)
        {
            MessageBox.Show(
                _settingsLoadError,
                "Настройки недоступны",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            return false;
        }

        try
        {
            await Task.Run(() => _settingsStore.Save(updatedSettings));
            Settings = updatedSettings;
            TryApplyAutostart();
            RefreshRuntime();
            return true;
        }
        catch (SettingsStoreException exception)
        {
            MessageBox.Show(exception.Message, "Ошибка сохранения", MessageBoxButton.OK, MessageBoxImage.Error);
            return false;
        }
    }

    public async Task<SendResult> SendBindingAsync(Guid bindingId)
    {
        var binding = Settings.Bindings.FirstOrDefault(item => item.Id == bindingId);
        return binding is null
            ? SendResult.Failure("Привязка уже удалена.")
            : await _pushoverClient.SendAsync(Settings.AppToken, binding);
    }

    public void ShowMainWindow()
    {
        if (_mainWindow is null)
        {
            return;
        }

        _mainWindow.Show();
        if (_mainWindow.WindowState == WindowState.Minimized)
        {
            _mainWindow.WindowState = WindowState.Normal;
        }

        _mainWindow.Activate();
        _mainWindow.Focus();
    }

    public void HideMainWindow() => _mainWindow?.Hide();

    public void ToggleHotkeys()
    {
        if (_settingsLoadError is not null)
        {
            return;
        }

        var updated = Settings.DeepCopy();
        updated.HotkeysEnabled = !updated.HotkeysEnabled;
        _ = ReplaceSettingsAsync(updated);
    }

    public void ExitApplication()
    {
        _isExiting = true;
        DisposeServices();
        _mainWindow?.AllowClose();
        Shutdown();
    }

    protected override void OnExit(ExitEventArgs eventArgs)
    {
        DisposeServices();
        base.OnExit(eventArgs);
    }

    private async void OnHotkeyPressed(KeyChord chord)
    {
        var binding = Settings.Bindings.FirstOrDefault(item => item.Chord.Equals(chord));
        if (binding is null || !Settings.HotkeysEnabled)
        {
            return;
        }

        var result = await _pushoverClient.SendAsync(Settings.AppToken, binding);
        if (!result.IsSuccess)
        {
            Dispatcher.Invoke(() => _trayService?.ShowError(result.ErrorMessage));
        }
    }

    private void TryApplyAutostart()
    {
        try
        {
            _autostartService.SetEnabled(Settings.StartWithWindows);
            _autostartWarning = null;
        }
        catch (Exception exception)
        {
            _autostartWarning = $"Автозапуск не обновлён: {exception.Message}";
        }
    }

    private void DisposeServices()
    {
        _hotkeyListener.Dispose();
        _trayService?.Dispose();
        _trayService = null;
    }

    private void RefreshRuntime()
    {
        if (_settingsLoadError is not null)
        {
            StopListenerSafely();
            SetRuntimeStatus(_settingsLoadError);
            return;
        }

        if (!Settings.HotkeysEnabled)
        {
            StopListenerSafely();
            SetRuntimeStatus("Горячие клавиши отключены.");
            return;
        }

        if (!PushoverKeyValidator.IsValid(Settings.AppToken))
        {
            StopListenerSafely();
            SetRuntimeStatus("Укажите Pushover App Token в настройках.");
            return;
        }

        if (Settings.Bindings.Count == 0)
        {
            StopListenerSafely();
            SetRuntimeStatus("Добавьте хотя бы одну горячую клавишу.");
            return;
        }

        try
        {
            _hotkeyListener.Configure(Settings.Bindings.Select(binding => binding.Chord));
            _hotkeyListener.Start();
            SetRuntimeStatus($"Горячие клавиши активны: {Settings.Bindings.Count}.");
        }
        catch (Exception exception)
        {
            StopListenerSafely();
            SetRuntimeStatus($"Не удалось включить горячие клавиши: {exception.Message}");
        }
    }

    private void StopListenerSafely()
    {
        try
        {
            _hotkeyListener.Stop();
        }
        catch
        {
            // A failed unhook must not prevent the settings window from remaining usable.
        }
    }

    private void SetRuntimeStatus(string status)
    {
        RuntimeStatus = _autostartWarning is null ? status : $"{status} {_autostartWarning}";
        _trayService?.SetTooltip($"Pushover Hotkeys: {RuntimeStatus}");
        _trayService?.SetHotkeysEnabled(_hotkeyListener.IsRunning);
        StateChanged?.Invoke(this, EventArgs.Empty);
    }
}
