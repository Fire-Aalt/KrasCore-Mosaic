#if UNITY_EDITOR
using System.Collections.Generic;
using Sirenix.OdinInspector;
using Sirenix.OdinInspector.Editor;
using Sirenix.Utilities.Editor;
using UnityEngine;

namespace Mosaic.Runtime
{
    public class RuleGroupMatrixWindow : OdinEditorWindow
    {
        public static int NumberOfActiveInspectorWindows;
        
        [HorizontalGroup("Split", width: 0.2f)]
        [CustomValueDrawer("IntGridValueDrawer")]
        [SerializeField] private int _selectedIntGridValue;
        
        [HorizontalGroup("Split", width: 0.5f)]
        [TableMatrix(DrawElementMethod = "DrawMatrixCell", ResizableColumns = false, SquareCells = true, HideColumnIndices = true, HideRowIndices = true, IsReadOnly = true)]
        [SerializeField] private int[,] _matrix = new int[RuleGroup.Rule.MatrixSize, RuleGroup.Rule.MatrixSize];
        
        
        [HorizontalGroup("Split", width: 0.3f)]
        [VerticalGroup("Split/Right")]
        [EnumToggleButtons, HideLabel]
        [SerializeField] private RuleResultType _ruleResultType;
        
        [HorizontalGroup("Split", width: 0.3f)]
        [VerticalGroup("Split/Right")]
        [ShowIf("_ruleResultType", RuleResultType.Sprite)]
        [SerializeField] private List<RuleResult<Sprite>> _tileSprites = new();
        
        [HorizontalGroup("Split", width: 0.3f)]
        [VerticalGroup("Split/Right")]
        [ShowIf("_ruleResultType", RuleResultType.Entity)]
        [SerializeField] private List<RuleResult<GameObject>> _tileEntities = new();
        
        [HorizontalGroup("Split", width: 0.3f)]
        [VerticalGroup("Split/Right")]
        [ShowIf("_ruleResultType", RuleResultType.Sprite)]
        [SerializeField] private List<Sprite> _convertSprites = new();
        
        [HorizontalGroup("Split", width: 0.3f)]
        [VerticalGroup("Split/Right")]
        [ShowIf("_ruleResultType", RuleResultType.Entity)]
        [AssetsOnly]
        [SerializeField] private List<GameObject> _convertGameObjects = new();
        
        private IntGrid _intGrid;
        private RuleGroup.Rule _target;
        
        public static void OpenWindow(RuleGroup.Rule target)
        {
            var window = GetWindow<RuleGroupMatrixWindow>(true, "Rule Matrix Window", true);
            window.Init(target);
            window.Show();
        }

        private void OnValidate()
        {
            if (_convertSprites.Count > 0)
            {
                foreach (var toConvert in _convertSprites)
                {
                    _tileSprites.Add(new RuleResult<Sprite>(1, spriteResult: toConvert));
                }
                _convertSprites.Clear();
            }
            foreach (var result in _tileSprites)
            {
                result.Validate(RuleResultType.Sprite);
            }

            if (_convertGameObjects.Count > 0)
            {
                foreach (var toConvert in _convertGameObjects)
                {
                    _tileEntities.Add(new RuleResult<GameObject>(1, entityResult: toConvert));
                }
                _convertGameObjects.Clear();
            }
            foreach (var result in _tileEntities)
            {
                result.Validate(RuleResultType.Entity);
            }
            _target.ResultType = _ruleResultType;
        }

        private void Init(RuleGroup.Rule target)
        {
            _target = target;
            _intGrid = target.BoundIntGrid;
            _matrix = target.Matrix;
            _tileEntities = target.TileEntities;
            _tileSprites = target.TileSprites;
            _ruleResultType = target.ResultType;
            _selectedIntGridValue = 1;
        }

        private void OnInspectorUpdate()
        {
            if (NumberOfActiveInspectorWindows == 0)
                Close();
        }

        private int IntGridValueDrawer(int intGridValue)
        {
            return RuleGroupEditorHelper.IntGridValueDrawer(intGridValue, _intGrid.intGridValues);
        }
        
        private int DrawMatrixCell(Rect rect, int value)
        {
            if (Event.current.OnMouseDown(rect, 0))
            {
                value = _selectedIntGridValue;
                GUI.changed = true;
            }
            else if (Event.current.OnMouseDown(rect, 1))
            {
                if (value == 0) value = -_selectedIntGridValue;
                else if (value == _selectedIntGridValue) value = -_selectedIntGridValue;
                else value = 0;
                GUI.changed = true;
            }

            RuleGroupEditorHelper.DrawMatrixCell(rect, value, _intGrid.intGridValues, false);
            return value;
        }
    }
}
#endif