using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.IO;
using PushoverHotkeys.Models;

namespace PushoverHotkeys.Services;

public sealed class SettingsStore
{
    private const string SettingsFileName = "settings.dat";
    private readonly string _settingsPath;
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = false };

    public SettingsStore(string? dataDirectory = null)
    {
        var root = dataDirectory ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "PushoverHotkeys");
        _settingsPath = Path.Combine(root, SettingsFileName);
    }

    public string SettingsPath => _settingsPath;

    public AppSettings Load()
    {
        if (!File.Exists(_settingsPath))
        {
            return new AppSettings();
        }

        try
        {
            var encrypted = File.ReadAllBytes(_settingsPath);
            var json = ProtectedData.Unprotect(encrypted, optionalEntropy: null, DataProtectionScope.CurrentUser);
            var settings = JsonSerializer.Deserialize<AppSettings>(json, JsonOptions);
            return settings ?? throw new SettingsStoreException("Файл настроек пуст или имеет неверный формат.");
        }
        catch (SettingsStoreException)
        {
            throw;
        }
        catch (Exception exception) when (exception is CryptographicException or JsonException or IOException or UnauthorizedAccessException)
        {
            throw new SettingsStoreException(
                "Не удалось прочитать зашифрованные настройки. Не удаляйте файл вручную: сначала сохраните резервную копию.",
                exception);
        }
    }

    public void Save(AppSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        var directory = Path.GetDirectoryName(_settingsPath)!;
        Directory.CreateDirectory(directory);

        try
        {
            var json = JsonSerializer.SerializeToUtf8Bytes(settings, JsonOptions);
            var encrypted = ProtectedData.Protect(json, optionalEntropy: null, DataProtectionScope.CurrentUser);
            var temporaryPath = _settingsPath + ".tmp";
            File.WriteAllBytes(temporaryPath, encrypted);
            File.Move(temporaryPath, _settingsPath, overwrite: true);
        }
        catch (Exception exception) when (exception is CryptographicException or IOException or UnauthorizedAccessException)
        {
            throw new SettingsStoreException("Не удалось сохранить зашифрованные настройки.", exception);
        }
    }
}

public sealed class SettingsStoreException : Exception
{
    public SettingsStoreException(string message, Exception? innerException = null) : base(message, innerException)
    {
    }
}
