# Mosaic
Mosaic is a Next Gen Runtime Unity Tilemap solution, heavily inspired by LDtk, built using Entity Component System stack and Odin Inspector 

![Mosaic](Documentation~/Images/Mosaic.png)

| Feature       | Unity.Tilemap                                                                 | Mosaic                                                                                                                                                      |
|---------------|-------------------------------------------------------------------------------|-------------------------------------------------------------------------------------------------------------------------------------------------------------|
| Rule engine   | RuleTile: very shallow support, requires custom code to achieve basic results | IntGrid: inspired by LDtk, one of the most powerful and feature rich rule engines                                                                           |
| GUI           | Poor GUI experience with RuleTile custom editor                               | Custom GUI made with [Odin Inspector](https://assetstore.unity.com/packages/tools/utilities/odin-inspector-and-serializer-89041?srsltid=AfmBOop97OyTTYiuIIGN0oQkSMLd0P3xSmw8NEuDFQQLFEFcz3blWS6p), with a separate EditorWindow to make GUI even more clear and concise. Custom rule pattern matrix controls and rendering |
| Performance   | Main thread only, really inefficient when using complex rule patterns         | 99% 'bursted' and 'jobified'. Main thread only submits meshes to render                                                                                     | 
| Allocations   | Huge GC spikes when using RuleTile                                            | 0 GC and Native allocations                                                                                                                               |
| Random        | No option to set a seed                                                       | `SetGlobalSeed()` and 100% deterministic                                                                                                                    |
| World editing | Tilemap saves changes in the editor                                           | IntGridAuthoring does not save editor data for now. However, adding such feature is trivial as all the data is stored in a `int[]`                       |
| Grid types    | Rectangular, hexagonal and isometric                                          | Only rectangular                                                                                                                                            |
| Object rule result  | Instantiates GameObjects, which is really expensive. A lot of GC allocations | Instantiates Entities, which is really cheap. No GC allocations |                  
| Rendering Pipeline | Internal `SpriteRenderer` based rendering path                                | `Entities.Graphics` based rendering with every `IntGridAuthoring` being a separate entity with a mesh. Utilizing `RuntimeMaterial` to create materials at runtime with different main textures as needed |
| 2D Rendering     | Supports both 3D and 2D rendering with SortingLayers   | Because `SpriteRenderer` rendering path is internal, Mosaic only works with 3D based rendering |

## Installation
Add these packages in this order using git urls in a package manager:
1. [KrasCore](https://github.com/Fire-Aalt/KrasCore): `https://github.com/Fire-Aalt/KrasCore.git`
2. KrasCore.Mosaic: `https://github.com/Fire-Aalt/KrasCore-Mosaic.git`

## Workflow
### Editor
To start, we need 2 things: `IntGrid` and `RuleGroup` ScriptableObjects

Create IntGrid using "Create/Mosaic/IntGrid". This is how we can configure it:

![IntGrid](Documentation~/Images/IntGrid.png)
*You can add a texture to be displayed instead of a color. Use create RuleGroup button to quickly create RuleGroup ScriptableObject*

Open RuleGroup ScriptableObject and add some rules to it like this:
![RuleGroup](Documentation~/Images/RuleGroup.png)
*Every parameter has a tooltip*

To edit the rule pattern, click on the matrix of the rule matrix preview of the rule. This window will pop up:
![Rule1](Documentation~/Images/Rule1.png)
Here you can modify rule matrix pattern and add or remove results. All the results are weighted, where more weight means more chance to be selected. You can have both sprite and entity to be rendered/spawned.

Next add `GridAuthoring` component to a GameObject in a SubScene and add a `TilemapAuthoring` as a child to Grid. Configure them as needed

### Code
Reference to an `IntGrid` or it's IntGridHash is required to send commands to `TilemapCommandBufferSystem`. 

1. Get a reference to `TilemapCommandBufferSingleton`
```csharp
var singleton = SystemAPI.GetSingleton<TilemapCommandBufferSingleton>();

// You can also set global seed here or do it later
singleton.SetGlobalSeed(seed);
```

2. Use `SetIntGridValue()` to update a referenced `IntGrid`
```csharp
// If you set 0 as IntGridValue you "remove" the position (the same as setting null value using SetTile in Unity.Tilemap)
_tcb.SetIntGridValue(topWalls, new int2(0, 1), topWallsSolidIntGridValue);
```

3. Use `Clear()` to clear all IntGridValues of a specific `IntGrid` or use `ClearAll()` to clear all `IntGrid`s values
```csharp
_tcb.Clear(topWalls);
_tcb.ClearAll();
```

*Done!*

## How do rules work
A matrix represents IntGridValues to search for with an offset from the center of every single position in the world. 
Controls and what they do are as follows:
1. Left click or "solid" color means that this cell must contain this exact IntGridValue
2. Right click or "canceled" color means that this cell can be anything but not this IntGridValue
3. Double right click removes the cell from cells to search (any IntGridValue is valid)
4. Any Value/No Value do the same as any other IntGridValues, but apply as a yes or no filter to the cell's IntGridValue (if IntGridValue = 1, and the cell is marked No Value, then the rule will not pass)

## Contribution
If you are interested in using this solution, I will be greatly appreciated. Write any bugs, feature requests or enhancements to Issues tab

### Special Thanks to:

[LDtk](https://ldtk.io/) for the idea and GUI

[NZCore](https://github.com/enzi/NZCore) for `ParallelList`

[BovineLabs](https://gitlab.com/tertle/com.bovinelabs.core/-/tree/master) for `GetSingleton<>()`
