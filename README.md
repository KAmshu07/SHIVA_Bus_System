# SHIVA Bus System

## Overview
SHIVA Bus System is a scoped event and message routing framework built for Unity projects that want reliable communication across gameplay systems without tight coupling. It provides request/response messaging, prioritized subscribers, scope-aware propagation, and permission controls so feature teams can collaborate safely within large solutions.

## Key Features
- Scoped message and event buses with configurable propagation (local, up, down, both).
- Request/response pattern with timeouts, async dispatch, and automatic cleanup.
- Builder APIs that let you toggle priority queues, async dispatch, and fail-fast handling.
- Assembly-level access permissions to guard sensitive scopes.
- Diagnostics helpers that expose live delivery/drop counts and handler presence.

## Getting Started
1. Register your assembly for the scopes you need:
   ```csharp
   BusManager.RegisterAccess(BusScope.Core, BusAccessLevel.ReadWrite);
   ```
2. Fetch a bus and subscribe:
   ```csharp
   var bus = BusManager.GetMessageBus(BusScope.Core);
   bus.Subscribe<PlayerSpawnedMessage>(OnPlayerSpawned);
   ```
3. Publish scoped messages or events:
   ```csharp
   bus.Publish(new PlayerSpawnedMessage(BusScope.Core));
   ```

Refer to `AGENTS.md` for contribution guidelines and advanced configuration tips.
