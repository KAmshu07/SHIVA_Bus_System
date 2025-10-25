# SHIVA Bus System

> A guided journey from everyday Unity communication patterns to a fully-fledged, scoped messaging backbone. Follow the path step by step—each section builds on the last, solving the problems uncovered along the way.

---

## Contents
1. [How We Communicate Today](#how-we-communicate-today)
2. [Why This Approach Hurts at Scale](#why-this-approach-hurts-at-scale)
3. [Events & Delegates 101](#events--delegates-101)
4. [Where Raw Events Fall Short](#where-raw-events-fall-short)
5. [Introducing the SHIVA Event Bus](#introducing-the-shiva-event-bus)
6. [What the Event Bus Fixed—and What It Didn’t](#what-the-event-bus-fixedand-what-it-didnt)
7. [Scoped Event Bus: Organising the City](#scoped-event-bus-organising-the-city)
8. [When Broadcast Isn’t Enough](#when-broadcast-isnt-enough)
9. [Message Bus: Two-Way Conversations](#message-bus-two-way-conversations)
10. [Scoped Messages: Events on Steroids](#scoped-messages-events-on-steroids)
11. [Power-Ups: Access, Diagnostics, Builders](#power-ups-access-diagnostics-builders)
12. [Project Layout](#project-layout)
13. [Requirements & Setup](#requirements--setup)
14. [Roadmap](#roadmap)
15. [Contributing](#contributing)
16. [License](#license)

---

## How We Communicate Today
The typical Unity project starts with direct references wired through the Inspector or discovered at runtime:

```csharp
public class PlayerStats : MonoBehaviour
{
    [SerializeField] private HealthHud hud;

    public void ApplyDamage(int amount)
    {
        health -= amount;
        hud.UpdateBar(health);
    }
}
```

It works well when there is one player, one HUD, and a single scene. The moment you add multiplayer, dynamic UI loading, or automated tests, the coupling becomes a liability—every script needs everyone else’s references.

---

## Why This Approach Hurts at Scale
- **Scene churn** – renaming a prefab or rearranging the hierarchy breaks references silently.
- **Hidden dependencies** – code outside the script can’t see who calls what; debugging becomes guesswork.
- **Poor testability** – you must stand up entire object graphs just to exercise one method.
- **One-off hacks multiply** – you add static singletons or `FindObjectOfType` calls just to get things done.

We need something more resilient but equally straightforward to use.

---

## Events & Delegates 101
Before jumping to SHIVA, let’s revisit the traditional alternative: C# events and delegates (including UnityEvents). They improve decoupling because senders don’t store concrete references to all listeners.

### Plain C# delegate
```csharp
public class PlayerStats : MonoBehaviour
{
    public event Action<int> HealthChanged;

    public void ApplyDamage(int amount)
    {
        health = Mathf.Max(0, health - amount);
        HealthChanged?.Invoke(health);
    }
}
```

```csharp
public class HealthHud : MonoBehaviour
{
    [SerializeField] private PlayerStats player;

    void OnEnable()  => player.HealthChanged += UpdateHud;
    void OnDisable() => player.HealthChanged -= UpdateHud;

    private void UpdateHud(int newHealth) => Debug.Log($"HUD: {newHealth} HP");
}
```

### UnityEvents for designer wiring
```csharp
[Serializable]
public class IntUnityEvent : UnityEvent<int> { }

public class PlayerStats : MonoBehaviour
{
    public IntUnityEvent OnHealthChanged;

    public void ApplyDamage(int amount)
    {
        health -= amount;
        OnHealthChanged.Invoke(health);
    }
}
```
Designers can hook listeners via the Inspector without touching code.

**Pros**: No direct references from sender to listener, natural to write, and widely understood.  
**Cons**: You still manage lifetimes, order, and discoverability manually—and it’s still a one-off pattern per script.

---

## Where Raw Events Fall Short
- **Lifetime management** – forgetting to unsubscribe causes memory leaks or phantom callbacks.
- **Ordering and priority** – hard to guarantee which listener fires first without custom logic.
- **Scope explosion** – cross-system messaging either requires global static events or complicated piping.
- **Visibility** – who is listening to a given event? You only know by scanning the codebase.
- **Response handling** – C# events are fire-and-forget; implementing request/response flows takes additional coordination.

In short, events are powerful but ad hoc. We need a structured bus that retains the strengths while removing the foot-guns.

---

## Introducing the SHIVA Event Bus
The SHIVA Event Bus keeps the familiar “publish/broadcast” shape but centralises the mechanics. You publish an event; any subscribers anywhere in the project receive it. No direct references, no custom event fields per class.

### Define an event
```csharp
using Nimrita.BusSystem;

public sealed class PlayerSpawned : BaseEvent
{
    public Vector3 Position { get; }

    public PlayerSpawned(Vector3 position)
    {
        Position = position;
    }
}
```

### Subscribe
```csharp
public class SpawnLogger : MonoBehaviour
{
    void OnEnable()
    {
        BusManager.EventBus.Subscribe<PlayerSpawned>(OnPlayerSpawned);
    }

    void OnDisable()
    {
        BusManager.EventBus.Unsubscribe<PlayerSpawned>(OnPlayerSpawned);
    }

    private void OnPlayerSpawned(PlayerSpawned evt)
    {
        Debug.Log($"Spawned at {evt.Position} @ {evt.Timestamp:O}");
    }
}
```

### Publish
```csharp
public class Spawner : MonoBehaviour
{
    public void Spawn(Vector3 position)
    {
        Instantiate(prefab, position, Quaternion.identity);
        BusManager.EventBus.Publish(new PlayerSpawned(position));
    }
}
```

### Optional extras out of the box
The default configuration already enables a few helpful behaviours:
- **Priorities** — higher numbers run first so critical listeners can react before general observers.
- **Async dispatch** — queue work onto the Unity main thread without blocking the caller when you enable it via the builder.
- **Advanced logging** — surface subscribe/publish traces when you need visibility.

```csharp
// Ensure analytics records the spawn before less critical listeners.
BusManager.EventBus.Subscribe<PlayerSpawned>(TrackSpawnAnalytics, priority: 100);

// Spin up a specialised bus for UI events that logs verbosely and dispatches asynchronously.
var uiEventBus = EventBusBuilder.Create()
    .WithScope(BusScope.UI)
    .EnableAsyncDispatch()
    .WithAdvancedLogging()
    .Build();

// Subscribe with a default priority on the specialised bus.
uiEventBus.Subscribe<PlayerSpawned>(evt => Debug.Log($"UI reacting to {evt.Position}"));
```

### Event Bus feature showcase

#### Prioritised listeners
```csharp
BusManager.EventBus.Subscribe<PlayerSpawned>(evt => Debug.Log("General log"), priority: 0);
BusManager.EventBus.Subscribe<PlayerSpawned>(evt => Debug.Log("Critical metrics"), priority: 50);

BusManager.EventBus.Publish(new PlayerSpawned(Vector3.zero));
// Output order:
// Critical metrics
// General log
```
Higher priorities run first—ideal for analytics, cheats, or security hooks that must process events before anything else.

#### Async dispatch
```csharp
var asyncBus = EventBusBuilder.Create()
    .WithScope(BusScope.Networking)
    .EnableAsyncDispatch()
    .Build();

asyncBus.Subscribe<PlayerSpawned>(evt =>
{
    Debug.Log($"Async listener ran at frame {Time.frameCount}");
});

asyncBus.Publish(new PlayerSpawned(Vector3.one));
```
`EnableAsyncDispatch()` queues execution through `UnityMainThreadDispatcher`, letting gameplay code continue immediately while the event processes on the next frame.

#### Advanced logging & warnings
```csharp
var loggedBus = EventBusBuilder.Create()
    .WithScope(BusScope.Core)
    .WithAdvancedLogging()
    .Build();

loggedBus.Subscribe<PlayerSpawned>(_ => throw new Exception("Demo failure"));
loggedBus.Publish(new PlayerSpawned(Vector3.up));
```
With advanced logging enabled you get detailed console output showing subscription lifecycles, dispatch order, and any thrown exceptions—vital when debugging misbehaving listeners.

**What’s better already?**
- Central place for all broadcasts.
- Weak references mean destroyed listeners drop away automatically.
- Optional priority, async dispatch, and advanced logging without ad hoc plumbing.

---

## What the Event Bus Fixed—and What It Didn’t
We now have a consistent, maintainable way to broadcast events. However, we still face challenges:

- **All events share one global channel** – UI, gameplay, and networking chatter mingle.
- **No formal boundary control** – any assembly can subscribe or publish, intentionally or not.
- **Still one-way** – there’s no built-in notion of requesting data and awaiting a reply.
- **Observability** – better than ad hoc events, but we can go further.

To tackle the first two issues we add structure with scopes.

---

## Scoped Event Bus: Organising the City
Imagine your project as a city. The **Global** scope is the capital; districts like **Core**, **UI**, and **Networking** handle their own business. Scoped event buses let messages stay in their neighbourhoods unless told otherwise.

### Built-in scopes
`BusScope.Global`, `BusScope.Core`, `BusScope.Networking`, `BusScope.UI` ship ready to use. You can create more:
```csharp
var inventoryScope = BusManager.CreateScope("Inventory", BusScope.Core);
BusManager.RegisterAccess(inventoryScope, BusAccessLevel.ReadWrite);
```

### Scoped event definition
```csharp
public sealed class InventoryOpened : ScopedEvent
{
    public InventoryOpened(BusScope scope)
        : base(scope, PropagationBehavior.Local) { }
}
```

### Scoped subscribing and publishing
```csharp
var inventoryBus = BusManager.GetEventBus(inventoryScope);

inventoryBus.Subscribe<InventoryOpened>(_ => Debug.Log("Show inventory modal"));
inventoryBus.Publish(new InventoryOpened(inventoryScope));
```

### Propagation options
- `Local` – handle only within the current scope.
- `UpToParent` – bubble to the parent scope.
- `DownToChildren` – cascade to child scopes.
- `UpAndDown` – broadcast to both parent and children.

With scopes you segment traffic, manage feature ownership, and keep propagation explicit. Still, events remain one-way communications.

---

## When Broadcast Isn’t Enough
Real games often need acknowledgements or data transfers:
- “Give me the player’s loadout.”
- “Confirm the purchase succeeded.”
- “Return the pathfinding result.”

Events can hack this via callbacks, but it gets messy. We need two-way messaging with built-in correlation, timeouts, and error handling.

---

## Message Bus: Two-Way Conversations
Enter the **Standard Message Bus**. Messages can be commands or queries. Some are fire-and-forget (like a simple event); others expect responses and behave like request/response RPCs.

### Define request & response
```csharp
public sealed class LoadoutRequest : BaseMessage
{
    public LoadoutRequest() : base(requiresResponse: true) { }
}

public sealed class LoadoutResponse
{
    public string PrimaryWeapon { get; }
    public string SecondaryWeapon { get; }

    public LoadoutResponse(string primary, string secondary)
    {
        PrimaryWeapon = primary;
        SecondaryWeapon = secondary;
    }
}
```

### Subscriber responds
```csharp
BusManager.MessageBus.Subscribe<LoadoutRequest>(request =>
{
    var response = new LoadoutResponse("Rifle", "Sidearm");
    BusManager.MessageBus.Respond(request, response);
});
```

### Sender awaits response
```csharp
var result = await BusManager.MessageBus.Request<LoadoutRequest, LoadoutResponse>(
    new LoadoutRequest(),
    timeout: TimeSpan.FromSeconds(3));
```

The bus keeps an internal table of pending responses (thread-safe), matches answers by message ID, and throws informative exceptions if no handlers exist or the timeout expires. For fire-and-forget messages you can still call `Publish` just like we did with events.

### Message Bus feature showcase

#### Fire-and-forget vs request/response
```csharp
// Fire-and-forget (acts like an event message).
BusManager.MessageBus.Publish(new HealthChanged(90));

// Conversation with response.
var loadout = await BusManager.MessageBus.Request<LoadoutRequest, LoadoutResponse>(new LoadoutRequest());
```

#### Handling timeouts gracefully
```csharp
try
{
    await BusManager.MessageBus.Request<LoadoutRequest, LoadoutResponse>(
        new LoadoutRequest(),
        timeout: TimeSpan.FromMilliseconds(250));
}
catch (TimeoutException tex)
{
    Debug.LogWarning($"Fallback to defaults: {tex.Message}");
}
```
Timeouts automatically clear pending entries so later responses don’t leak.

#### Bubbling errors back to the caller
```csharp
BusManager.MessageBus.Subscribe<LoadoutRequest>(_ =>
{
    throw new InvalidOperationException("Inventory service offline");
});

try
{
    await BusManager.MessageBus.Request<LoadoutRequest, LoadoutResponse>(new LoadoutRequest());
}
catch (InvalidOperationException ex)
{
    Debug.LogError($"Request failed: {ex.Message}");
}
```
Exceptions thrown in handlers propagate through the awaiting task—no silent failures.

#### Fail fast when nobody listens
```csharp
var strictBus = MessageBusBuilder.Create()
    .WithScope(BusScope.Core)
    .FailOnUnhandledMessages()
    .Build();

try
{
    strictBus.Publish(new HealthChanged(75)); // Throws if no active subscribers
}
catch (InvalidOperationException ex)
{
    Debug.LogError(ex.Message);
}
```
Fail-fast behaviour helps catch misconfigured gameplay flows during development.

All builder options from the event bus—priority, async dispatch, advanced logging, and scope integration—apply equally to messages.

---

## Scoped Messages: Events on Steroids
Now combine scopes with the rich messaging API. Scoped messages inherit from `ScopedMessage`, gaining propagation controls and access validation in addition to request/response support.

```csharp
public sealed class InventorySyncRequest : ScopedMessage
{
    public InventorySyncRequest(BusScope scope)
        : base(scope, PropagationBehavior.UpToParent, requiresResponse: true) { }
}

public sealed class InventorySnapshot
{
    public IReadOnlyList<Item> Items { get; }
    public InventorySnapshot(IReadOnlyList<Item> items) => Items = items;
}
```

```csharp
var inventoryScope = BusManager.CreateScope("Inventory", BusScope.Core);
BusManager.RegisterAccess(inventoryScope, BusAccessLevel.ReadWrite);

var inventoryBus = BusManager.GetMessageBus(inventoryScope);

inventoryBus.Subscribe<InventorySyncRequest>(request =>
{
    var snapshot = BuildSnapshot();
    inventoryBus.Respond(request, new InventorySnapshot(snapshot));
});

var response = await inventoryBus.Request<InventorySyncRequest, InventorySnapshot>(
    new InventorySyncRequest(inventoryScope));
```

Scoped messages are the most capable payload type:
- All event capabilities (publish, propagation, priority, async dispatch).
- Plus request/response, timeouts, error propagation, and pending-response tracking.
- Plus access control enforcement per assembly.

---

## Power-Ups: Access, Diagnostics, Builders

### Access Control
You can restrict who may publish or subscribe per scope. Calls like `BusManager.RegisterAccess(scope, BusAccessLevel.ReadWrite)` store permissions keyed to the calling assembly. Attempting to fetch a bus or instantiate a scoped payload without permission throws immediately.

### Diagnostics
`BusDiagnostics.GetMessageBusStatus(scope)` (or its event twin) returns counts for published, delivered, dropped messages and pending responses, plus a breakdown of active subscribers by message type. Great for debugging, monitoring, or editor tooling.

### Builders
Need custom behaviour (async dispatch, priority queues, fail-fast on unhandled messages, parent/child relationships)? Use `MessageBusBuilder` or `EventBusBuilder`:
```csharp
var customBus = MessageBusBuilder.Create()
    .WithScope(inventoryScope)
    .EnableAsyncDispatch()
    .WithPrioritySupport()
    .WithAdvancedLogging()
    .FailOnUnhandledMessages()
    .WithParent(BusManager.GetMessageBus(BusScope.Core))
    .Build();
```

Behind the scenes all buses rely on `UnityMainThreadDispatcher` to funnel async work onto Unity’s main thread safely. The dispatcher manages its lifecycle automatically (no duplicate hidden objects, no work accepted after application quit).

---

## Project Layout
```
Assets/
  Nimrita/BusSystem/
    BaseBus.cs
    BusConfig.cs
    BusDiagnostics.cs
    BusManager.cs
    BusRegistry.cs
    BusScope.cs
    UnityMainThreadDispatcher.cs
    Builders/
    Events/
    Interfaces/
    Messages/
  Scenes/
Packages/
ProjectSettings/
README.md  (this guide)
AGENTS.md  (local-only, ignored)
```
Runtime implementation lives entirely inside `Assets/Nimrita/BusSystem`, making it easy to package or reuse.

---

## Requirements & Setup

| Requirement        | Details                                                     |
|--------------------|-------------------------------------------------------------|
| Unity Version      | 2021.3 LTS or newer (validated on 2021.3 and 2022.3)        |
| Scripting Runtime  | .NET Standard 2.1                                           |
| Platforms          | Any platform supported by your Unity edition                |
| Dependencies       | Uses only built-in Unity packages (URP samples included)    |

**Quick start**
1. Clone the repo and open it in Unity.  
2. Let Unity import assets the first time.  
3. Play the sample scene at `Assets/Scenes/SampleScene.unity` to watch event/message traffic.  
4. Explore `Assets/Nimrita/BusSystem` to integrate or customise the framework.

### Run Tests
- Edit Mode: `unity-editor -projectPath "$(pwd)" -quit -batchmode -logFile Logs/editmode.log -runTests -testPlatform EditMode`
- Play Mode: `unity-editor -projectPath "$(pwd)" -quit -batchmode -logFile Logs/playmode.log -runTests -testPlatform PlayMode`

Logs should be warning-free after each run.

### Branching Workflow (main/dev)
- Long-lived branches: `main` and `dev` only.
- Do all work on `dev`; never commit directly to `main`.
- To update `main` from `dev`:
  - `git checkout dev`
  - Work → `git add -A && git commit -m "..." && git push`
  - `git checkout main`
  - Fast-forward from dev: `git merge --ff-only dev`
  - `git push origin main`
  - `git checkout dev` to continue

---

## Roadmap
- Package distribution (UPM/OpenUPM) for plug-and-play adoption.
- Editor diagnostics window visualising scopes, listeners, and live traffic.
- Expanded sample scenes covering gameplay, UI, and network scenarios.
- Automated test suite for async dispatch, propagation, and access enforcement.
- Optional telemetry exporters for production monitoring.

Have an idea? Open an issue and let’s shape the roadmap together.

---

## Contributing
We welcome bug reports, feature ideas, and pull requests.

1. Fork the repository and create a feature branch (`git checkout -b feature/new-idea`).  
2. Follow the existing code style: four-space indentation, PascalCase types.  
3. Update or add tests/samples when behaviour changes.  
4. Commit with clear messages and push your branch.  
5. Open a pull request describing the change, validation steps, and any supporting assets (screenshots, logs, recordings).

---

## License
SHIVA Bus System is released under the MIT License. You may use, modify, and distribute it in commercial or personal projects provided you retain the license notice. See `LICENSE` for the full text.
