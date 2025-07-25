using System;
using System.Collections.Generic;
using KrasCore.Mosaic.Data;
using Sirenix.OdinInspector;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;
using Sirenix.Utilities.Editor;

namespace KrasCore.Mosaic.Authoring
{
    [HideMonoScript]
    [CreateAssetMenu(menuName = "Mosaic/RuleGroup")]
    public class RuleGroup : ScriptableObject
    {
        [ReadOnly]
        [Title("Bound IntGrid")]
        public IntGridDefinition intGrid;
        
        [Title("Tile Rules")]
        public List<Rule> rules = new();

        [Button(ButtonSizes.Large)]
        public void AddRule()
        {
            var rule = new Rule();
            rule.Bind(this);
            rules.Add(rule);
            EditorUtility.SetDirty(this);
        }

        private void OnValidate()
        {
            foreach (var rule in rules)
            {
                rule.Bind(this);
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
        public class Rule
        {
            private const int MatrixSizeConst = 9;
            
            public int MatrixSize => ruleMatrix.GetCurrentSize();
            public int MatrixSizeHalf => MatrixSize / 2;

            [HorizontalGroup("Split", 0.1f)] [EnumToggleButtons, HideLabel]
            public Enabled enabled = Enabled.Enabled;

            [HorizontalGroup("Split", 0.2f)]
            [IntGridMatrix(MatrixRectMethod = nameof(MatrixControlRect), IsReadonly = true)]
            public IntGridMatrix ruleMatrix = new(MatrixSizeConst);
            
            [HorizontalGroup("Split", 0.33f), BoxGroup("Split/Rule", centerLabel: true)] 
            [LabelText("", SdfIconType.Dice6Fill)]
            public float ruleChance = 100f;

            [Header("Transformation")]
            [HorizontalGroup("Split", 0.33f), BoxGroup("Split/Rule")] 
            [EnumToggleButtons, HideLabel]
            public RuleTransform ruleTransform;
            
            [Header("Transformation")]
            [HorizontalGroup("Split", 0.33f), BoxGroup("Split/Result", centerLabel: true)] 
            [EnumToggleButtons, HideLabel]
            public ResultTransform resultTransform;

            [field: SerializeField, HideInInspector] public List<SpriteResult> TileSprites { get; set; }
            [field: SerializeField, HideInInspector] public List<EntityResult> TileEntities { get; set; }
            [field: SerializeField, HideInInspector] public IntGridDefinition BoundIntGridDefinition { get; private set; }
            [field: SerializeField, HideInInspector] public RuleGroup RuleGroup { get; private set; }

            public void Bind(RuleGroup ruleGroup)
            {
                BoundIntGridDefinition = ruleGroup.intGrid;
                RuleGroup = ruleGroup;
                ruleMatrix.intGrid = BoundIntGridDefinition;
            }

            public void Validate()
            {
                TileSprites ??= new List<SpriteResult>();
                TileEntities ??= new List<EntityResult>();
                ruleChance = Mathf.Clamp(ruleChance, 0f, 100f);
            }

            private void MatrixControlRect(Rect rect)
            {
                if (Event.current.OnMouseDown(rect, 0))
                {
                    RuleGroupMatrixWindow.OpenWindow(this);
                }
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
