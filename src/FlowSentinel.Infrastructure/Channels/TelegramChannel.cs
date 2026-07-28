using System.Net.Http.Json;
using System.Text.Json;
using FlowSentinel.Application;
using FlowSentinel.Domain;

namespace FlowSentinel.Infrastructure.Channels;

internal sealed class TelegramChannel : INotificationChannel
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ISecretProtector _secretProtector;

    public TelegramChannel(IHttpClientFactory httpClientFactory, ISecretProtector secretProtector)
    {
        _httpClientFactory = httpClientFactory;
        _secretProtector = secretProtector;
    }

    public ChannelType ChannelType => ChannelType.Telegram;

    public async Task<DeliveryResult> SendAsync(
        ChannelConfiguration configuration,
        DeliveryRequest request,
        CancellationToken cancellationToken)
    {
        var settings = JsonSerializer.Deserialize<TelegramSettings>(configuration.SettingsJson, FlowJson.Options)
                       ?? throw new InvalidOperationException("Configuração do Telegram inválida.");
        var token = _secretProtector.UnprotectIfNeeded(settings.BotToken);
        if (string.IsNullOrWhiteSpace(token))
        {
            return DeliveryResult.Failed("Token do Telegram não informado.", transient: false);
        }

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(Math.Clamp(settings.TimeoutSeconds, 5, 300)));
        var client = _httpClientFactory.CreateClient(nameof(TelegramChannel));
        var payload = new Dictionary<string, object?>
        {
            ["chat_id"] = request.Recipient,
            ["text"] = request.Message,
            ["disable_notification"] = settings.DisableNotification
        };
        if (!string.IsNullOrWhiteSpace(settings.ParseMode))
        {
            payload["parse_mode"] = settings.ParseMode;
        }

        using var response = await client.PostAsJsonAsync(
            $"https://api.telegram.org/bot{token}/sendMessage",
            payload,
            FlowJson.Options,
            timeout.Token);
        var body = await response.Content.ReadAsStringAsync(timeout.Token);
        if (!response.IsSuccessStatusCode)
        {
            return DeliveryResult.Failed(
                $"Telegram retornou {(int)response.StatusCode}: {body}",
                transient: (int)response.StatusCode >= 500 || (int)response.StatusCode == 429);
        }

        string? messageId = null;
        try
        {
            using var document = JsonDocument.Parse(body);
            if (document.RootElement.TryGetProperty("result", out var result) &&
                result.TryGetProperty("message_id", out var id))
            {
                messageId = id.ToString();
            }
        }
        catch (JsonException)
        {
        }

        return DeliveryResult.Sent(messageId);
    }
}
