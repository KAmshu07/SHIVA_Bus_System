using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Reflection;
using UnityEngine;

public class BusRegistry
{
    private static readonly Lazy<BusRegistry> _instance = new Lazy<BusRegistry>(() => new BusRegistry());
    public static BusRegistry Instance => _instance.Value;

    private readonly Dictionary<BusScope, StandardMessageBus> _messageBuses = new Dictionary<BusScope, StandardMessageBus>();
    private readonly Dictionary<BusScope, EventBus> _eventBuses = new Dictionary<BusScope, EventBus>();
    private readonly Dictionary<(Assembly, BusScope), BusAccessLevel> _accessRights = new Dictionary<(Assembly, BusScope), BusAccessLevel>();

    private BusRegistry()
    {
        // Initialize the hierarchy
        InitializeScope(BusScope.Global);
        InitializeScope(BusScope.Core, BusScope.Global);
        InitializeScope(BusScope.Networking, BusScope.Core);
        InitializeScope(BusScope.UI, BusScope.Core);

        // Set up default access rights
        SetDefaultAccessRights();
    }

    private void InitializeScope(BusScope scope, BusScope parentScope = null)
    {
        var config = new BusConfig
        {
            EnableAsyncDispatch = true,
            EnablePriority = true,
            EnableAdvancedLogging = true,
            Scope = scope
        };

        StandardMessageBus parentMsgBus = null;
        EventBus parentEvtBus = null;

        if (parentScope != null)
        {
            _messageBuses.TryGetValue(parentScope, out parentMsgBus);
            _eventBuses.TryGetValue(parentScope, out parentEvtBus);
        }

        _messageBuses[scope] = new StandardMessageBus(config, parentMsgBus);
        _eventBuses[scope] = new EventBus(config, parentEvtBus);
    }

    private void SetDefaultAccessRights()
    {
        // Give global access to the executing assembly initially
        var currentAssembly = Assembly.GetExecutingAssembly();
        RegisterAccess(currentAssembly, BusScope.Global, BusAccessLevel.ReadWrite);
    }

    public void RegisterAccess(Assembly assembly, BusScope scope, BusAccessLevel level)
    {
        if (assembly == null) throw new ArgumentNullException(nameof(assembly));
        if (scope == null) throw new ArgumentNullException(nameof(scope));

        _accessRights[(assembly, scope)] = level;

        // Also register minimal access to parent scopes if needed
        var current = scope.Parent;
        while (current != null)
        {
            if (!_accessRights.ContainsKey((assembly, current)))
            {
                // Give read-only access to parent scopes by default
                _accessRights[(assembly, current)] = BusAccessLevel.ReadOnly;
            }
            current = current.Parent;
        }
    }

    public bool CanPublish(BusScope scope, Assembly assembly)
    {
        if (assembly == null || scope == null) return false;

        return _accessRights.TryGetValue((assembly, scope), out var access) &&
               (access == BusAccessLevel.WriteOnly || access == BusAccessLevel.ReadWrite);
    }

    public bool CanSubscribe(BusScope scope, Assembly assembly)
    {
        if (assembly == null || scope == null) return false;

        return _accessRights.TryGetValue((assembly, scope), out var access) &&
               (access == BusAccessLevel.ReadOnly || access == BusAccessLevel.ReadWrite);
    }

    public StandardMessageBus GetMessageBus(BusScope scope, Assembly requestingAssembly)
    {
        EnsureAccess(scope, requestingAssembly);
        if (_messageBuses.TryGetValue(scope, out var bus))
        {
            return bus;
        }

        throw new InvalidOperationException($"No message bus registered for scope {scope.Name}");
    }

    public EventBus GetEventBus(BusScope scope, Assembly requestingAssembly)
    {
        EnsureAccess(scope, requestingAssembly);
        if (_eventBuses.TryGetValue(scope, out var bus))
        {
            return bus;
        }

        throw new InvalidOperationException($"No event bus registered for scope {scope.Name}");
    }

    // Creates a new custom scope at runtime
    public BusScope CreateScope(string name, BusScope parent, Assembly requestingAssembly)
    {
        if (string.IsNullOrEmpty(name)) throw new ArgumentException("Scope name cannot be null or empty.", nameof(name));
        if (parent == null) throw new ArgumentNullException(nameof(parent));

        var newScope = parent.CreateChildScope(name);
        InitializeScope(newScope, parent);
        if (requestingAssembly != null)
        {
            RegisterAccess(requestingAssembly, newScope, BusAccessLevel.ReadWrite);
        }
        return newScope;
    }

    private void EnsureAccess(BusScope scope, Assembly assembly)
    {
        if (assembly == null) throw new ArgumentNullException(nameof(assembly));
        if (scope == null) throw new ArgumentNullException(nameof(scope));

        if (!_accessRights.TryGetValue((assembly, scope), out var access) || access == BusAccessLevel.None)
        {
            var assemblyName = assembly.GetName().Name;
            throw new UnauthorizedAccessException(
                $"Assembly {assemblyName} does not have access to bus scope {scope.Name}. " +
                $"Register access via BusManager.RegisterAccess().");
        }
    }

    public IReadOnlyDictionary<BusScope, BaseBus<IStandardMessage>.BusStatus> SnapshotMessageBuses()
    {
        var snapshot = new Dictionary<BusScope, BaseBus<IStandardMessage>.BusStatus>();
        foreach (var pair in _messageBuses)
        {
            snapshot[pair.Key] = pair.Value.GetStatusSnapshot();
        }
        return new ReadOnlyDictionary<BusScope, BaseBus<IStandardMessage>.BusStatus>(snapshot);
    }

    public IReadOnlyDictionary<BusScope, BaseBus<IEvent>.BusStatus> SnapshotEventBuses()
    {
        var snapshot = new Dictionary<BusScope, BaseBus<IEvent>.BusStatus>();
        foreach (var pair in _eventBuses)
        {
            snapshot[pair.Key] = pair.Value.GetStatusSnapshot();
        }
        return new ReadOnlyDictionary<BusScope, BaseBus<IEvent>.BusStatus>(snapshot);
    }
}
