using System.Text.RegularExpressions;

namespace PushoverHotkeys.Models;

public sealed class AppSettings
{
    public int Version { get; set; } = 1;
    public string AppToken { get; set; } = string.Empty;
    public bool StartWithWindows { get; set; } = true;
    public bool HotkeysEnabled { get; set; } = true;
    public List<HotkeyBinding> Bindings { get; set; } = [];

    public AppSettings DeepCopy() => new()
    {
        Version = Version,
        AppToken = AppToken,
        StartWithWindows = StartWithWindows,
        HotkeysEnabled = HotkeysEnabled,
        Bindings = Bindings.Select(binding => binding.DeepCopy()).ToList()
    };
}

public sealed class HotkeyBinding
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public KeyChord Chord { get; set; } = new();
    public string Message { get; set; } = "GM";
    public int Priority { get; set; } = (int)PushoverPriority.Normal;
    public string Sound { get; set; } = PushoverSounds.DefaultId;
    public List<Recipient> Recipients { get; set; } = [];

    public HotkeyBinding DeepCopy() => new()
    {
        Id = Id,
        Chord = Chord.DeepCopy(),
        Message = Message,
        Priority = Priority,
        Sound = Sound,
        Recipients = Recipients.Select(recipient => recipient.DeepCopy()).ToList()
    };
}

public enum PushoverPriority
{
    Lowest = -2,
    Low = -1,
    Normal = 0,
    High = 1
}

public static class PushoverSounds
{
    public const string DefaultId = "default";

    public static readonly IReadOnlyList<PushoverSoundOption> Options =
    [
        new(DefaultId, "По умолчанию в Pushover"),
        new("none", "Без звука"),
        new("pushover", "Pushover"),
        new("bike", "Bike"),
        new("bugle", "Bugle"),
        new("cashregister", "Cash Register"),
        new("classical", "Classical"),
        new("cosmic", "Cosmic"),
        new("falling", "Falling"),
        new("gamelan", "Gamelan"),
        new("incoming", "Incoming"),
        new("intermission", "Intermission"),
        new("magic", "Magic"),
        new("mechanical", "Mechanical"),
        new("pianobar", "Piano Bar"),
        new("siren", "Siren"),
        new("spacealarm", "Space Alarm"),
        new("tugboat", "Tug Boat"),
        new("alien", "Alien Alarm"),
        new("climb", "Climb"),
        new("persistent", "Persistent"),
        new("echo", "Pushover Echo"),
        new("updown", "Up Down"),
        new("vibrate", "Только вибрация")
    ];

    public static bool IsValid(string? sound) => Options.Any(option => option.Id == sound);

    public static string DisplayName(string? sound) =>
        Options.FirstOrDefault(option => option.Id == sound)?.Name ?? "По умолчанию в Pushover";
}

public sealed record PushoverSoundOption(string Id, string Name);

public sealed class Recipient
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;
    public string UserKey { get; set; } = string.Empty;

    public string DisplayName => string.IsNullOrWhiteSpace(Name) ? UserKey : $"{Name} — {UserKey}";

    public Recipient DeepCopy() => new() { Id = Id, Name = Name, UserKey = UserKey };
}

public static partial class PushoverKeyValidator
{
    [GeneratedRegex("^[A-Za-z0-9]{30}$", RegexOptions.CultureInvariant)]
    private static partial Regex PushoverKeyRegex();

    public static bool IsValid(string? value) => value is not null && PushoverKeyRegex().IsMatch(value);
}
