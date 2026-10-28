using System.Text.Json;
using System.Text.Json.Serialization;

namespace Payment.Core.P24.Models;

internal sealed class P24ApiResponse<T>
{
    [JsonPropertyName("data")]
    public T? Data { get; init; }

    [JsonPropertyName("responseCode")]
    public int ResponseCode { get; init; }

    /// <summary>
    /// P24 returns <c>error</c> as a string for most failures, but as an array of
    /// per-item objects for refund conflicts (HTTP 409). Kept raw so both shapes parse.
    /// </summary>
    [JsonPropertyName("error")]
    public JsonElement? Error { get; init; }

    [JsonPropertyName("code")]
    public int? ErrorCode { get; init; }

    /// <summary>
    /// Next-page token returned by the paginated report endpoints.
    /// </summary>
    [JsonPropertyName("token")]
    public string? Token { get; init; }

    /// <summary>
    /// Paging counters returned by the report endpoints. The P24 spec documents it as an object
    /// for some endpoints and as a one-element array for others, so it is kept raw.
    /// </summary>
    [JsonPropertyName("pageInformation")]
    public JsonElement? PageInformation { get; init; }

    [JsonIgnore]
    public bool HasError => Error is { ValueKind: not (JsonValueKind.Null or JsonValueKind.Undefined) };

    /// <summary>
    /// Human-readable error text, or null when the response carries no error.
    /// </summary>
    [JsonIgnore]
    public string? ErrorMessage
    {
        get
        {
            if (Error is not { } error)
            {
                return null;
            }

            return error.ValueKind switch
            {
                JsonValueKind.String => error.GetString(),
                JsonValueKind.Array => string.Join("; ", error.EnumerateArray().Select(DescribeItem)),
                JsonValueKind.Null or JsonValueKind.Undefined => null,
                _ => error.GetRawText(),
            };
        }
    }

    private static string DescribeItem(JsonElement item)
    {
        if (item.ValueKind == JsonValueKind.Object
            && item.TryGetProperty("message", out var message)
            && message.ValueKind == JsonValueKind.String)
        {
            var prefix = item.TryGetProperty("orderId", out var orderId)
                ? $"orderId {orderId.GetRawText()}: "
                : string.Empty;
            return prefix + message.GetString();
        }

        return item.GetRawText();
    }
}
