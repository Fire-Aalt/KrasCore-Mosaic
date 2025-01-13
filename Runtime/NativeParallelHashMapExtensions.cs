using System;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;

namespace KrasCore.Mosaic
{
    public static class NativeParallelHashMapExtensions
    {
        public static void ToNativeLists<TKey, TValue>(this NativeParallelHashMap<TKey, TValue> map,
            ref NativeList<TKey> keyList, ref NativeList<TValue> valueList) 
            where TKey : unmanaged, IEquatable<TKey>
            where TValue : unmanaged
        {
            map.m_HashMapData.ConvertToList(ref keyList, ref valueList);
        }
        
        private static unsafe void ConvertToList<TKey, TValue>(this UnsafeParallelHashMap<TKey, TValue> data, 
            ref NativeList<TKey> keyList, ref NativeList<TValue> valueList)
            where TKey : unmanaged, IEquatable<TKey>
            where TValue : unmanaged
        {
            var dataCount = data.Count();
            
            keyList.Clear();
            valueList.Clear();
            if (keyList.Capacity < dataCount)
            {
                keyList.Capacity = dataCount;
                valueList.Capacity = dataCount;
            }
            GetKeyValueLists(data.m_Buffer, dataCount, ref keyList, ref valueList);
        }
        
        [GenerateTestsForBurstCompatibility(GenericTypeArguments = new [] { typeof(int), typeof(int) })]
        private static unsafe void GetKeyValueLists<TKey, TValue>(UnsafeParallelHashMapData* data, int dataCount,
            ref NativeList<TKey> keyList, ref NativeList<TValue> valueList)
            where TKey : unmanaged
            where TValue : unmanaged
        {
            var bucketArray = (int*)data->buckets;
            var bucketNext = (int*)data->next;

            for (int i = 0, count = 0, max = dataCount, capacityMask = data->bucketCapacityMask
                 ; i <= capacityMask && count < max
                 ; ++i
                )
            {
                int bucket = bucketArray[i];

                while (bucket != -1)
                {
                    keyList.Add(UnsafeUtility.ReadArrayElement<TKey>(data->keys, bucket));
                    valueList.Add(UnsafeUtility.ReadArrayElement<TValue>(data->values, bucket));
                    count++;
                    bucket = bucketNext[bucket];
                }
            }
        }
    }
}