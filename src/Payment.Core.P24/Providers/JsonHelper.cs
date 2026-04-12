using System.Net.Http.Json;
using System.Text.Json;

namespace Payment.Core.P24.Providers;

internal static class JsonHelper
{
    internal static async Task<T?> ReadJsonOrNull<T>(
        HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        var contentType = response.Content.Headers.ContentType?.MediaType ?? string.Empty;
        if (!contentType.Contains("json", StringComparison.OrdinalIgnoreCase))
        {
            return default;
        }

        try
        {
            return await response.Content.ReadFromJsonAsync<T>(cancellationToken);
        }
        catch (JsonException)
        {
            return default;
        }
    }
}
