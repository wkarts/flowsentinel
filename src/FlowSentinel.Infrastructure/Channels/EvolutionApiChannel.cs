using System.Net.Http.Json;
using System.Text.Json;
using FlowSentinel.Application;
using FlowSentinel.Domain;

namespace FlowSentinel.Infrastructure.Channels;

internal sealed class EvolutionApiChannel : INotificationChannel, IEvolutionInstanceService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ISecretProtector _secretProtector;

    public EvolutionApiChannel(IHttpClientFactory httpClientFactory, ISecretProtector secretProtector)
    {
        _httpClientFactory = httpClientFactory;
        _secretProtector = secretProtector;
    }

    public ChannelType ChannelType => ChannelType.EvolutionApi;

    public async Task<DeliveryResult> SendAsync(
        ChannelConfiguration configuration,
        DeliveryRequest request,
        CancellationToken cancellationToken)
    {
        var settings = Parse(configuration);
        var number = NormalizeNumber(request.Recipient);
        if (number.Length < 10)
        {
            return DeliveryResult.Failed("Número do WhatsApp inválido.", transient: false);
        }

        var payload = settings.IsV1Payload
            ? new Dictionary<string, object?>
            {
                ["number"] = number,
                ["textMessage"] = new Dictionary<string, object?> { ["text"] = request.Message }
            }
            : new Dictionary<string, object?>
            {
                ["number"] = number,
                ["text"] = request.Message
            };

        using var response = await SendAsync(
            settings,
            HttpMethod.Post,
            settings.GetSendTextPath(),
            payload,
            cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            return DeliveryResult.Failed(
                $"Evolution API retornou {(int)response.StatusCode}: {body}",
                transient: (int)response.StatusCode >= 500 || (int)response.StatusCode == 429);
        }

        return DeliveryResult.Sent(TryExtractMessageId(body));
    }

    public async Task<EvolutionInstanceStatus> GetStatusAsync(
        ChannelConfiguration configuration,
        CancellationToken cancellationToken)
    {
        var settings = Parse(configuration);
        using var response = await SendAsync(
            settings,
            HttpMethod.Get,
            settings.GetConnectionStatePath(),
            null,
            cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        var state = TryFindString(body, "state", "status", "connectionStatus") ?? response.StatusCode.ToString();
        var normalizedState = state.Trim().ToLowerInvariant();
        return new EvolutionInstanceStatus
        {
            Connected = response.IsSuccessStatusCode &&
                        normalizedState is "open" or "connected" or "online" or "ready",
            State = state,
            RawResponse = body
        };
    }

    public async Task<EvolutionQrCodeResult> GetQrCodeAsync(
        ChannelConfiguration configuration,
        CancellationToken cancellationToken)
    {
        var settings = Parse(configuration);
        using var response = await SendAsync(
            settings,
            HttpMethod.Get,
            settings.GetConnectPath(),
            null,
            cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException($"Evolution API retornou {(int)response.StatusCode}: {body}");
        }

        return new EvolutionQrCodeResult
        {
            Base64Image = TryFindString(body, "base64", "qrcode", "qrCode"),
            PairingCode = TryFindString(body, "pairingCode", "code"),
            RawResponse = body
        };
    }

    private EvolutionApiSettings Parse(ChannelConfiguration configuration)
    {
        var settings = JsonSerializer.Deserialize<EvolutionApiSettings>(configuration.SettingsJson, FlowJson.Options)
                       ?? throw new InvalidOperationException("Configuração da Evolution API inválida.");
        settings.ApiKey = _secretProtector.UnprotectIfNeeded(settings.ApiKey);
        if (string.IsNullOrWhiteSpace(settings.BaseUrl) || string.IsNullOrWhiteSpace(settings.Instance))
        {
            throw new InvalidOperationException("Informe BaseUrl e Instance da Evolution API.");
        }
        return settings;
    }

    private async Task<HttpResponseMessage> SendAsync(
        EvolutionApiSettings settings,
        HttpMethod method,
        string path,
        object? payload,
        CancellationToken cancellationToken)
    {
        var client = _httpClientFactory.CreateClient(nameof(EvolutionApiChannel));
        client.Timeout = TimeSpan.FromSeconds(Math.Clamp(settings.TimeoutSeconds, 5, 300));
        var baseUrl = settings.BaseUrl.TrimEnd('/');
        var normalizedPath = path.StartsWith('/') ? path : "/" + path;
        using var message = new HttpRequestMessage(method, baseUrl + normalizedPath);
        if (!string.IsNullOrWhiteSpace(settings.ApiKey))
        {
            message.Headers.TryAddWithoutValidation(settings.ApiKeyHeader, settings.ApiKey);
        }
        if (payload is not null)
        {
            message.Content = JsonContent.Create(payload, options: FlowJson.Options);
        }
        return await client.SendAsync(message, cancellationToken);
    }

    private static string NormalizeNumber(string value) => new(value.Where(char.IsDigit).ToArray());

    private static string? TryExtractMessageId(string json) =>
        TryFindString(json, "id", "messageId", "message_id");

    private static string? TryFindString(string json, params string[] names)
    {
        try
        {
            using var document = JsonDocument.Parse(json);
            return Find(document.RootElement, names);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static string? Find(JsonElement element, IReadOnlyCollection<string> names)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            foreach (var property in element.EnumerateObject())
            {
                if (names.Any(name => string.Equals(name, property.Name, StringComparison.OrdinalIgnoreCase)) &&
                    property.Value.ValueKind is JsonValueKind.String or JsonValueKind.Number)
                {
                    return property.Value.ToString();
                }

                var nested = Find(property.Value, names);
                if (nested is not null)
                {
                    return nested;
                }
            }
        }
        else if (element.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in element.EnumerateArray())
            {
                var nested = Find(item, names);
                if (nested is not null)
                {
                    return nested;
                }
            }
        }

        return null;
    }
}
