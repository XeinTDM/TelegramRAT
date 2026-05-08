using Moq;
using Telegram.Bot;
using Telegram.Bot.Types;
using TelegramRAT.Commands.Core;
using TelegramRAT.Commands;
using Xunit;
using Telegram.Bot.Requests;
using Telegram.Bot.Requests.Abstractions;

namespace TelegramRAT.Tests.Commands;

public class HelpCommandTests
{
    [Fact]
    public async Task ExecuteAsync_WithNoArgs_SendsDefaultHelp()
    {
        // Arrange
        var botMock = new Mock<ITelegramBotClient>();
        botMock.Setup(b => b.SendRequest(It.IsAny<IRequest<Message>>(), It.IsAny<CancellationToken>())).ReturnsAsync(new Message());
        var commands = new List<IBotCommand>();
        var command = new HelpCommand(botMock.Object, commands);
        var model = new BotCommandModel
        {
            Command = "help",
            Args = Array.Empty<string>(),
            Message = TestHelpers.CreateMessage(123, 456)
        };

        // Act
        await command.ExecuteAsync(model);

        // Assert
        botMock.Verify(b => b.SendRequest(
            It.Is<SendMessageRequest>(r => r.Text.Contains("retrieve description of other commands") && r.ChatId.Identifier == 123),
            It.IsAny<CancellationToken>()), 
            Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_WithValidCommandArg_SendsCommandHelp()
    {
        // Arrange
        var botMock = new Mock<ITelegramBotClient>();
        botMock.Setup(b => b.SendRequest(It.IsAny<IRequest<Message>>(), It.IsAny<CancellationToken>())).ReturnsAsync(new Message());
        
        var mockCmd = new Mock<IBotCommand>();
        mockCmd.Setup(c => c.Command).Returns("test");
        mockCmd.Setup(c => c.Description).Returns("Test description");
        mockCmd.Setup(c => c.Aliases).Returns(new[] { "t" });
        mockCmd.Setup(c => c.Example).Returns("/test example");

        var commands = new List<IBotCommand> { mockCmd.Object };
        var command = new HelpCommand(botMock.Object, commands);
        var model = new BotCommandModel
        {
            Command = "help",
            Args = new[] { "test" },
            Message = TestHelpers.CreateMessage(123, 456)
        };

        // Act
        await command.ExecuteAsync(model);

        // Assert
        botMock.Verify(b => b.SendRequest(
            It.Is<SendMessageRequest>(r => 
                r.Text.Contains("TEST") && 
                r.Text.Contains("Test description") && 
                r.Text.Contains("Aliases: t") &&
                r.Text.Contains("/test example")),
            It.IsAny<CancellationToken>()), 
            Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_WithInvalidCommandArg_SendsErrorMessage()
    {
        // Arrange
        var botMock = new Mock<ITelegramBotClient>();
        botMock.Setup(b => b.SendRequest(It.IsAny<IRequest<Message>>(), It.IsAny<CancellationToken>())).ReturnsAsync(new Message());
        var commands = new List<IBotCommand>();
        var command = new HelpCommand(botMock.Object, commands);
        var model = new BotCommandModel
        {
            Command = "help",
            Args = new[] { "nonexistent" },
            Message = TestHelpers.CreateMessage(123, 456)
        };

        // Act
        await command.ExecuteAsync(model);

        // Assert
        botMock.Verify(b => b.SendRequest(
            It.Is<SendMessageRequest>(r => r.Text.Contains("This command doesn't exist!")),
            It.IsAny<CancellationToken>()), 
            Times.Once);
    }
}
