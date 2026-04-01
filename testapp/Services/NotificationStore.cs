using Payment.Models.Requests;

namespace PaymentsLibrary.TestApp.Services;

public record ReceivedNotification(
    DateTime ReceivedAt,
    string SessionId,
    int OrderId,
    int Amount,
    string Currency,
    int MethodId,
    string Sign,
    bool Valid);

public class NotificationStore
{
    private readonly List<ReceivedNotification> _items = [];
    private readonly Lock _lock = new();

    public void Add(InstantPaymentNotificationRequest payload, bool valid)
    {
        var entry = new ReceivedNotification(
            ReceivedAt: DateTime.UtcNow,
            SessionId: payload.SessionId,
            OrderId: payload.OrderId,
            Amount: payload.Amount,
            Currency: payload.Currency,
            MethodId: payload.MethodId,
            Sign: payload.Sign,
            Valid: valid);

        lock (_lock)
        {
            _items.Insert(0, entry);
            if (_items.Count > 50)
            {
                _items.RemoveAt(_items.Count - 1);
            }
        }
    }

    public IReadOnlyList<ReceivedNotification> GetAll()
    {
        lock (_lock)
        {
            return [.. _items];
        }
    }
}
