public interface IScopedEvent : IEvent
{
    BusScope Scope { get; }
    PropagationBehavior Propagation { get; }
}