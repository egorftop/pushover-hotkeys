using Microsoft.Win32;

namespace PushoverHotkeys.Services;

public sealed class AutostartService
{
    private const string RunPath = "Software\\Microsoft\\Windows\\CurrentVersion\\Run";
    private const string ValueName = "PushoverHotkeys";

    public void SetEnabled(bool enabled)
    {
        using var key = Registry.CurrentUser.CreateSubKey(RunPath, writable: true)
            ?? throw new InvalidOperationException("Не удалось открыть раздел автозапуска Windows.");

        if (enabled)
        {
            var executablePath = Environment.ProcessPath
                ?? throw new InvalidOperationException("Не удалось определить путь к приложению.");
            key.SetValue(ValueName, $"\"{executablePath}\" --minimized", RegistryValueKind.String);
        }
        else
        {
            key.DeleteValue(ValueName, throwOnMissingValue: false);
        }
    }
}
