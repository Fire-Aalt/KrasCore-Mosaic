using KrasCore.Mosaic.Data;
using KrasCore.NZCore;
using Unity.Jobs;

[assembly: RegisterGenericJobType(typeof(ParallelList<EntityCommand>.UnsafeParallelListToArraySingleThreaded))]