using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using TelegramRAT.Features;
using TelegramRAT.Services;
using TelegramRAT.Utilities;
using Xunit;

namespace TelegramRAT.Tests;

public class NetworkServiceTests
{
    [Fact]
    public async Task GetIpAddressAndJson_ParsesCorrectly()
    {
        var responses = new Dictionary<string, string>
        {
            ["https://api.ipify.org/?format=json"] = "{\"ip\":\"203.0.113.42\"}",
            ["http://ip-api.com/json/203.0.113.42"] = "{\"status\":\"success\",\"isp\":\"ExampleISP\",\"country\":\"Wonderland\",\"city\":\"Fictionville\",\"timezone\":\"UTC+0\",\"countryCode\":\"WL\",\"lat\":51.5074,\"lon\":-0.1278}"
        };

        var handler = new StubHttpMessageHandler(responses);
        var httpClient = new HttpClient(handler)
        {
            Timeout = TimeSpan.FromSeconds(10)
        };

        var factory = new StubHttpClientFactory(httpClient);
        var networkService = new NetworkService(factory);

        var ipAddress = await networkService.GetIpAddressAsync();
        var networkInfo = await networkService.GetFromJsonAsync<NetworkInfo>("http://ip-api.com/json/" + ipAddress);

        Assert.Equal("203.0.113.42", ipAddress);
        Assert.Equal("ExampleISP", networkInfo.Isp);
        Assert.Equal("Wonderland", networkInfo.Country);
        Assert.Equal("Fictionville", networkInfo.City);
        Assert.Equal("UTC+0", networkInfo.Timezone);
        Assert.Equal("WL", networkInfo.CountryCode);
        Assert.Equal(51.5074, networkInfo.Lat.GetValueOrDefault());
        Assert.Equal(-0.1278, networkInfo.Lon.GetValueOrDefault());

        Assert.Equal(2, handler.RequestCount);
    }

    private sealed class StubHttpClientFactory : IHttpClientFactory
    {
        private readonly HttpClient _client;
        public StubHttpClientFactory(HttpClient client) => _client = client;
        public HttpClient CreateClient(string name) => _client;
    }

    private sealed class StubHttpMessageHandler : HttpMessageHandler
    {
        private readonly IDictionary<string, string> _responses;

        public StubHttpMessageHandler(IDictionary<string, string> responses)
        {
            _responses = responses;
        }

        public int RequestCount { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            RequestCount++;
            var uri = request.RequestUri!.ToString();

            if (_responses.TryGetValue(uri, out var content))
            {
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(content, Encoding.UTF8, "application/json")
                });
            }

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound));
        }
    }
}
