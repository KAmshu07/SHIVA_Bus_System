using System;
using System.Reflection;
using System.Runtime.CompilerServices;

public static class BusManager
{
    // For backward compatibility
    public static StandardMessageBus MessageBus => GetMessageBus(BusScope.Global);

    public static EventBus EventBus => GetEventBus(BusScope.Global);

    // New methods for the SHBI system
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static StandardMessageBus GetMessageBus(BusScope scope) =>
        BusRegistry.Instance.GetMessageBus(scope, Assembly.GetCallingAssembly());

    [MethodImpl(MethodImplOptions.NoInlining)]
    public static EventBus GetEventBus(BusScope scope) =>
        BusRegistry.Instance.GetEventBus(scope, Assembly.GetCallingAssembly());

    // Register assembly access
    public static void RegisterAccess(Assembly assembly, BusScope scope, BusAccessLevel level) =>
        BusRegistry.Instance.RegisterAccess(assembly, scope, level);

    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void RegisterAccess(BusScope scope, BusAccessLevel level) =>
        BusRegistry.Instance.RegisterAccess(Assembly.GetCallingAssembly(), scope, level);

    // Create a new custom scope
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static BusScope CreateScope(string name, BusScope parent) =>
        BusRegistry.Instance.CreateScope(name, parent, Assembly.GetCallingAssembly());
}
