using System.Diagnostics;
using System.Runtime.InteropServices;
using PushoverHotkeys.Models;

namespace PushoverHotkeys.Services;

public sealed class GlobalHotkeyListener : IDisposable
{
    private const int WhKeyboardLl = 13;
    private const int WmKeyDown = 0x0100;
    private const int WmKeyUp = 0x0101;
    private const int WmSysKeyDown = 0x0104;
    private const int WmSysKeyUp = 0x0105;
    private const int VkShift = 0x10;
    private const int VkControl = 0x11;
    private const int VkMenu = 0x12;
    private const int VkLwin = 0x5B;
    private const int VkRwin = 0x5C;

    private readonly LowLevelKeyboardProc _callback;
    private readonly PhysicalKeyTracker _keyTracker = new();
    private IntPtr _hookHandle;
    private HashSet<KeyChord> _chords = [];

    public GlobalHotkeyListener()
    {
        _callback = HookCallback;
    }

    public bool IsRunning => _hookHandle != IntPtr.Zero;

    public event Action<KeyChord>? HotkeyPressed;

    public void Configure(IEnumerable<KeyChord> chords)
    {
        var configured = chords.Where(chord => chord.IsValid)
            .Select(chord => chord.DeepCopy())
            .ToHashSet();
        Interlocked.Exchange(ref _chords, configured);
    }

    public void Start()
    {
        if (IsRunning)
        {
            return;
        }

        using var process = Process.GetCurrentProcess();
        var moduleName = process.MainModule?.ModuleName;
        var moduleHandle = string.IsNullOrWhiteSpace(moduleName) ? IntPtr.Zero : GetModuleHandle(moduleName);
        _hookHandle = SetWindowsHookEx(WhKeyboardLl, _callback, moduleHandle, 0);
        if (_hookHandle == IntPtr.Zero)
        {
            throw new InvalidOperationException($"Windows не разрешила установить обработчик клавиатуры (код {Marshal.GetLastWin32Error()}).");
        }
    }

    public void Stop()
    {
        if (!IsRunning)
        {
            return;
        }

        var handle = _hookHandle;
        _hookHandle = IntPtr.Zero;
        _keyTracker.Clear();
        if (!UnhookWindowsHookEx(handle))
        {
            throw new InvalidOperationException($"Windows не разрешила снять обработчик клавиатуры (код {Marshal.GetLastWin32Error()}).");
        }
    }

    private IntPtr HookCallback(int code, IntPtr wParam, IntPtr lParam)
    {
        if (code >= 0)
        {
            var message = wParam.ToInt32();
            var keyData = Marshal.PtrToStructure<KbdLlHookStruct>(lParam);
            if (message is WmKeyDown or WmSysKeyDown)
            {
                if (_keyTracker.TryMarkDown(keyData.VkCode))
                {
                    var modifiers = GetCurrentModifiers();
                    var configured = Volatile.Read(ref _chords);
                    var chord = configured.FirstOrDefault(candidate => candidate.Matches(keyData.VkCode, modifiers));
                    if (chord is not null)
                    {
                        HotkeyPressed?.Invoke(chord.DeepCopy());
                    }
                }
            }
            else if (message is WmKeyUp or WmSysKeyUp)
            {
                _keyTracker.MarkUp(keyData.VkCode);
            }
        }

        return CallNextHookEx(_hookHandle, code, wParam, lParam);
    }

    private static HotkeyModifiers GetCurrentModifiers()
    {
        var result = HotkeyModifiers.None;
        if (IsDown(VkControl)) result |= HotkeyModifiers.Control;
        if (IsDown(VkMenu)) result |= HotkeyModifiers.Alt;
        if (IsDown(VkShift)) result |= HotkeyModifiers.Shift;
        if (IsDown(VkLwin) || IsDown(VkRwin)) result |= HotkeyModifiers.Windows;
        return result;
    }

    private static bool IsDown(int virtualKey) => (GetAsyncKeyState(virtualKey) & 0x8000) != 0;

    public void Dispose()
    {
        if (IsRunning)
        {
            try
            {
                Stop();
            }
            catch
            {
                // Shutdown should not fail because a Windows hook has already gone away.
            }
        }
    }

    private delegate IntPtr LowLevelKeyboardProc(int code, IntPtr wParam, IntPtr lParam);

    [StructLayout(LayoutKind.Sequential)]
    private struct KbdLlHookStruct
    {
        public uint VkCode;
        public uint ScanCode;
        public uint Flags;
        public uint Time;
        public IntPtr ExtraInfo;
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr SetWindowsHookEx(int idHook, LowLevelKeyboardProc callback, IntPtr moduleHandle, uint threadId);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool UnhookWindowsHookEx(IntPtr hookHandle);

    [DllImport("user32.dll")]
    private static extern IntPtr CallNextHookEx(IntPtr hookHandle, int code, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll")]
    private static extern short GetAsyncKeyState(int virtualKey);

    [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    private static extern IntPtr GetModuleHandle(string? moduleName);
}
