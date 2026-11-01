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

    /// <summary>
    /// Numeric error code. The report endpoints type it as a string, so it is read leniently:
    /// a value that is not an integer leaves it null and callers fall back to the HTTP status.
    /// </summary>
    [JsonPropertyName("code")]
    [JsonConverter(typeof(LenientIntegerConverter<int>))]
    public int? ErrorCode { get; init; }

    /// <summary>
    /// Field-level reasons the report endpoints attach to a 400, e.g. <c>[{"type":["Provided type is unknown"]}]</c>.
    /// </summary>
    [JsonPropertyName("validation")]
    public JsonElement? Validation { get; init; }

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
    /// Human-readable error text, with the <c>validation</c> details appended when present,
    /// or null when the response carries no error.
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

            var message = error.ValueKind switch
            {
                JsonValueKind.String => error.GetString(),
                JsonValueKind.Array => string.Join("; ", error.EnumerateArray().Select(DescribeItem)),
                JsonValueKind.Null or JsonValueKind.Undefined => null,
                _ => error.GetRawText(),
            };

            var details = DescribeValidation();
            return message is null || details is null ? message : $"{message}: {details}";
        }
    }

    private string? DescribeValidation()
    {
        if (Validation is not { ValueKind: JsonValueKind.Array } validation)
        {
            return null;
        }

        var fields = validation.EnumerateArray()
            .Where(item => item.ValueKind == JsonValueKind.Object)
            .SelectMany(item => item.EnumerateObject())
            .Select(field => $"{field.Name}: {DescribeValues(field.Value)}")
            .ToList();

        return fields.Count == 0 ? null : string.Join("; ", fields);
    }

    private static string DescribeValues(JsonElement value) => value.ValueKind switch
    {
        JsonValueKind.Array => string.Join(", ", value.EnumerateArray().Select(DescribeValues)),
        JsonValueKind.String => value.GetString() ?? string.Empty,
        _ => value.GetRawText(),
    };

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
