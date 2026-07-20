using System.Windows.Input;

namespace PushoverHotkeys.Models;

[Flags]
public enum HotkeyModifiers
{
    None = 0,
    Control = 1,
    Alt = 2,
    Shift = 4,
    Windows = 8
}

public sealed class KeyChord : IEquatable<KeyChord>
{
    public uint VirtualKey { get; set; }
    public HotkeyModifiers Modifiers { get; set; }

    public bool IsValid => VirtualKey != 0;

    public KeyChord DeepCopy() => new() { VirtualKey = VirtualKey, Modifiers = Modifiers };

    public static KeyChord FromWpfKey(Key key, ModifierKeys modifiers)
    {
        var virtualKey = (uint)KeyInterop.VirtualKeyFromKey(key);
        var resultModifiers = FromWpfModifiers(modifiers);
        return new KeyChord
        {
            VirtualKey = virtualKey,
            Modifiers = NormalizeModifiers(virtualKey, resultModifiers)
        };
    }

    public static HotkeyModifiers FromWpfModifiers(ModifierKeys modifiers)
    {
        var result = HotkeyModifiers.None;
        if (modifiers.HasFlag(ModifierKeys.Control)) result |= HotkeyModifiers.Control;
        if (modifiers.HasFlag(ModifierKeys.Alt)) result |= HotkeyModifiers.Alt;
        if (modifiers.HasFlag(ModifierKeys.Shift)) result |= HotkeyModifiers.Shift;
        if (modifiers.HasFlag(ModifierKeys.Windows)) result |= HotkeyModifiers.Windows;
        return result;
    }

    public static HotkeyModifiers NormalizeModifiers(uint virtualKey, HotkeyModifiers modifiers)
    {
        return virtualKey switch
        {
            0x10 or 0xA0 or 0xA1 => modifiers & ~HotkeyModifiers.Shift,
            0x11 or 0xA2 or 0xA3 => modifiers & ~HotkeyModifiers.Control,
            0x12 or 0xA4 or 0xA5 => modifiers & ~HotkeyModifiers.Alt,
            0x5B or 0x5C => modifiers & ~HotkeyModifiers.Windows,
            _ => modifiers
        };
    }

    public bool Matches(uint virtualKey, HotkeyModifiers modifiers) =>
        VirtualKey == virtualKey && Modifiers == NormalizeModifiers(virtualKey, modifiers);

    public bool Equals(KeyChord? other) =>
        other is not null && VirtualKey == other.VirtualKey && Modifiers == other.Modifiers;

    public override bool Equals(object? obj) => Equals(obj as KeyChord);

    public override int GetHashCode() => HashCode.Combine(VirtualKey, (int)Modifiers);

    public override string ToString()
    {
        var parts = new List<string>();
        if (Modifiers.HasFlag(HotkeyModifiers.Control)) parts.Add("Ctrl");
        if (Modifiers.HasFlag(HotkeyModifiers.Alt)) parts.Add("Alt");
        if (Modifiers.HasFlag(HotkeyModifiers.Shift)) parts.Add("Shift");
        if (Modifiers.HasFlag(HotkeyModifiers.Windows)) parts.Add("Win");

        var key = KeyInterop.KeyFromVirtualKey((int)VirtualKey);
        parts.Add(key == Key.None ? $"VK_{VirtualKey:X2}" : key.ToString());
        return string.Join(" + ", parts);
    }
}

