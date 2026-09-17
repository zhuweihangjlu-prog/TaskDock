using System.Runtime.InteropServices;
using System.Windows.Interop;

namespace TaskDock.Services;

public sealed class HotkeyService : IDisposable
{
    private const int HotkeyId = 0x5444;
    private const int WmHotkey = 0x0312;
    private readonly IntPtr _handle;
    private readonly HwndSource _source;
    private readonly Action _callback;

    public HotkeyService(IntPtr handle, Action callback)
    {
        _handle = handle;
        _callback = callback;
        _source = HwndSource.FromHwnd(handle);
        _source.AddHook(WndProc);
    }

    public bool Register(string gesture)
    {
        UnregisterHotKey(_handle, HotkeyId);
        var parts = gesture.Split('+', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        uint modifiers = 0;
        uint key = 0;
        foreach (var part in parts)
        {
            if (part.Equals("Ctrl", StringComparison.OrdinalIgnoreCase)) modifiers |= 0x0002;
            else if (part.Equals("Alt", StringComparison.OrdinalIgnoreCase)) modifiers |= 0x0001;
            else if (part.Equals("Shift", StringComparison.OrdinalIgnoreCase)) modifiers |= 0x0004;
            else if (part.Equals("Win", StringComparison.OrdinalIgnoreCase)) modifiers |= 0x0008;
            else if (part.Equals("Space", StringComparison.OrdinalIgnoreCase)) key = 0x20;
            else if (part.Length == 1) key = char.ToUpperInvariant(part[0]);
        }
        return key != 0 && RegisterHotKey(_handle, HotkeyId, modifiers | 0x4000, key);
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == WmHotkey && wParam.ToInt32() == HotkeyId) { handled = true; _callback(); }
        return IntPtr.Zero;
    }

    public void Dispose()
    {
        UnregisterHotKey(_handle, HotkeyId);
        _source.RemoveHook(WndProc);
    }

    [DllImport("user32.dll")] private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);
    [DllImport("user32.dll")] private static extern bool UnregisterHotKey(IntPtr hWnd, int id);
}
