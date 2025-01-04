using System;
using System.Collections.Generic;
using Sirenix.OdinInspector;
using Sirenix.Utilities.Editor;
using UnityEngine;

namespace Mosaic.Runtime
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
        public enum Mirror
        {
            [InspectorName("X")] [Tooltip("X mirror. Enable this to also check for match when mirrored horizontally")]
            MirrorX = 1,

            [InspectorName("Y")] [Tooltip("Y mirror. Enable this to also check for match when mirrored vertically")]
            MirrorY = 2
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
            public Mirror mirror;

            public int[] RuleMatrix { get; private set; } = new int[MatrixSize * MatrixSize];
            public List<RuleResult<Sprite>> TileSprites { get; private set; } = new();
            public List<RuleResult<GameObject>> TileEntities { get; private set; } = new();
            public RuleResultType ResultType { get; set; }

            public IntGrid BoundIntGrid { get; private set; }

            public void OnBeforeSerialize()
            {
                for (int x = 0; x < MatrixSize; x++)
                {
                    for (int y = 0; y < MatrixSize; y++)
                    {
                        RuleMatrix[x * MatrixSize + y] = Matrix[x, y];
                    }
                }
            }

            public void OnAfterDeserialize()
            {
                for (int x = 0; x < MatrixSize; x++)
                {
                    for (int y = 0; y < MatrixSize; y++)
                    {
                        Matrix[x, y] = RuleMatrix[x * MatrixSize + y];
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

                RuleGroupEditorHelper.DrawMatrixCell(rect, value, BoundIntGrid.intGridValues, true);
                return value;
            }
#endif
        }
    }
}
