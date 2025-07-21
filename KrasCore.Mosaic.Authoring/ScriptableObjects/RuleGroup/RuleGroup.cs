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
            
            public int2 GetOffsetFromCenterMirrored(int index, bool2 mirror)
            {
                var x = index % MatrixSize;
                var y = index / MatrixSize;
                
                if (MatrixSize % 2 != 0)
                {
                    var offset = new int2(x - MatrixSizeHalf, MatrixSizeHalf - y);
                    
                    if (mirror.x) offset.x = MatrixSizeHalf - x;
                    if (mirror.y) offset.y = y - MatrixSizeHalf;
                
                    return offset;
                }
                else
                {
                    var temp = new int2(x, y);
                    
                    if (mirror.x) temp.x = x + 1 - MatrixSizeHalf;
                    if (mirror.y) temp.y = y + 1 - MatrixSizeHalf;
                
                    return new int2(x - MatrixSizeHalf + 1, MatrixSizeHalf - y);
                }
            }
            
            public int2 GetOffsetFromCenterRotated(int index, int rotation)
            {
                var x = index % MatrixSize;
                var y = index / MatrixSize;

                // rotation++;
                // var tmp = x;
                // x = MatrixSize - y - 1;
                // y = tmp;
                // Debug.Log($"Return: {x} {y} Before: {index % MatrixSize} {index / MatrixSize}");
                //         // antiClockwise
                //         //res[MatrixSize - j - 1][i] = mat[i][j];
                //         
                //         // clockwise
                //         //res[j][MatrixSize - i - 1] = mat[i][j];
                //
                // Debug.Log($"After {0} rotations: {x} {y}");
                // for (int i = 0; i < rotation; i++)
                // {
                //     tmp = x;
                //     x = y;
                //     y = MatrixSize - tmp - 1;
                //     Debug.Log($"After {i + 1} rotations: {x} {y}");
                // }
                //
                // var res = new int2(MatrixSizeHalf - x, MatrixSizeHalf - y);
                // //if (MatrixSize % 2 == 0) res += new int2(1, 1);
                // return res;
                
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
    
                var nx = (rd2.x + (MatrixSize - 1)) / 2;
                var ny = ((MatrixSize - 1) - rd2.y) / 2;

                var ans = new int2(
                    nx - MatrixSizeHalf,
                    MatrixSizeHalf - ny
                );
                if (MatrixSize % 2 == 0) ans += new int2(1, 0);
                
                return ans;
            }
        }
    }
}
