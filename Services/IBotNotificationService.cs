using Telegram.Bot.Types;

namespace TelegramRAT.Services;

public interface IBotNotificationService
{
    Task SendSuccessAsync(Message message, string successMessage);
    Task SendErrorAsync(Message message, Exception ex, bool includeStackTrace = false);
    Task SendInfoAsync(Message message, string infoMessage);
    Task ReportExceptionAsync(Message message, Exception exception);
}
