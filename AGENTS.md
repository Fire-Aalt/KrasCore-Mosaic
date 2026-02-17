# Repository Guidelines

## Project Structure & Module Organization
This repository is a Unity UPM package (`com.firealt.krascore-mosaic`) focused on ECS tilemap runtime and tooling.

- `KrasCore.Mosaic/`: runtime systems and system groups.
- `KrasCore.Mosaic.Data/`: shared data structs, components, commands, and utilities.
- `KrasCore.Mosaic.Authoring/`: authoring components, bakers, and ScriptableObjects.
- `KrasCore.Mosaic.Editor/`: custom inspectors, UI Toolkit views, and editor resources.
- `KrasCore.Mosaic.Debug/`: optional runtime debugging integration.
- `Shaders/`: terrain shader assets and HLSL includes.
- `Documentation~/`: package documentation images.

Keep `.asmdef` boundaries intact and place new code in the closest existing module.

## Build, Test, and Development Commands
Run commands from a host Unity project that references this package.

- Compile check (batch mode):
  `"<UnityEditorPath>/Unity.exe" -batchmode -projectPath "<HostProject>" -quit -logFile Logs/mosaic-compile.log`
- EditMode tests:
  `"<UnityEditorPath>/Unity.exe" -batchmode -projectPath "<HostProject>" -runTests -testPlatform EditMode -testResults Logs/editmode-results.xml -quit`
- PlayMode tests:
  `"<UnityEditorPath>/Unity.exe" -batchmode -projectPath "<HostProject>" -runTests -testPlatform PlayMode -testResults Logs/playmode-results.xml -quit`

## Coding Style & Naming Conventions
- C#: 4-space indentation, braces on new lines, one public type per file.
- Naming: `PascalCase` for types/methods/properties, `camelCase` for locals/parameters, `_camelCase` for private fields.
- Use `var` for variable declarations whenever possible; use explicit types only where `var` is not possible.
- Match existing namespace layout (`KrasCore.Mosaic.*`) and keep filenames aligned with primary type names.
- Unity assets must keep paired `.meta` files in commits.

## Testing Guidelines
No dedicated test assemblies are currently included in this package. For new tests, use Unity Test Framework and create `Tests/Editor` or `Tests/Runtime` assemblies with clear names like `RuleEngineSystemTests`.

Cover rule evaluation, mesh generation paths, and authoring-to-entity baking behavior when changing runtime logic.

## Commit & Pull Request Guidelines
Recent history favors short, imperative commit subjects (for example: `Fix unset graphics buffer issue`, `Update README.md`). Use the same style and keep subject lines focused on one change.

For PRs, include:
- concise summary and motivation,
- linked issue (if any),
- Unity version/package impact,
- screenshots or GIFs for editor UI changes,
- test evidence (batch compile/test output).
