using System;
using Sirenix.OdinInspector;
using UnityEngine;

namespace KrasCore.Mosaic
{
    [Serializable]
    public struct EntityResult
    {
        public int weight;
        
        [AssetsOnly]
        [LabelText("Entity")]
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
    public struct SpriteResult
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
}