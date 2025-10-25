public class EventBusBuilder
{
    private readonly BusConfig _config = new BusConfig();
    private BaseBus<IEvent> _parentBus;

    private EventBusBuilder() { }

    public static EventBusBuilder Create()
    {
        return new EventBusBuilder();
    }

    public EventBusBuilder WithPrioritySupport()
    {
        _config.EnablePriority = true;
        return this;
    }

    public EventBusBuilder EnableAsyncDispatch()
    {
        _config.EnableAsyncDispatch = true;
        return this;
    }

    public EventBusBuilder WithAdvancedLogging()
    {
        _config.EnableAdvancedLogging = true;
        return this;
    }

    public EventBusBuilder FailOnUnhandledMessages()
    {
        _config.ThrowOnUnhandledMessages = true;
        return this;
    }

    public EventBusBuilder WithScope(BusScope scope)
    {
        _config.Scope = scope;
        return this;
    }

    public EventBusBuilder WithParent(EventBus parent)
    {
        _parentBus = parent;
        return this;
    }

    public EventBus Build()
    {
        return new EventBus(_config, _parentBus);
    }
}
