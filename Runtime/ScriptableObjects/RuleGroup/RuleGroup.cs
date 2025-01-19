using System;
using System.Collections.Generic;
using Sirenix.OdinInspector;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;

#if UNITY_EDITOR
using Sirenix.Utilities.Editor;
#endif

namespace KrasCore.Mosaic
{
    [HideMonoScript]
    [CreateAssetMenu(menuName = "Mosaic/RuleGroup")]
    public class RuleGroup : ScriptableObject
    {
        [ReadOnly]
        [Title("Bound IntGrid")]
        public IntGrid intGrid;
        
        [Title("Tile Rules")]
        public List<Rule> rules = new();

#if UNITY_EDITOR
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
#endif

        [Flags]
        public enum Enabled
        {
            [InspectorName("Enabled")] [Tooltip("Enable/disable this rule")]
            Enabled = 1
        }

        [Serializable]
        public class Rule
        {
            public const int MatrixSize = 9;
            public const int MatrixSizeHalf = MatrixSize / 2;
            public const int AnyIntGridValue = 999;

            [HorizontalGroup("Split", 0.1f)] [EnumToggleButtons, HideLabel]
            public Enabled enabled = Enabled.Enabled;

            [HorizontalGroup("Split", 0.2f)]
            [Matrix("DrawMatrixCell", MatrixRectMethod = "MatrixControlRect")]
            public int[] ruleMatrix = new int[MatrixSize * MatrixSize];
            
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
            [field: SerializeField, HideInInspector] public IntGrid BoundIntGrid { get; private set; }
            [field: SerializeField, HideInInspector] public RuleGroup RuleGroup { get; private set; }

#if UNITY_EDITOR
            public void Bind(RuleGroup ruleGroup)
            {
                BoundIntGrid = ruleGroup.intGrid;
                RuleGroup = ruleGroup;
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
            
            private int DrawMatrixCell(Rect rect, int index, int value)
            {
                RuleGroupEditorHelper.DrawMatrixCell(rect, index, value, BoundIntGrid, true);
                return value;
            }
#endif
            
            public static int2 GetOffsetFromCenterMirrored(int index, bool2 mirror)
            {
                var x = index % MatrixSize;
                var y = index / MatrixSize;

                var offset = new int2(x - MatrixSizeHalf, MatrixSizeHalf - y);
                
                if (mirror.x)
                {
                    offset.x = MatrixSizeHalf - x;
                }
                if (mirror.y)
                {
                    offset.y = y - MatrixSizeHalf;
                }
                return offset;
            }
            
            public static int2 GetOffsetFromCenterRotated(int index, int rotation)
            {
                var x = index % MatrixSize;
                var y = index / MatrixSize;

                var offset = new int2(x - MatrixSizeHalf, MatrixSizeHalf - y);

                return rotation switch
                {
                    0 => offset,
                    1 => new int2(offset.y, -offset.x),
                    2 => new int2(-offset.x, -offset.y),
                    3 => new int2(-offset.y, offset.x),
                    _ => default
                };
            }
        }
    }
}
