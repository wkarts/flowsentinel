using System.Text.Json;
using FlowSentinel.Application;
using FlowSentinel.Domain;
using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;

namespace FlowSentinel.Infrastructure.Channels;

internal sealed class EmailChannel : INotificationChannel
{
    private readonly ISecretProtector _secretProtector;

    public EmailChannel(ISecretProtector secretProtector)
    {
        _secretProtector = secretProtector;
    }

    public ChannelType ChannelType => ChannelType.Email;

    public async Task<DeliveryResult> SendAsync(
        ChannelConfiguration configuration,
        DeliveryRequest request,
        CancellationToken cancellationToken)
    {
        var settings = JsonSerializer.Deserialize<SmtpSettings>(configuration.SettingsJson, FlowJson.Options)
                       ?? throw new InvalidOperationException("Configuração SMTP inválida.");
        settings.Password = _secretProtector.UnprotectIfNeeded(settings.Password);

        try
        {
            var message = new MimeMessage();
            message.From.Add(new MailboxAddress(settings.FromName, settings.FromAddress));
            message.To.Add(MailboxAddress.Parse(request.Recipient));
            message.Subject = request.Subject;
            message.Body = new TextPart(settings.IsHtml ? "html" : "plain")
            {
                Text = request.Message
            };

            using var client = new SmtpClient
            {
                Timeout = Math.Clamp(settings.TimeoutSeconds, 5, 300) * 1000
            };
            var security = Enum.TryParse<SecureSocketOptions>(settings.Security, true, out var parsed)
                ? parsed
                : SecureSocketOptions.StartTls;
            await client.ConnectAsync(settings.Host, settings.Port, security, cancellationToken);
            if (!string.IsNullOrWhiteSpace(settings.Username))
            {
                await client.AuthenticateAsync(settings.Username, settings.Password, cancellationToken);
            }
            var messageId = await client.SendAsync(message, cancellationToken);
            await client.DisconnectAsync(true, cancellationToken);
            return DeliveryResult.Sent(messageId);
        }
        catch (FormatException exception)
        {
            return DeliveryResult.Failed(exception.Message, transient: false);
        }
        catch (Exception exception)
        {
            return DeliveryResult.Failed(exception.Message, transient: true);
        }
    }
}
