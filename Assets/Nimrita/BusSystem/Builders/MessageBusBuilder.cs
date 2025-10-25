public class MessageBusBuilder
{
    private readonly BusConfig _config = new BusConfig();
    private BaseBus<IStandardMessage> _parentBus;

    private MessageBusBuilder() { }

    public static MessageBusBuilder Create()
    {
        return new MessageBusBuilder();
    }

    public MessageBusBuilder WithPrioritySupport()
    {
        _config.EnablePriority = true;
        return this;
    }

    public MessageBusBuilder EnableAsyncDispatch()
    {
        _config.EnableAsyncDispatch = true;
        return this;
    }

    public MessageBusBuilder WithAdvancedLogging()
    {
        _config.EnableAdvancedLogging = true;
        return this;
    }

    public MessageBusBuilder FailOnUnhandledMessages()
    {
        _config.ThrowOnUnhandledMessages = true;
        return this;
    }

    public MessageBusBuilder WithScope(BusScope scope)
    {
        _config.Scope = scope;
        return this;
    }

    public MessageBusBuilder WithParent(StandardMessageBus parent)
    {
        _parentBus = parent;
        return this;
    }

    public StandardMessageBus Build()
    {
        return new StandardMessageBus(_config, _parentBus);
    }
}
