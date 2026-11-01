using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;

namespace Ramreader.Trainer.Services;

/// <summary>Hotkey global (jalan walau game yang sedang fokus) lewat RegisterHotKey.</summary>
public sealed class HotkeyService : IDisposable
{
    private const int WmHotkey = 0x0312;
    private const uint ModNoRepeat = 0x4000;

    private readonly IntPtr _hwnd;
    private readonly HwndSource _source;
    private readonly Dictionary<int, Action> _actions = new();
    private int _nextId = 1;

    public HotkeyService(Window window)
    {
        _hwnd = new WindowInteropHelper(window).Handle;
        _source = HwndSource.FromHwnd(_hwnd) ?? throw new InvalidOperationException("Jendela belum siap.");
        _source.AddHook(WndProc);
    }

    /// <summary>Daftarkan satu hotkey. Mengembalikan pesan masalah, atau null kalau berhasil.</summary>
    public string? Register(string text, Action action)
    {
        if (!TryParse(text, out uint modifiers, out uint vk))
            return "tulis seperti F1, Ctrl+F2, atau Alt+Shift+K";

        int id = _nextId++;
        if (!RegisterHotKey(_hwnd, id, modifiers | ModNoRepeat, vk))
            return $"\"{text.Trim()}\" tidak bisa dipakai (sudah dipakai program lain atau fitur lain)";

        _actions[id] = action;
        return null;
    }

    public void UnregisterAll()
    {
        foreach (int id in _actions.Keys)
            UnregisterHotKey(_hwnd, id);
        _actions.Clear();
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == WmHotkey && _actions.TryGetValue(wParam.ToInt32(), out var action))
        {
            try { action(); } catch { }
            handled = true;
        }
        return IntPtr.Zero;
    }

    private static bool TryParse(string? text, out uint modifiers, out uint vk)
    {
        modifiers = 0;
        vk = 0;

        var parts = (text ?? "").Split('+', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0) return false;

        for (int i = 0; i < parts.Length - 1; i++)
        {
            switch (parts[i].ToLowerInvariant())
            {
                case "ctrl": case "control": modifiers |= 0x0002; break;
                case "alt": modifiers |= 0x0001; break;
                case "shift": modifiers |= 0x0004; break;
                case "win": modifiers |= 0x0008; break;
                default: return false;
            }
        }

        try
        {
            var converted = new KeyConverter().ConvertFromInvariantString(parts[^1]);
            if (converted is not Key key || key == Key.None) return false;

            int code = KeyInterop.VirtualKeyFromKey(key);
            if (code == 0) return false;

            vk = (uint)code;
            return true;
        }
        catch
        {
            return false;
        }
    }

    public void Dispose()
    {
        UnregisterAll();
        _source.RemoveHook(WndProc);
    }

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool UnregisterHotKey(IntPtr hWnd, int id);
}