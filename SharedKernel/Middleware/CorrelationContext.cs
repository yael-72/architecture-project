namespace SharedKernel.Middleware;

/// <summary>
/// AsyncLocal storage for the current request/message correlation ID.
/// Shared across HTTP middleware, RabbitMQ consumers, and service code.
/// </summary>
public static class CorrelationContext
{
    private static readonly AsyncLocal<Guid> _current = new();

    public static Guid CorrelationId
    {
        get
        {
            if (_current.Value == Guid.Empty)
                _current.Value = Guid.NewGuid();
            return _current.Value;
        }
        set => _current.Value = value;
    }
}
