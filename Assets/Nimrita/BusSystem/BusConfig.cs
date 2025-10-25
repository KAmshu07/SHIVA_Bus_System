public class BusConfig
{
    public bool EnableAsyncDispatch { get; set; }
    public bool EnablePriority { get; set; }
    public bool EnableAdvancedLogging { get; set; }
    public BusScope Scope { get; set; } = BusScope.Global; // Default to global
    public PropagationBehavior DefaultPropagation { get; set; } = PropagationBehavior.Local;
    public bool ThrowOnUnhandledMessages { get; set; }
}
