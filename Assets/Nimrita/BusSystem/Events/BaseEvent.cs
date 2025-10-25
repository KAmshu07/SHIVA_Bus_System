using System;

public abstract class BaseEvent : IEvent
{
    public DateTime Timestamp { get; }

    protected BaseEvent()
    {
        Timestamp = DateTime.UtcNow;
    }
}