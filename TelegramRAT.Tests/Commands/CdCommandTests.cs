using Moq;
using Telegram.Bot.Types;
using TelegramRAT.Commands.File;
using TelegramRAT.Commands;
using TelegramRAT.Services;
using Xunit;

namespace TelegramRAT.Tests.Commands;

public class CdCommandTests
{
    [Fact]
    public async Task ExecuteAsync_WithValidPath_ChangesDirectory()
    {
        // Arrange
        var notificationMock = new Mock<IBotNotificationService>();
        var fileSystemMock = new Mock<IFileSystemService>();

        fileSystemMock.Setup(f => f.SanitizePath(It.IsAny<string>())).Returns("C:\\TestDir");
        fileSystemMock.Setup(f => f.GetFullPath("C:\\TestDir")).Returns("C:\\TestDir");
        fileSystemMock.Setup(f => f.DirectoryExists("C:\\TestDir")).Returns(true);
        fileSystemMock.Setup(f => f.GetCurrentDirectory()).Returns("C:\\TestDir");

        var command = new CdCommand(notificationMock.Object, fileSystemMock.Object);
        var model = new BotCommandModel
        {
            Command = "cd",
            Args = new[] { "C:\\TestDir" },
            Message = new Message { Chat = new Chat { Id = 123 } }
        };

        // Act
        await command.ExecuteAsync(model);

        // Assert
        fileSystemMock.Verify(f => f.SetCurrentDirectory("C:\\TestDir"), Times.Once);
        notificationMock.Verify(n => n.SendSuccessAsync(It.IsAny<Message>(), It.Is<string>(s => s.Contains("C:\\TestDir"))), Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_WithInvalidPath_ReportsError()
    {
        // Arrange
        var notificationMock = new Mock<IBotNotificationService>();
        var fileSystemMock = new Mock<IFileSystemService>();

        fileSystemMock.Setup(f => f.SanitizePath(It.IsAny<string>())).Returns("InvalidDir");
        fileSystemMock.Setup(f => f.GetFullPath("InvalidDir")).Returns("C:\\InvalidDir");
        fileSystemMock.Setup(f => f.DirectoryExists("C:\\InvalidDir")).Returns(false);

        var command = new CdCommand(notificationMock.Object, fileSystemMock.Object);
        var model = new BotCommandModel
        {
            Command = "cd",
            Args = new[] { "InvalidDir" },
            Message = new Message { Chat = new Chat { Id = 123 } }
        };

        // Act
        await command.ExecuteAsync(model);

        // Assert
        fileSystemMock.Verify(f => f.SetCurrentDirectory(It.IsAny<string>()), Times.Never);
        notificationMock.Verify(n => n.SendErrorAsync(It.IsAny<Message>(), It.IsAny<DirectoryNotFoundException>(), false), Times.Once);
    }
}
