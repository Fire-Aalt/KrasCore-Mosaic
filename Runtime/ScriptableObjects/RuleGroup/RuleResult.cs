using System;
using Sirenix.OdinInspector;
using UnityEngine;

namespace KrasCore.Mosaic
{
    [Serializable]
    public class EntityResult
    {
        [TableColumnWidth(50, false)]
        public int weight;
        
        [AssetsOnly]
        [HideLabel]
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
        [TableColumnWidth(50, false)]
        public int weight;
            
        [PreviewField]
        [AssetsOnly]
        [LabelText("@result.name")]
        [LabelWidth(width: 100)]
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