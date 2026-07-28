namespace FlowSentinel.Infrastructure.Channels;

internal sealed class TelegramSettings
{
    public string BotToken { get; set; } = string.Empty;
    public string? ParseMode { get; set; }
    public bool DisableNotification { get; set; }
    public int TimeoutSeconds { get; set; } = 30;
}

internal sealed class EvolutionApiSettings
{
    public string BaseUrl { get; set; } = string.Empty;
    public string ApiKey { get; set; } = string.Empty;
    public string ApiKeyHeader { get; set; } = "apikey";
    public string Instance { get; set; } = string.Empty;
    public string ApiVersion { get; set; } = "V2";
    public string? SendTextPathTemplate { get; set; }
    public string? ConnectionStatePathTemplate { get; set; }
    public string? ConnectPathTemplate { get; set; }
    public string? PayloadMode { get; set; }
    public int TimeoutSeconds { get; set; } = 30;

    public string GetSendTextPath() => Expand(
        SendTextPathTemplate ?? "/message/sendText/{instance}");

    public string GetConnectionStatePath() => Expand(
        ConnectionStatePathTemplate ?? "/instance/connectionState/{instance}");

    public string GetConnectPath() => Expand(
        ConnectPathTemplate ?? "/instance/connect/{instance}");

    public bool IsV1Payload => string.Equals(PayloadMode ?? ApiVersion, "V1", StringComparison.OrdinalIgnoreCase);

    private string Expand(string value) => value.Replace(
        "{instance}",
        Uri.EscapeDataString(Instance),
        StringComparison.OrdinalIgnoreCase);
}

internal sealed class SmtpSettings
{
    public string Host { get; set; } = string.Empty;
    public int Port { get; set; } = 587;
    public string Security { get; set; } = "StartTls";
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string FromAddress { get; set; } = string.Empty;
    public string FromName { get; set; } = "FlowSentinel";
    public bool IsHtml { get; set; }
    public int TimeoutSeconds { get; set; } = 30;
}
