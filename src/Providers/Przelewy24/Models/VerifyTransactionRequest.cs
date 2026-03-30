using System.Text.Json.Serialization;

namespace PaymentsLibrary.Providers.Przelewy24.Models;

internal sealed class VerifyTransactionRequest
{
    [JsonPropertyName("merchantId")] public int MerchantId { get; init; }
    [JsonPropertyName("posId")]      public int PosId { get; init; }
    [JsonPropertyName("sessionId")]  public required string SessionId { get; init; }
    [JsonPropertyName("amount")]     public int Amount { get; init; }
    [JsonPropertyName("currency")]   public required string Currency { get; init; }
    [JsonPropertyName("orderId")]    public int OrderId { get; init; }
    [JsonPropertyName("sign")]       public required string Sign { get; init; }
}
