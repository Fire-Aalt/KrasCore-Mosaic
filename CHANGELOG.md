# Changelog
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