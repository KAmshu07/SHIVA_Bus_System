# Repository Guidelines

## Project Structure & Module Organization
Runtime source lives under `Assets/Nimrita/BusSystem`, grouped by concern (`Builders`, `Events`, `Interfaces`, `Messages`). Scenes demonstrating bus workflows sit in `Assets/Scenes`; refresh scene references whenever bus contracts change. Configuration assets live in `Assets/Settings`, while `ProjectSettings/` and `Packages/` manage Unity editor and dependency metadata. Treat `Library/`, `Logs/`, `Temp/`, and `UserSettings/` as generated outputs—never commit manual edits there.

## Build, Test, and Development Commands
In-editor changes should be followed by a domain reload using the Unity Play button to validate behaviour. For unattended validation, run `unity-editor -projectPath "$(pwd)" -quit -batchmode -logFile Logs/editmode.log -runTests -testPlatform EditMode` to execute Edit Mode tests, and swap `-testPlatform PlayMode` for Play Mode coverage. Use `unity-editor -projectPath "$(pwd)" -quit -batchmode -executeMethod BuildBusDemo.PerformBuild` if you add or update an automated build entry point.

## Coding Style & Naming Conventions
Write C# with four-space indentation, braces on new lines for type declarations, and PascalCase for classes, interfaces, and public members (`EventBusBuilder`). Prefer camelCase for method parameters and locals, and `_camelCase` for private fields as seen in `BaseBus`. Leave descriptive XML documentation on public APIs that will be consumed by other teams. Run the built-in C# formatter before committing; if you introduce Roslyn analyzers or `.editorconfig`, keep the rules uniform across folders.

## Testing Guidelines
Use the Unity Test Framework for coverage. Place new Edit Mode tests in `Assets/Tests/EditMode` and Play Mode tests in `Assets/Tests/PlayMode`, mirroring the runtime namespace so discovery remains automatic. Test names should read `MethodName_Scenario_ExpectedOutcome`, and favour lightweight fakes over direct `MonoBehaviour` dependencies unless lifecycle validation is required. Ensure asynchronous bus flows hit both success and timeout paths; log files should remain warning-free after each test run.

## Commit & Pull Request Guidelines
Craft Git commits with a concise present-tense subject (`Bus: add priority queue for subscribers`) followed by a short body listing rationale and risks. Bundle related asset meta updates with the script change that introduced them to keep import GUIDs consistent. Pull requests need a clear description, reproduction or validation steps, references to tracked issues, and screenshots or recordings when scene behaviour changes. Request reviews from another Unity developer before merging to keep architecture decisions visible.
