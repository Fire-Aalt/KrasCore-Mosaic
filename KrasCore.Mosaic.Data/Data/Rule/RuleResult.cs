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
        
        public EntityResult()
        {
            weight = 1;
        }
        
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
        [LabelWidth(width: 100)]
        public Sprite result;

        public SpriteResult()
        {
            weight = 1;
        }
        
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