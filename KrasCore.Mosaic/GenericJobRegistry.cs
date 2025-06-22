using KrasCore.Mosaic.Data;
using KrasCore.NZCore;
using Unity.Jobs;

[assembly: RegisterGenericJobType(typeof(ParallelList<RuleCommand>.UnsafeParallelListToArraySingleThreaded))]
[assembly: RegisterGenericJobType(typeof(ParallelList<EntityCommand>.UnsafeParallelListToArraySingleThreaded))]
[assembly: RegisterGenericJobType(typeof(ParallelList<SpriteCommand>.UnsafeParallelListToArraySingleThreaded))]
[assembly: RegisterGenericJobType(typeof(ParallelList<RemoveCommand>.UnsafeParallelListToArraySingleThreaded))]
[assembly: RegisterGenericJobType(typeof(ParallelList<SetCommand>.UnsafeParallelListToArraySingleThreaded))]