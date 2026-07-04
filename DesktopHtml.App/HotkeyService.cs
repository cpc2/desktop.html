using System.Runtime.InteropServices;
using System.Windows.Interop;

namespace DesktopHtml.App;

/// <summary>
/// System-wide hotkeys for skins via RegisterHotKey and a hidden message-only
/// window. Must be created on the UI thread (needs its message pump).
/// Registrations are owned by callers (the bridge dispatcher scopes them to
/// the page and releases them on reload).
/// </summary>
public sealed class HotkeyService : IDisposable
{
    private const int WM_HOTKEY = 0x0312;
    private const uint MOD_ALT = 0x0001;
    private const uint MOD_CONTROL = 0x0002;
    private const uint MOD_SHIFT = 0x0004;
    private const uint MOD_WIN = 0x0008;
    private const uint MOD_NOREPEAT = 0x4000;
    private static readonly IntPtr HWND_MESSAGE = new(-3);

    public static HotkeyService? Instance { get; private set; }

    private readonly HwndSource _source;
    private readonly Dictionary<int, Action> _handlers = new();
    private int _nextId = 1;

    public static HotkeyService Initialize()
    {
        Instance ??= new HotkeyService();
        return Instance;
    }

    private HotkeyService()
    {
        _source = new HwndSource(new HwndSourceParameters("desktop-html-hotkeys")
        {
            Width = 0,
            Height = 0,
            WindowStyle = 0,
            ParentWindow = HWND_MESSAGE
        });
        _source.AddHook(WndProc);
    }

    /// <summary>Registers a hotkey. Throws when the combination is taken.</summary>
    public int Register(IReadOnlyList<string> modifiers, string key, Action pressed)
    {
        var mods = MOD_NOREPEAT;
        foreach (var modifier in modifiers)
        {
            mods |= modifier.ToLowerInvariant() switch
            {
                "alt" => MOD_ALT,
                "ctrl" or "control" => MOD_CONTROL,
                "shift" => MOD_SHIFT,
                "win" or "meta" or "super" => MOD_WIN,
                _ => throw new InvalidOperationException($"Unknown hotkey modifier '{modifier}'. Use ctrl, alt, shift, or win.")
            };
        }

        var virtualKey = ParseKey(key);
        return System.Windows.Application.Current.Dispatcher.Invoke(() =>
        {
            var id = _nextId++;
            if (!RegisterHotKey(_source.Handle, id, mods, virtualKey))
            {
                var error = Marshal.GetLastWin32Error();
                throw new InvalidOperationException(error == 1409
                    ? $"Hotkey '{string.Join("+", modifiers)}+{key}' is already registered by another application."
                    : $"Hotkey registration failed (error {error}).");
            }

            _handlers[id] = pressed;
            return id;
        });
    }

    public bool Unregister(int hotkeyId)
    {
        return System.Windows.Application.Current.Dispatcher.Invoke(() =>
        {
            if (!_handlers.Remove(hotkeyId))
            {
                return false;
            }

            UnregisterHotKey(_source.Handle, hotkeyId);
            return true;
        });
    }

    public void Dispose()
    {
        foreach (var id in _handlers.Keys.ToArray())
        {
            UnregisterHotKey(_source.Handle, id);
        }

        _handlers.Clear();
        _source.RemoveHook(WndProc);
        _source.Dispose();
        Instance = null;
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == WM_HOTKEY && _handlers.TryGetValue((int)wParam, out var pressed))
        {
            handled = true;
            try
            {
                pressed();
            }
            catch
            {
            }
        }

        return IntPtr.Zero;
    }

    private static uint ParseKey(string key)
    {
        var normalized = key.Trim().ToUpperInvariant();

        if (normalized.Length == 1)
        {
            var ch = normalized[0];
            if (ch is >= 'A' and <= 'Z' or >= '0' and <= '9')
            {
                return ch;
            }
        }

        if (normalized.Length is 2 or 3 && normalized[0] == 'F' &&
            int.TryParse(normalized[1..], out var fn) && fn is >= 1 and <= 24)
        {
            return (uint)(0x6F + fn); // VK_F1 = 0x70
        }

        return normalized switch
        {
            "SPACE" => 0x20,
            "ENTER" or "RETURN" => 0x0D,
            "TAB" => 0x09,
            "ESCAPE" or "ESC" => 0x1B,
            "BACKSPACE" => 0x08,
            "INSERT" => 0x2D,
            "DELETE" => 0x2E,
            "HOME" => 0x24,
            "END" => 0x23,
            "PAGEUP" => 0x21,
            "PAGEDOWN" => 0x22,
            "UP" => 0x26,
            "DOWN" => 0x28,
            "LEFT" => 0x25,
            "RIGHT" => 0x27,
            "PRINTSCREEN" => 0x2C,
            "PAUSE" => 0x13,
            _ => throw new InvalidOperationException($"Unknown hotkey key '{key}'. Use a letter, digit, F1-F24, or a named key (space, enter, escape, arrows, ...).")
        };
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool UnregisterHotKey(IntPtr hWnd, int id);
}
