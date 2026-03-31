using System.Text.Json.Serialization;

namespace PaymentsLibrary.Providers.Przelewy24.Models;

internal sealed class P24ApiResponse<T>
{
    [JsonPropertyName("data")]         public T? Data { get; init; }
    [JsonPropertyName("error")]        public string? Error { get; init; }
    [JsonPropertyName("errorMessage")] public string? ErrorMessage { get; init; }
}

internal sealed class RegisterTransactionData
{
    [JsonPropertyName("token")] public required string Token { get; init; }
}

internal sealed class TransactionStatusData
{
    [JsonPropertyName("sessionId")]     public required string SessionId { get; init; }
    [JsonPropertyName("orderId")]       public long OrderId { get; init; }
    [JsonPropertyName("amount")]        public int Amount { get; init; }
    [JsonPropertyName("currency")]      public required string Currency { get; init; }
    [JsonPropertyName("status")]        public int Status { get; init; }
    [JsonPropertyName("paymentMethod")] public int? MethodId { get; init; }
}
