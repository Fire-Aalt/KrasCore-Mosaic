using System;
using System.Collections.Generic;
using KrasCore.Editor;
using Sirenix.OdinInspector;
using UnityEngine;

namespace KrasCore.Mosaic
{
    [CreateAssetMenu(menuName = "Mosaic/IntGrid")]
    public class IntGrid : ScriptableObject
    {
        public List<IntGridValue> intGridValues = new();

        //[ReadOnly]
        public List<RuleGroup> ruleGroups = new();
        
        [SerializeField, HideInInspector]
        private int _counter = 1;

        [Button]
        private void CreateRuleGroup()
        {
            var instance = AssetDatabaseUtils.CreateNewScriptableObjectAsset<RuleGroup>("RuleGroup", this);
            instance.intGrid = this;
            ruleGroups.Add(instance);
        }
        
        private void OnValidate()
        {
            foreach (var intGridValue in intGridValues)
            {
                if (intGridValue.value == -1)
                {
                    intGridValue.value = _counter;
                    _counter++;
                }
            }
        }
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