using System.Threading.Tasks;
using TelegramRAT.Utilities;
using Xunit;

namespace TelegramRAT.Tests.Utilities;

public class UtilsTests
{
    [Fact]
    public async Task GetIpAddressAsync_ReturnsValue()
    {
        string ip = await Utils.GetIpAddressAsync();
        Assert.False(string.IsNullOrWhiteSpace(ip));
    }

    [Fact]
    public void GetWindowsVersion_NotEmpty()
    {
        string version = Utils.GetWindowsVersion();
        Assert.NotNull(version);
    }
}
