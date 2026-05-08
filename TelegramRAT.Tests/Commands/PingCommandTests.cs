using Moq;
using Telegram.Bot;
using Telegram.Bot.Types;
using TelegramRAT.Commands.Core;
using TelegramRAT.Commands;
using Xunit;
using Telegram.Bot.Requests;
using Telegram.Bot.Requests.Abstractions;

namespace TelegramRAT.Tests.Commands;

public class PingCommandTests
{
    [Fact]
    public async Task ExecuteAsync_SendsPingAndElapsedTime()
    {
        // Arrange
        var botMock = new Mock<ITelegramBotClient>();
        botMock.Setup(b => b.SendRequest(It.IsAny<IRequest<Message>>(), It.IsAny<CancellationToken>())).ReturnsAsync(new Message());
        var command = new PingCommand(botMock.Object);
        var message = TestHelpers.CreateMessage(123, 456);
        message.Date = DateTime.UtcNow.AddSeconds(-1);
        var model = new BotCommandModel
        {
            Command = "ping",
            Message = message
        };

        // Act
        await command.ExecuteAsync(model);

        // Assert
        // We expect two messages: "Ping!" and "Elapsed time: ... ms"
        botMock.Verify(b => b.SendRequest(
            It.Is<SendMessageRequest>(r => r.Text == "Ping!" && r.ChatId.Identifier == 123),
            It.IsAny<CancellationToken>()), 
            Times.Once);

        botMock.Verify(b => b.SendRequest(
            It.Is<SendMessageRequest>(r => r.Text.StartsWith("Elapsed time:") && r.ChatId.Identifier == 123),
            It.IsAny<CancellationToken>()), 
            Times.Once);
    }
}
