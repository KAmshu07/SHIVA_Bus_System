using System;

public class EventBus : BaseBus<IEvent>
{
    public EventBus(BusConfig config, BaseBus<IEvent> parentBus = null)
        : base(config, $"EventBus-{config.Scope.Name}", parentBus) { }
}