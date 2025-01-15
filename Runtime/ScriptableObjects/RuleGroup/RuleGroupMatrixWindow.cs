#if UNITY_EDITOR
using System.Collections.Generic;
using Sirenix.OdinInspector;
using Sirenix.OdinInspector.Editor;
using Sirenix.Utilities.Editor;
using UnityEngine;

namespace KrasCore.Mosaic
{
    public class RuleGroupMatrixWindow : OdinEditorWindow
    {
        public static int NumberOfActiveInspectorWindows;
        
        [HorizontalGroup("Split", width: 0.2f)]
        [CustomValueDrawer("IntGridValueDrawer")]
        [SerializeField] private int _selectedIntGridValue;
        
        [HorizontalGroup("Split", width: 0.4f)]
        [TableMatrix(DrawElementMethod = "DrawMatrixCell", ResizableColumns = false, SquareCells = true, HideColumnIndices = true, HideRowIndices = true, IsReadOnly = true)]
        [SerializeField] private int[,] _matrix = new int[RuleGroup.Rule.MatrixSize, RuleGroup.Rule.MatrixSize];
        
        //TODO: add random behavior (UI problems)
        // [HorizontalGroup("Split", width: 0.2f)]
        // [HideLabel, EnumToggleButtons]
        // [SerializeField] private RandomBehavior _randomBehavior;
        
        [HorizontalGroup("Split", width: 0.2f)]
        [VerticalGroup("Split/Sprites")]
        [SerializeField] private List<SpriteResult> _tileSprites;
        
        [HorizontalGroup("Split", width: 0.2f)]
        [VerticalGroup("Split/Sprites")]
        [SerializeField] private List<Sprite> _convertSprites = new();
        
        [HorizontalGroup("Split", width: 0.2f)]
        [VerticalGroup("Split/Entities")]
        [SerializeField] private List<EntityResult> _tileEntities;
        
        [HorizontalGroup("Split", width: 0.2f)]
        [VerticalGroup("Split/Entities")]
        [AssetsOnly]
        [SerializeField] private List<GameObject> _convertPrefabs = new();
        
        private IntGrid _intGrid;
        
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
                    _tileSprites.Add(new SpriteResult(1, toConvert));
                }
                _convertSprites.Clear();
            }

            if (_convertPrefabs.Count > 0)
            {
                foreach (var toConvert in _convertPrefabs)
                {
                    _tileEntities.Add(new EntityResult(1, toConvert));
                }
                _convertPrefabs.Clear();
            }
        }

        private void Init(RuleGroup.Rule target)
        {
            _intGrid = target.BoundIntGrid;
            _matrix = target.Matrix;
            _tileEntities = target.TileEntities;
            _tileSprites = target.TileSprites;
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

            RuleGroupEditorHelper.DrawMatrixCell(rect, value, _intGrid, false);
            return value;
        }
    }
}
#endif