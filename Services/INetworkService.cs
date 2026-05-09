using System.Text.Json;

namespace TelegramRAT.Services;

public interface INetworkService
{
    Task<string> GetIpAddressAsync(CancellationToken cancellationToken = default);
    Task<T> GetFromJsonAsync<T>(string requestUri, CancellationToken cancellationToken = default);
}

public class NetworkService(IHttpClientFactory httpClientFactory) : INetworkService
{
    public async Task<string> GetIpAddressAsync(CancellationToken cancellationToken = default)
    {
        var httpClient = httpClientFactory.CreateClient();
        using var response = await httpClient.GetAsync(
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

    public async Task<T> GetFromJsonAsync<T>(string requestUri, CancellationToken cancellationToken = default)
    {
        var httpClient = httpClientFactory.CreateClient();
        using var response = await httpClient.GetAsync(
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
}
