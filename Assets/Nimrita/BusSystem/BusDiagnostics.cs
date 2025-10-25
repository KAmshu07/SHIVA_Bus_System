using System.Collections.Generic;

public static class BusDiagnostics
{
    public static BaseBus<IStandardMessage>.BusStatus GetMessageBusStatus(BusScope scope)
    {
        return BusManager.GetMessageBus(scope).GetStatusSnapshot();
    }

    public static BaseBus<IEvent>.BusStatus GetEventBusStatus(BusScope scope)
    {
        return BusManager.GetEventBus(scope).GetStatusSnapshot();
    }

    public static IReadOnlyDictionary<BusScope, BaseBus<IStandardMessage>.BusStatus> GetAllMessageBusStatuses()
    {
        return BusRegistry.Instance.SnapshotMessageBuses();
    }

    public static IReadOnlyDictionary<BusScope, BaseBus<IEvent>.BusStatus> GetAllEventBusStatuses()
    {
        return BusRegistry.Instance.SnapshotEventBuses();
    }
}
