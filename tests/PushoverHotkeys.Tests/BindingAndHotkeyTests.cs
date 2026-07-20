using PushoverHotkeys.Models;
using PushoverHotkeys.Services;
using Xunit;

namespace PushoverHotkeys.Tests;

public sealed class BindingAndHotkeyTests
{
    private const string FirstKey = "aBcDeFgHiJkLmNoPqRsTuVwXyZ0123";
    private const string SecondKey = "zYxWvUtSrQpOnMlKjIhGfEdCbA9876";

    [Fact]
    public void Upsert_CombinesRecipientsForTheSameKeyChord()
    {
        var bindings = new List<HotkeyBinding>();
        var chord = new KeyChord { VirtualKey = 0x70, Modifiers = HotkeyModifiers.Control };

        BindingMerger.Upsert(bindings, new HotkeyBinding
        {
            Chord = chord,
            Recipients = [new Recipient { UserKey = FirstKey }]
        });
        BindingMerger.Upsert(bindings, new HotkeyBinding
        {
            Chord = chord.DeepCopy(),
            Recipients = [new Recipient { UserKey = SecondKey }]
        });

        var merged = Assert.Single(bindings);
        Assert.Equal(2, merged.Recipients.Count);
    }

    [Fact]
    public void Upsert_RetainsConfiguredMessagePriorityAndSound()
    {
        var bindings = new List<HotkeyBinding>();
        var candidate = new HotkeyBinding
        {
            Chord = new KeyChord { VirtualKey = 0x70 },
            Message = "Проверка",
            Priority = (int)PushoverPriority.High,
            Sound = "siren",
            Recipients = [new Recipient { UserKey = FirstKey }]
        };

        BindingMerger.Upsert(bindings, candidate);

        var binding = Assert.Single(bindings);
        Assert.Equal("Проверка", binding.Message);
        Assert.Equal(1, binding.Priority);
        Assert.Equal("siren", binding.Sound);
    }

    [Fact]
    public void Upsert_RejectsMoreThanFiftyRecipients()
    {
        var recipients = Enumerable.Range(0, 51)
            .Select(index => new Recipient { UserKey = index.ToString("D30") })
            .ToList();

        Assert.Throws<ArgumentException>(() => BindingMerger.Upsert([], new HotkeyBinding
        {
            Chord = new KeyChord { VirtualKey = 0x70 },
            Recipients = recipients
        }));
    }

    [Theory]
    [InlineData(0x1B, HotkeyModifiers.None)] // Escape
    [InlineData(0x60, HotkeyModifiers.None)] // NumPad0
    [InlineData(0x41, HotkeyModifiers.Control)] // Ctrl+A
    public void KeyChord_MatchesStandardKeys(int virtualKey, HotkeyModifiers modifiers)
    {
        var chord = new KeyChord { VirtualKey = (uint)virtualKey, Modifiers = modifiers };

        Assert.True(chord.Matches((uint)virtualKey, modifiers));
    }

    [Fact]
    public void PhysicalKeyTracker_OnlyAcceptsOneDownEventUntilKeyUp()
    {
        var tracker = new PhysicalKeyTracker();

        Assert.True(tracker.TryMarkDown(0x70));
        Assert.False(tracker.TryMarkDown(0x70));
        tracker.MarkUp(0x70);
        Assert.True(tracker.TryMarkDown(0x70));
    }
}
