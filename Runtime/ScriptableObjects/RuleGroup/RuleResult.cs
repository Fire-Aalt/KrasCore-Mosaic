using System;
using Sirenix.OdinInspector;
using UnityEngine;
using Object = UnityEngine.Object;

namespace KrasCore.Mosaic
{
    [Serializable]
    public class EntityResult
    {
        public int weight;
        
        [LabelText("Entity")]
        [AssetsOnly]
        public GameObject result;
        
        public EntityResult(int weight, GameObject result)
        {
            this.weight = weight;
            this.result = result;
        }

        public void Validate()
        {
            weight = Mathf.Max(1, weight);
        }
    }
    
    [Serializable]
    public class SpriteResult
    {
        public int weight;
            
        [PreviewField]
        [LabelText("Sprite")]
        public Sprite result;

        public SpriteResult(int weight, Sprite result)
        {
            this.weight = weight;
            this.result = result;
        }

        public void Validate()
        {
            weight = Mathf.Max(1, weight);
        }
    }
    
    public enum RuleResultType
    {
        Sprite,
        Entity
    }
}