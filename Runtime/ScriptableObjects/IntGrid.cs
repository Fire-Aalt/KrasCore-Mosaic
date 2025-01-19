using System;
using System.Collections.Generic;
using KrasCore.Editor;
using Sirenix.OdinInspector;
using UnityEditor;
using UnityEngine;
using Hash128 = Unity.Entities.Hash128;

namespace KrasCore.Mosaic
{
    [CreateAssetMenu(menuName = "Mosaic/IntGrid")]
    public class IntGrid : ScriptableObject
    {
        public List<IntGridValue> intGridValues = new();

        public readonly Dictionary<int, IntGridValue> IntGridValuesDict = new();

        //[ReadOnly]
        public List<RuleGroup> ruleGroups = new();
        
        [SerializeField, HideInInspector]
        private int _counter = 1;

        [HideInInspector]
        public Hash128 intGridHash;
        
#if UNITY_EDITOR
        [Button]
        private void CreateRuleGroup()
        {
            var instance = AssetDatabaseUtils.CreateNewScriptableObjectAsset<RuleGroup>(name + "Group", this);
            instance.intGrid = this;
            ruleGroups.Add(instance);
            EditorUtility.SetDirty(this);
            EditorUtility.SetDirty(instance);
        }

        private void OnEnable()
        {
            ValidateDict();
        }
        
        private void OnValidate()
        {
            intGridHash = AssetDatabaseUtils.ToGuidHash(this);
            foreach (var intGridValue in intGridValues)
            {
                if (intGridValue.value == -1)
                {
                    intGridValue.value = _counter;
                    _counter++;
                }
            }
            ValidateDict();
        }
        
        private void ValidateDict()
        {
            foreach (var intGridValue in intGridValues)
            {
                IntGridValuesDict[intGridValue.value] = intGridValue;
            }
        }
#endif
    }
    
    [Serializable]
    public class IntGridValue
    {
        [ReadOnly]
        public int value = -1;
        public string name;
        public Color color;
        public Texture texture;
    }
}