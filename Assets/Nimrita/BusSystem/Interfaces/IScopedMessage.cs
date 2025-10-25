public interface IScopedMessage : IStandardMessage
{
    BusScope Scope { get; }
    PropagationBehavior Propagation { get; }
}