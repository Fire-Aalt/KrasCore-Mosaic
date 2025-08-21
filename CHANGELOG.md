# Changelog
## [1.4.0] - 2025-08-21

### Breaking Changes
* `TilemapAuthoring` serialization layout has been changed. Rendering data has been reset to default

### Added
* Tilemap Terrain. A special terrain that merges multiple `IntGrid` layers into a single mesh using a custom TerrainShader and blending logic.
There is no limit to the number of layers used in the Terrain, the only limit is the amount of blended `IntGrid` layers,
which can be dynamically adjusted in the authoring script. This Terrain can greatly improve GPU performance in cases where several `IntGrid` layers overlap and share the same rendering data

### Changed
* Internal renaming
* Further optimize `IntGridMeshDataSystem` by removing global parallel execution. Separate layers are processed in parallel, but each layer essentially does only single threaded work
* Moved internal singletons into their own systems

## [1.3.0] - 2025-07-25

### Breaking Changes
* `IntGridMatrix` serialization layout has been changed to support Dual-Grid configuration. All `IntGridMatrix` in `RuleGroup`s have to be reconfigured

### Added
* Dual-Grid system support! For more info check jess::codes video: https://www.youtube.com/watch?v=jEWFSv3ivTg
* Optional Debug assembly with BovineLabs.Core, BobineLabs.Anchor and BovineLabs.Quill integration to visually inspect IntGrid values
* Custom editor drawers for `IntGridMatrix` and `IntGridValueSelector`

### Changed
* Overall code cleanup and simplification with better separation
* 2-3x rule matching performance improvement. Instead of 7 jobs per `IntGrid` + 1 const, now `TilemapRuleEngineSystem` sends only 3 jobs const
* Reduced number of jobs scheduled by `TilemapMeshDataSystem`
* Moved `TilemapRuleEngineSystem` and `TilemapMeshDataSystem` to the end of `PresentationSystemGroup` to not waste jobs space when gameplay logic is performed
* Removed singleton sync points in both `TilemapRuleEngineSystem` and `TilemapMeshDataSystem`
* Mosaic meshes are now updated on the next frame, just like entities instantiated/destroyed by `TilemapRuleEngineSystem`
* IntGrid value is now a struct `IntGridValue`, which is a wrapper for `short` instead of an `int`. This change would lead to 50% less memory required to store serialized IntGrid values
* Improve `TilemapEntityCleanupSystem` performance by using `EntityManager` batch API

### Fixed
* Setting `IntGrid` value to 0 will now remove the entry from the hash map, instead of just setting to 0

## [1.2.0] - 2025-06-22

### Added
* Separate assemblies for Main, Data, Authoring and Editor
* Bounds culling to tilemap layers

### Changed
* Tilemap layers are now rendered using Unity.Entities.Graphics
* ScriptableObjects are now editor only (used for baking)

## [1.1.0] - 2025-06-05

### Added
* `AsParallelWriter()` to TilemapCommandBufferSingleton
* TilemapInitializationSystemGroup and TilemapInitializationSystem

### Changed
* Marked singletons as internal
* TilemapData is now an IEnableableComponent

### Removed
* TilemapCommandBuffer, now merged into TilemapCommandBufferSingleton
* GridBakingSystem, now merged into TilemapInitializationSystem

### Fixed
* GridData changes at runtime now correctly affect tilemap layers