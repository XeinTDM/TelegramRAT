using Moq;
using Telegram.Bot;
using Telegram.Bot.Types;
using TelegramRAT.Commands.Misc;
using TelegramRAT.Commands;
using TelegramRAT.Services;
using Xunit;
using Telegram.Bot.Requests;
using Telegram.Bot.Requests.Abstractions;

namespace TelegramRAT.Tests.Commands;

public class PyCommandTests
{
    [Fact]
    public async Task ExecuteAsync_WithExpression_ExecutesAndSendsOutput()
    {
        // Arrange
        var botMock = new Mock<ITelegramBotClient>();
        botMock.Setup(b => b.SendRequest(It.IsAny<IRequest<Message>>(), It.IsAny<CancellationToken>())).ReturnsAsync(new Message());
        var notificationMock = new Mock<IBotNotificationService>();
        var pythonMock = new Mock<IPythonService>();

        string expectedOutput = "Hello from Python";
        pythonMock.Setup(p => p.Execute("print('test')", out expectedOutput));

        var command = new PyCommand(botMock.Object, notificationMock.Object, pythonMock.Object);
        var model = new BotCommandModel
        {
            Command = "py",
            RawArgs = "print('test')",
            Args = new[] { "print('test')" },
            Message = TestHelpers.CreateMessage(123, 456)
        };

        // Act
        await command.ExecuteAsync(model);

        // Assert
        botMock.Verify(b => b.SendRequest(
            It.Is<SendMessageRequest>(r => r.Text.Contains("Executed! Output:\nHello from Python")),
            It.IsAny<CancellationToken>()), 
            Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_WithNoArgs_SendsErrorMessage()
    {
        // Arrange
        var botMock = new Mock<ITelegramBotClient>();
        botMock.Setup(b => b.SendRequest(It.IsAny<IRequest<Message>>(), It.IsAny<CancellationToken>())).ReturnsAsync(new Message());
        var notificationMock = new Mock<IBotNotificationService>();
        var pythonMock = new Mock<IPythonService>();

        var command = new PyCommand(botMock.Object, notificationMock.Object, pythonMock.Object);
        var model = new BotCommandModel
        {
            Command = "py",
            Args = Array.Empty<string>(),
            Message = TestHelpers.CreateMessage(123, 456)
        };

        // Act
        await command.ExecuteAsync(model);

        // Assert
        botMock.Verify(b => b.SendRequest(
            It.Is<SendMessageRequest>(r => r.Text.Contains("Need an expression or file to execute")),
            It.IsAny<CancellationToken>()), 
            Times.Once);
    }
}
