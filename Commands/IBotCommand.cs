using TelegramRAT.Commands;

namespace TelegramRAT.Commands;

public interface IBotCommand
{
    string Command { get; }
    string[] Aliases { get; }
    string Description { get; }
    string Example { get; }
    int ArgsCount { get; }
    Task ExecuteAsync(BotCommandModel model);
    bool Validate(BotCommandModel model);
}
