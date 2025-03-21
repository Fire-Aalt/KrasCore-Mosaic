using KrasCore.Mosaic;
using KrasCore.NZCore;
using Unity.Jobs;

[assembly: RegisterGenericJobType(typeof(ParallelList<RuleCommand>.UnsafeParallelListToArraySingleThreaded))]
[assembly: RegisterGenericJobType(typeof(ParallelList<EntityCommand>.UnsafeParallelListToArraySingleThreaded))]
[assembly: RegisterGenericJobType(typeof(ParallelList<SpriteCommand>.UnsafeParallelListToArraySingleThreaded))]
[assembly: RegisterGenericJobType(typeof(ParallelList<RemoveCommand>.UnsafeParallelListToArraySingleThreaded))]