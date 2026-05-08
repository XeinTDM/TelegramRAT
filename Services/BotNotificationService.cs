using Telegram.Bot.Types.ReplyMarkups;
using Telegram.Bot.Types;
using Telegram.Bot;
using System.Net;

namespace TelegramRAT.Services;

public class BotNotificationService(ITelegramBotClient botClient) : IBotNotificationService
{
    public async Task SendSuccessAsync(Message message, string successMessage)
        => await botClient.SendMessage(message.Chat.Id, successMessage, parseMode: Telegram.Bot.Types.Enums.ParseMode.Html, replyMarkup: new InlineKeyboardMarkup(InlineKeyboardButton.WithCallbackData("Reply", message.MessageId.ToString())));

    public async Task SendInfoAsync(Message message, string infoMessage)
        => await botClient.SendMessage(message.Chat.Id, infoMessage, parseMode: Telegram.Bot.Types.Enums.ParseMode.Html, replyMarkup: new InlineKeyboardMarkup(InlineKeyboardButton.WithCallbackData("Reply", message.MessageId.ToString())));

    public async Task SendErrorAsync(Message message, Exception ex, bool includeStackTrace = false)
    {
        var encodedMessage = WebUtility.HtmlEncode(ex.Message);
        var errorMessage = includeStackTrace
            ? $"Error: {encodedMessage}\n{WebUtility.HtmlEncode(ex.StackTrace)}"
            : $"Error: {encodedMessage}";

        var replyMarkup = message.ReplyMarkup == null
            ? new InlineKeyboardMarkup(InlineKeyboardButton.WithCallbackData("Reply", message.MessageId.ToString()))
            : null;

        await botClient.SendMessage(
            message.Chat.Id,
            errorMessage,
            parseMode: Telegram.Bot.Types.Enums.ParseMode.Html,
            replyMarkup: replyMarkup
        );
    }

    public async Task ReportExceptionAsync(Message message, Exception exception)
    {
#if DEBUG
        bool includeStackTrace = true;
#else
        bool includeStackTrace = false;
#endif
        await SendErrorAsync(message, exception, includeStackTrace);
    }
}
