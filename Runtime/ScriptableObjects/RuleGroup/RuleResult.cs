using System;
using Sirenix.OdinInspector;
using UnityEngine;
using Object = UnityEngine.Object;

namespace KrasCore.Mosaic
{
    [Serializable]
    public class RuleResult<T> where T : Object
    {
        public int weight;
            
        [PreviewField]
        [ShowIf("_resultType", RuleResultType.Sprite)]
        [LabelText("Sprite")]
        public T spriteResult;
            
        [ShowIf("_resultType", RuleResultType.Entity)]
        [LabelText("Entity")]
        [AssetsOnly]
        public T entityResult;

        private RuleResultType _resultType;
            
        public RuleResult(int weight, T spriteResult = null, T entityResult = null)
        {
            this.weight = weight;
            this.spriteResult = spriteResult;
            this.entityResult = entityResult;
        }

        public void Validate(RuleResultType resultType)
        {
            weight = Mathf.Max(1, weight);
            _resultType = resultType;
        }
    }
    
    public enum RuleResultType
    {
        Sprite,
        Entity
    }
}