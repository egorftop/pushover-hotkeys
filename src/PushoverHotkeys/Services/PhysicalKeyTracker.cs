namespace PushoverHotkeys.Services;

/// <summary>Suppresses Windows auto-repeat while preserving a new press after key-up.</summary>
public sealed class PhysicalKeyTracker
{
    private readonly HashSet<uint> _pressedKeys = [];

    public bool TryMarkDown(uint virtualKey) => _pressedKeys.Add(virtualKey);

    public void MarkUp(uint virtualKey) => _pressedKeys.Remove(virtualKey);

    public void Clear() => _pressedKeys.Clear();
}

