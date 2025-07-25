using System;
using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;
using Hash128 = Unity.Entities.Hash128;
using KrasCore.Editor;

namespace KrasCore.Mosaic.Authoring
{
    [CreateAssetMenu(menuName = "Mosaic/IntGrid")]
    public class IntGridDefinition : ScriptableObject
    {
        [field: SerializeField, HideInInspector] public Hash128 Hash { get; private set; }

        public bool useDualGrid;
        public List<IntGridValueDefinition> intGridValues = new();

        //[ReadOnly]
        public List<RuleGroup> ruleGroups = new();
        
        [SerializeField, HideInInspector]
        private short _counter = 1;

        public readonly Dictionary<int, IntGridValueDefinition> IntGridValuesDict = new();
        
        [Button]
        private void CreateRuleGroup()
        {
            var instance = AssetDatabaseUtils.CreateNewScriptableObjectAsset<RuleGroup>(name + "Group", this);
            instance.intGrid = this;
            ruleGroups.Add(instance);
        }

        private void OnEnable()
        {
            ValidateDict();
        }
        
        private void OnValidate()
        {
            Hash = AssetDatabaseUtils.ToGuidHash(this);
            foreach (var intGridValue in intGridValues)
            {
                if (intGridValue.value == -1)
                {
                    intGridValue.value = _counter;
                    _counter++;
                }
            }

            foreach (var ruleGroup in ruleGroups)
            {
                ruleGroup.intGrid = this;
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
    }
    
    [Serializable]
    public class IntGridValueDefinition
    {
        [ReadOnly]
        public short value = -1;
        public string name;
        public Color color;
        public Texture texture;
    }
}