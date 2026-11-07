using MongoDB.Driver;
using NotificationService.Data;

namespace NotificationService.Services;

public class NotificationStore
{
    private readonly MongoNotificationContext _mongoContext;

    public NotificationStore(MongoNotificationContext mongoContext)
    {
        _mongoContext = mongoContext;
    }

    public Task SaveAsync(NotificationDocument notification)
    {
        return _mongoContext.Notifications.InsertOneAsync(notification);
    }
}
