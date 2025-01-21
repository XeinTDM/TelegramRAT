namespace TelegramRAT.Commands;

public class BotCommand
{
    public string Command { get; init; }
    public string[] Aliases { get; init; } = Array.Empty<string>();
    public string Description { get; init; }
    public string Example { get; init; }
    public int ArgsCount { get; init; } = -1;
    public Func<BotCommandModel, Task> Execute { get; init; }

    public bool ValidateModel(BotCommandModel model) =>
        model != null &&
        string.Equals(Command, model.Command, StringComparison.OrdinalIgnoreCase) &&
        ArgsCount switch
        {
            -1 => true,
            -2 => model.Args.Length > 0,
            _ => ArgsCount == model.Args.Length
        };
}
