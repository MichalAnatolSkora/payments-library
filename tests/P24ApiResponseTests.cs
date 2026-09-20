using System.Text.Json;
using Payment.Core.P24.Models;

namespace Payment.Tests;

/// <summary>
/// P24 sends <c>error</c> in three shapes: a string, an array of per-item objects (refund 409),
/// or nothing. The parser must render each into a message without throwing.
/// </summary>
public sealed class P24ApiResponseTests
{
    private static P24ApiResponse<JsonElement> Parse(string json) =>
        JsonSerializer.Deserialize<P24ApiResponse<JsonElement>>(json)!;

    [Fact]
    public void NoErrorField_HasNoError()
    {
        var r = Parse("""{"data":{"x":1},"responseCode":0}""");

        Assert.False(r.HasError);
        Assert.Null(r.ErrorMessage);
    }

    [Fact]
    public void ExplicitNullError_HasNoError()
    {
        var r = Parse("""{"error":null,"responseCode":0}""");

        Assert.False(r.HasError);
        Assert.Null(r.ErrorMessage);
    }

    [Fact]
    public void StringError_ReturnedVerbatim()
    {
        var r = Parse("""{"error":"Incorrect authentication","code":401}""");

        Assert.True(r.HasError);
        Assert.Equal("Incorrect authentication", r.ErrorMessage);
        Assert.Equal(401, r.ErrorCode);
    }

    [Fact]
    public void ArrayError_JoinsItemMessagesWithOrderId()
    {
        var r = Parse("""{"error":[{"orderId":1,"status":false,"message":"Transaction not found"},{"orderId":2,"status":false,"message":"Insufficient funds available"}],"code":409}""");

        Assert.True(r.HasError);
        Assert.Equal("orderId 1: Transaction not found; orderId 2: Insufficient funds available", r.ErrorMessage);
    }

    [Fact]
    public void ArrayError_ItemWithoutMessage_FallsBackToRawJson()
    {
        var r = Parse("""{"error":[{"orderId":1,"status":false}],"code":409}""");

        Assert.Equal("""{"orderId":1,"status":false}""", r.ErrorMessage);
    }

    [Fact]
    public void ObjectError_FallsBackToRawJson()
    {
        var r = Parse("""{"error":{"field":"amount"},"code":400}""");

        Assert.True(r.HasError);
        Assert.Equal("""{"field":"amount"}""", r.ErrorMessage);
    }
}
