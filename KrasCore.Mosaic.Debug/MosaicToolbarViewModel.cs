using System;
using System.Collections.Generic;
using BovineLabs.Anchor;
using BovineLabs.Anchor.Contracts;
using Unity.AppUI.UI;
using Unity.Collections;
using Unity.Entities;
using Unity.Properties;

namespace KrasCore.Mosaic.Debug
{
    public partial class MosaicToolbarViewModel : SystemObservableObject<MosaicToolbarViewModel.Data>, IInitializable, IDisposable
    {
        [CreateProperty]
        public UIArray<Data.IntGridName> IntGrids => Value.IntGrids;

        [CreateProperty]
        public IEnumerable<int> IntGridValues
        {
            get => Value.IntGridValues.Value.AsArray();
            set => SetProperty(Value.IntGridValues, value);
        }
        
        public void Initialize()
        {
            Value.Initialize();
        }

        public void Dispose()
        {
            Value.Dispose();
        }
        
        public void BindItem(DropdownItem item, int index)
        {
            item.label = Value.IntGrids[index].Name.ToString();
        }
        
        public partial struct Data
        {
            [SystemProperty]
            private ChangedList<int> intGridValues;

            [SystemProperty]
            private NativeList<IntGridName> intGrids;

            internal void Initialize()
            {
                intGridValues = new NativeList<int>(Allocator.Persistent);
                intGrids = new NativeList<IntGridName>(Allocator.Persistent);
            }

            internal void Dispose()
            {
                intGridValues.Value.Dispose();
                intGrids.Dispose();
            }
            
            public struct IntGridName : IComparable<IntGridName>, IEquatable<IntGridName>
            {
                public Hash128 IntGridHash;
                public FixedString128Bytes Name;

                public int CompareTo(IntGridName other)
                {
                    return Name.CompareTo(other.Name);
                }

                public bool Equals(IntGridName other)
                {
                    return IntGridHash.Equals(other.IntGridHash) && Name.Equals(other.Name);
                }

                public override int GetHashCode()
                {
                    unchecked
                    {
                        return (IntGridHash.GetHashCode() * 397) ^ Name.GetHashCode();
                    }
                }
            }
        }
    }
}