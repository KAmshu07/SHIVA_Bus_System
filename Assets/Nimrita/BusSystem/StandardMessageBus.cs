using System;

public class StandardMessageBus : BaseBus<IStandardMessage>
{
    public StandardMessageBus(BusConfig config, BaseBus<IStandardMessage> parentBus = null)
        : base(config, $"MessageBus-{config.Scope.Name}", parentBus) { }
}