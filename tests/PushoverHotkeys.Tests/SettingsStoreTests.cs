using PushoverHotkeys.Models;
using PushoverHotkeys.Services;
using Xunit;

namespace PushoverHotkeys.Tests;

public sealed class SettingsStoreTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "PushoverHotkeysTests", Guid.NewGuid().ToString("N"));

    [Fact]
    public void SaveThenLoad_RoundTripsAndDoesNotExposeSecretsInPlainText()
    {
        var store = new SettingsStore(_directory);
        var token = "aBcDeFgHiJkLmNoPqRsTuVwXyZ0123";
        var userKey = "zYxWvUtSrQpOnMlKjIhGfEdCbA9876";
        var settings = new AppSettings
        {
            AppToken = token,
            Bindings =
            [
                new HotkeyBinding
                {
                    Chord = new KeyChord { VirtualKey = 0x41 },
                    Recipients = [new Recipient { UserKey = userKey }]
                }
            ]
        };

        store.Save(settings);
        var loaded = store.Load();

        Assert.Equal(token, loaded.AppToken);
        Assert.Equal(userKey, Assert.Single(Assert.Single(loaded.Bindings).Recipients).UserKey);
        var diskText = File.ReadAllText(store.SettingsPath);
        Assert.False(diskText.Contains(token, StringComparison.Ordinal));
        Assert.False(diskText.Contains(userKey, StringComparison.Ordinal));
    }

    [Fact]
    public void Load_WhenEncryptedFileIsCorrupted_ThrowsHelpfulException()
    {
        var store = new SettingsStore(_directory);
        Directory.CreateDirectory(_directory);
        File.WriteAllBytes(store.SettingsPath, [1, 2, 3, 4]);

        var exception = Assert.Throws<SettingsStoreException>(store.Load);

        Assert.Contains("Не удалось прочитать", exception.Message);
    }

    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }
}
