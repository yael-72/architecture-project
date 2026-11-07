using MongoDB.Driver;

namespace NotificationService.Data;

public class MongoNotificationContext
{
    public MongoNotificationContext(string connectionString, string databaseName)
    {
        var mongoUrl = new MongoUrl(connectionString);
        var client = new MongoClient(mongoUrl);
        var database = client.GetDatabase(databaseName);
        Notifications = database.GetCollection<NotificationDocument>("notifications");
    }

    public IMongoCollection<NotificationDocument> Notifications { get; }
}
