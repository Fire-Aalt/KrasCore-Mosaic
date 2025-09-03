using System;
using UnityEngine;

namespace KrasCore.Mosaic
{
    [Serializable]
    public class PrefabResult
    {
        public int weight;
        public GameObject result;
        
        public PrefabResult()
        {
            weight = 1;
        }
        
        public PrefabResult(GameObject result)
        {
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
        public Sprite result;

        public SpriteResult()
        {
            weight = 1;
        }
        
        public SpriteResult(Sprite result)
        {
            this.result = result;
        }

        public void Validate()
        {
            weight = Mathf.Max(1, weight);
        }
    }
}