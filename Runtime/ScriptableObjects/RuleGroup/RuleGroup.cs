using System;
using System.Collections.Generic;
using Sirenix.OdinInspector;
using Unity.Mathematics;
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
        //[ReadOnly]
        [Title("Bound IntGrid")]
        public IntGrid intGrid;
        
        [Title("Tile Rules")]
        public List<Rule> rules = new();
        
        [Button(ButtonSizes.Large)]
        public void AddRule()
        {
            var rule = new Rule();
            rule.Bind(intGrid);
            rules.Add(rule);
        }

        private void OnValidate()
        {
            foreach (var rule in rules)
            {
                rule.Bind(intGrid);
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
            public const int MatrixSize = 9;
            public const int MatrixSizeHalf = MatrixSize / 2;
            public const int AnyIntGridValue = 999;

            [HorizontalGroup("Split", 0.2f)] [EnumToggleButtons, HideLabel]
            public Enabled enabled = Enabled.Enabled;

            [HorizontalGroup("Split", 0.2f)]
            [ShowInInspector]
            [TableMatrix(DrawElementMethod = "DrawMatrixCell", ResizableColumns = false, SquareCells = true,
                HideColumnIndices = true, HideRowIndices = true, IsReadOnly = true)]
            public int[,] Matrix = new int[MatrixSize, MatrixSize];

            [HorizontalGroup("Split", 0.3f)] [VerticalGroup("Split/Right")] [LabelText("", SdfIconType.Dice6Fill)]
            public float ruleChance = 100f;

            [HorizontalGroup("Split", 0.3f)] [VerticalGroup("Split/Right")] [EnumToggleButtons, HideLabel]
            public RuleTransform ruleTransform;

            [HideInInspector, SerializeField]
            public int[] RuleMatrix = new int[MatrixSize * MatrixSize];
            [HideInInspector, SerializeField]
            public List<SpriteResult> TileSprites = new();
            [HideInInspector, SerializeField]
            public List<EntityResult> TileEntities = new();
            [HideInInspector, SerializeField]
            public IntGrid BoundIntGrid;

            public void OnBeforeSerialize()
            {
                for (int x = 0; x < MatrixSize; x++)
                {
                    for (int y = 0; y < MatrixSize; y++)
                    {
                        RuleMatrix[y * MatrixSize + x] = Matrix[x, y];
                    }
                }
            }

            public void OnAfterDeserialize()
            {
                for (int x = 0; x < MatrixSize; x++)
                {
                    for (int y = 0; y < MatrixSize; y++)
                    {
                        Matrix[x, y] = RuleMatrix[y * MatrixSize + x];
                    }
                }
            }

            public void Bind(IntGrid intGrid)
            {
                BoundIntGrid = intGrid;
            }

            public void Validate()
            {
                ruleChance = Mathf.Clamp(ruleChance, 0f, 100f);
            }

#if UNITY_EDITOR
            private int DrawMatrixCell(Rect rect, int value)
            {
                if (Event.current.OnMouseDown(rect, 0))
                {
                    RuleGroupMatrixWindow.OpenWindow(this);
                }

                RuleGroupEditorHelper.DrawMatrixCell(rect, value, BoundIntGrid, true);
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
