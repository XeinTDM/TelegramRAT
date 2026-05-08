using Telegram.Bot;
using Telegram.Bot.Types.Enums;

namespace TelegramRAT.Commands.Misc;

public class GetIdCommand(ITelegramBotClient botClient) : AbstractBotCommand
{
    public override string Command => "getid";
    public override string Description => "Get chat or user id. To get user's id type this command as answer to user message.";
    public override int ArgsCount => 0;
    public override string Example => "/getid";

    public override async Task ExecuteAsync(BotCommandModel model)
    {
        if (model.Message == null) return;

        if (model.Message.ReplyToMessage != null)
        {
            await botClient.SendMessage(model.Message.Chat.Id, $"User id: <code>{model.Message.ReplyToMessage.From?.Id.ToString() ?? "Unknown"}</code>", ParseMode.Html, replyParameters: new Telegram.Bot.Types.ReplyParameters { MessageId = model.Message.MessageId });
            return;
        }
        await botClient.SendMessage(model.Message.Chat.Id, $"This chat id: <code>{model.Message.Chat.Id}</code>", ParseMode.Html);
    }
}
