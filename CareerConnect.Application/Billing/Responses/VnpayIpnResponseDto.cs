using System.Text.Json.Serialization;

namespace CareerConnect.Application.Billing.Responses;

public sealed class VnpayIpnResponseDto
{
    [JsonPropertyName("RspCode")]
    public string RspCode { get; set; } = string.Empty;

    [JsonPropertyName("Message")]
    public string Message { get; set; } = string.Empty;
}
