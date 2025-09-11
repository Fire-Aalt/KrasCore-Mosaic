using System;
using System.Collections.Generic;
using KrasCore.Mosaic.Data;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;
using UnityEngine.Serialization;

namespace KrasCore.Mosaic.Authoring
{
    [CreateAssetMenu(menuName = "Mosaic/RuleGroup")]
    public class RuleGroup : ScriptableObject
    {
        [Header("Bound IntGrid")]
        public IntGridDefinition intGrid;
        
        [Header("Tile Rules")]
        public List<Rule> rules = new();

        public void AddRule()
        {
            var rule = new Rule();
            rule.Bind(this, -1);
            rules.Add(rule);
            EditorUtility.SetDirty(this);

            OnValidate();
        }

        private void OnValidate()
        {
            for (var ruleIndex = 0; ruleIndex < rules.Count; ruleIndex++)
            {
                var rule = rules[ruleIndex];
                rule.Bind(this, ruleIndex);
                rule.Validate();
            }
        }

        [Flags]
        public enum Enabled
        {
            [InspectorName("Enabled")] [Tooltip("Enable/disable this rule")]
            Enabled = 1
        }
        
        [Serializable]
        public class Rule : ISerializationCallbackReceiver
        {
            private const int MatrixSizeConst = 9;
            
            public int MatrixSize => ruleMatrix.GetCurrentSize();
            public int MatrixSizeHalf => MatrixSize / 2;

            public Enabled enabled = Enabled.Enabled;

            public IntGridMatrix ruleMatrix = new(MatrixSizeConst);
            
            public float ruleChance = 100f;
            public Transformation ruleTransformation;
            
            [FormerlySerializedAs("resultTransform")]
            public Transformation resultTransformation;

#pragma warning disable CS0612 // Type or member is obsolete
            [SerializeField, FormerlySerializedAs("ruleTransform")]
            private RuleTransform _ruleTransformToMigrate;
            
            public void OnBeforeSerialize()
            {
                
            }

            public void OnAfterDeserialize()
            {
                if (_ruleTransformToMigrate == RuleTransform.Migrated) return;
                
                if (_ruleTransformToMigrate == RuleTransform.MirrorX |
                    _ruleTransformToMigrate == RuleTransform.MirrorXY)
                {
                    ruleTransformation ^= Transformation.MirrorX;
                }
                if (_ruleTransformToMigrate == RuleTransform.MirrorY |
                    _ruleTransformToMigrate == RuleTransform.MirrorXY)
                {
                    ruleTransformation ^= Transformation.MirrorY;
                }
                if (_ruleTransformToMigrate == RuleTransform.Rotated)
                {
                    ruleTransformation ^= Transformation.Rotated;
                }
                _ruleTransformToMigrate = RuleTransform.Migrated;
            }
#pragma warning restore CS0612 // Type or member is obsolete
            
            [HideInInspector]
            [FormerlySerializedAs("<TileSprites>k__BackingField")]
            public List<SpriteResult> TileSprites = new();
            
            [HideInInspector]
            [FormerlySerializedAs("<TileEntities>k__BackingField")]
            public List<PrefabResult> TileEntities = new();
            
            [field: SerializeField, HideInInspector] public IntGridDefinition BoundIntGridDefinition { get; private set; }
            [field: SerializeField, HideInInspector] public RuleGroup RuleGroup { get; private set; }
            [field: SerializeField, HideInInspector] public int RuleIndex { get; private set; }
            
            public void Bind(RuleGroup ruleGroup, int index)
            {
                BoundIntGridDefinition = ruleGroup.intGrid;
                RuleGroup = ruleGroup;
                RuleIndex = index;
                ruleMatrix.intGrid = BoundIntGridDefinition;
            }

            public void Validate()
            {
                ruleChance = Mathf.Clamp(ruleChance, 0f, 100f);
            }
            
            // Handles both even and odd matrices
            public int2 GetOffsetFromCenterMirrored(int index, bool2 mirror)
            {
                var x = index % MatrixSize;
                var y = index / MatrixSize;
                
                if (mirror.x) x = (MatrixSize - 1) - x;
                if (mirror.y) y = (MatrixSize - 1) - y;
                
                var res = new int2(
                    x - MatrixSizeHalf,
                    MatrixSizeHalf - y
                );
                if (MatrixSize % 2 == 0) res += new int2(1, 0);
                
                return res;
            }
            
            // Handles both even and odd matrices
            public int2 GetOffsetFromCenterRotated(int index, int rotation)
            {
                var x = index % MatrixSize;
                var y = index / MatrixSize;
                
                var x2 = x * 2 - (MatrixSize - 1);
                var y2 = (MatrixSize - 1) - y * 2;
    
                var rd2 = rotation switch
                {
                    0 => new int2(x2, y2),
                    1 => new int2(y2, -x2),
                    2 => new int2(-x2, -y2),
                    3 => new int2(-y2, x2),
                    _ => default
                };
    
                x = (rd2.x + (MatrixSize - 1)) / 2;
                y = ((MatrixSize - 1) - rd2.y) / 2;

                var res = new int2(
                    x - MatrixSizeHalf,
                    MatrixSizeHalf - y
                );
                if (MatrixSize % 2 == 0) res += new int2(1, 0);
                
                return res;
            }
        }
    }
}
