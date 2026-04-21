using System.Net.Mail;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using SharedKernel.Domain;
using SharedKernel.Services;
using Xunit;

namespace SharedKernel.Tests.Services;

/// <summary>
/// Unit tests for <see cref="EmailAlerter"/>. Asserts the "Critical-only" policy:
/// Info/Warning/Error must never trigger an SMTP send. Critical always does.
/// Uses a mocked <see cref="ISmtpClient"/> to avoid opening real sockets.
/// </summary>
public sealed class EmailAlerterTests
{
    private static EmailAlerterConfig BuildValidConfig() => new()
    {
        Enabled = true,
        Host = "smtp.example.com",
        Port = 587,
        From = "alerts@example.com",
        To = "ops@example.com",
        EnableSsl = true
    };

    [Fact]
    public async Task SendImmediate_WithCritical_CallsSmtp()
    {
        // Arrange
        Mock<ISmtpClient> smtp = new();
        smtp.Setup(x => x.SendMailAsync(It.IsAny<MailMessage>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        EmailAlerter sut = new(BuildValidConfig(), smtp.Object, NullLogger<EmailAlerter>.Instance);

        // Act
        await sut.SendImmediateAsync(AlertSeverity.Critical, "IBKR down", "connection lost", CancellationToken.None);

        // Assert
        smtp.Verify(x => x.SendMailAsync(It.IsAny<MailMessage>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Theory]
    [InlineData(AlertSeverity.Info)]
    [InlineData(AlertSeverity.Warning)]
    [InlineData(AlertSeverity.Error)]
    public async Task SendImmediate_WithNonCritical_DoesNotCallSmtp(AlertSeverity severity)
    {
        Mock<ISmtpClient> smtp = new();
        EmailAlerter sut = new(BuildValidConfig(), smtp.Object, NullLogger<EmailAlerter>.Instance);

        await sut.SendImmediateAsync(severity, "t", "m", CancellationToken.None);

        smtp.Verify(x => x.SendMailAsync(It.IsAny<MailMessage>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task SendImmediate_WhenDisabled_DoesNotCallSmtp()
    {
        Mock<ISmtpClient> smtp = new();
        EmailAlerter sut = new(BuildValidConfig() with { Enabled = false }, smtp.Object, NullLogger<EmailAlerter>.Instance);

        await sut.SendImmediateAsync(AlertSeverity.Critical, "t", "m", CancellationToken.None);

        smtp.Verify(x => x.SendMailAsync(It.IsAny<MailMessage>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task SendDigest_WithCriticalAndEntries_SendsSingleMail()
    {
        Mock<ISmtpClient> smtp = new();
        smtp.Setup(x => x.SendMailAsync(It.IsAny<MailMessage>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        EmailAlerter sut = new(BuildValidConfig(), smtp.Object, NullLogger<EmailAlerter>.Instance);

        List<(string title, string message)> entries = new()
        {
            ("margin high", "margin=78%"),
            ("semaphore red", "6h"),
        };

        await sut.SendDigestAsync(AlertSeverity.Critical, entries, CancellationToken.None);

        smtp.Verify(x => x.SendMailAsync(
            It.Is<MailMessage>(m => m.Subject.Contains("digest") && m.Body.Contains("margin high") && m.Body.Contains("semaphore red")),
            It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task SendDigest_WithWarning_DoesNotSend()
    {
        Mock<ISmtpClient> smtp = new();
        EmailAlerter sut = new(BuildValidConfig(), smtp.Object, NullLogger<EmailAlerter>.Instance);

        await sut.SendDigestAsync(AlertSeverity.Warning, new List<(string, string)> { ("t", "m") }, CancellationToken.None);

        smtp.Verify(x => x.SendMailAsync(It.IsAny<MailMessage>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task SendImmediate_WhenSmtpThrows_SwallowsException()
    {
        // Background workers must survive SMTP outages — EmailAlerter must not re-raise.
        Mock<ISmtpClient> smtp = new();
        smtp.Setup(x => x.SendMailAsync(It.IsAny<MailMessage>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new SmtpException("connection refused"));
        EmailAlerter sut = new(BuildValidConfig(), smtp.Object, NullLogger<EmailAlerter>.Instance);

        // Should not throw
        await sut.SendImmediateAsync(AlertSeverity.Critical, "t", "m", CancellationToken.None);
    }

    [Fact]
    public async Task Constructor_WithInvalidConfig_DisablesAlerter()
    {
        // Missing From should flip Enabled to false — validated at construction.
        EmailAlerterConfig bad = new()
        {
            Enabled = true,
            Host = "smtp.example.com",
            Port = 587,
            From = "",
            To = "ops@example.com"
        };

        Mock<ISmtpClient> smtp = new();
        EmailAlerter sut = new(bad, smtp.Object, NullLogger<EmailAlerter>.Instance);

        // Sending Critical should not touch SMTP because config was rejected.
        await sut.SendImmediateAsync(AlertSeverity.Critical, "t", "m", CancellationToken.None);
        smtp.Verify(x => x.SendMailAsync(It.IsAny<MailMessage>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
