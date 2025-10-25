using System;
using System.Reflection;

public abstract class ScopedMessage : BaseMessage, IScopedMessage
{
    public BusScope Scope { get; }
    public PropagationBehavior Propagation { get; }

    protected ScopedMessage(
        BusScope scope,
        PropagationBehavior propagation = PropagationBehavior.Local,
        bool requiresResponse = false) : base(requiresResponse)
    {
        Scope = scope ?? throw new ArgumentNullException(nameof(scope));
        Propagation = propagation;

        // Runtime validation can be enabled here if desired
        // Uncomment when BusRegistry is implemented

        var declaringAssembly = GetType().Assembly;
        if (!BusRegistry.Instance.CanPublish(scope, declaringAssembly))
        {
            throw new UnauthorizedAccessException(
                $"Assembly {declaringAssembly.GetName().Name} does not have publish rights to scope {scope.Name}");
        }

    }
}
