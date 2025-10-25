using System;

public abstract class BaseMessage : IStandardMessage
{
    public string MessageId { get; }
    public bool RequiresResponse { get; }

    protected BaseMessage(bool requiresResponse = false)
    {
        MessageId = Guid.NewGuid().ToString();
        RequiresResponse = requiresResponse;
    }
}