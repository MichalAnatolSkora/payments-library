using System.Text.Json.Serialization;

namespace Payment.Core.P24.Models;

internal sealed class P24ApiResponse<T>
{
    [JsonPropertyName("data")]
    public T? Data { get; init; }

    [JsonPropertyName("responseCode")]
    public int ResponseCode { get; init; }

    [JsonPropertyName("error")]
    public string? Error { get; init; }

    [JsonPropertyName("code")]
    public int? ErrorCode { get; init; }
}