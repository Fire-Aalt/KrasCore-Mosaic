using System.Collections.Generic;
using Sirenix.OdinInspector;
using Sirenix.OdinInspector.Editor;
using Sirenix.Utilities.Editor;
using UnityEditor;
using UnityEngine;

namespace KrasCore.Mosaic.Authoring
{
    public class RuleGroupMatrixWindow : OdinEditorWindow
    {
        public static int NumberOfActiveInspectorWindows;
        
        [HorizontalGroup("Split", width: 0.2f)]
        [BoxGroup("Split/Select", centerLabel: true, LabelText = "Select IntGrid Value")]
        [CustomValueDrawer("IntGridValueDrawer")]
        [SerializeField] private int _selectedIntGridValue;
        
        [HorizontalGroup("Split", width: 0.4f)]
        [Matrix(nameof(DrawMatrixCell))]
        [SerializeField] private int[] _matrix;
        
        [HorizontalGroup("Split", width: 0.2f)]
        [VerticalGroup("Split/Sprites")]
        [Title("Sprites")]
        //[TableList(HideToolbar = true)]
        [SerializeField] private List<SpriteResult> _tileSprites;
        
        [HorizontalGroup("Split", width: 0.2f)]
        [VerticalGroup("Split/Sprites")]
        [AssetsOnly]
        [SerializeField] private List<Sprite> _convertSprites = new();

        [HorizontalGroup("Split", width: 0.2f)]
        [VerticalGroup("Split/Entities")]
        [Title("Entities")]
        //[TableList(HideToolbar = true)]
        [SerializeField] private List<EntityResult> _tileEntities;
        
        [HorizontalGroup("Split", width: 0.2f)]
        [VerticalGroup("Split/Entities")]
        [AssetsOnly]
        [SerializeField] private List<GameObject> _convertPrefabs = new();
        
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
                    _tileSprites.Add(new SpriteResult(1, toConvert));
                }
                _convertSprites.Clear();
            }

            _tileSprites.RemoveAll((s) => s.result == null);
            foreach (var result in _tileSprites)
            {
                result.Validate();
            }

            if (_convertPrefabs.Count > 0)
            {
                foreach (var toConvert in _convertPrefabs)
                {
                    _tileEntities.Add(new EntityResult(1, toConvert));
                }
                _convertPrefabs.Clear();
            }
            
            _tileEntities.RemoveAll((s) => s.result == null);
            foreach (var result in _tileEntities)
            {
                result.Validate();
            }
          
            EditorUtility.SetDirty(_target.RuleGroup);
        }

        private void Init(RuleGroup.Rule target)
        {
            _matrix = target.ruleMatrix;
            _tileEntities = target.TileEntities;
            _tileSprites = target.TileSprites;
            
            _intGrid = target.BoundIntGrid;
            _target = target;
            _selectedIntGridValue = 1;
        }
        
        public override void SaveChanges()
        {
            base.SaveChanges();
            
            if (_target != null)
            {
                EditorUtility.SetDirty(_target.RuleGroup);
            }
        }

        private void OnInspectorUpdate()
        {
            SaveChanges();
            if (NumberOfActiveInspectorWindows == 0)
                Close();
        }

        private int IntGridValueDrawer(int intGridValue)
        {
            return RuleGroupEditorHelper.IntGridValueDrawer(intGridValue, _intGrid.intGridValues);
        }
        
        private int DrawMatrixCell(Rect rect, int index, int value)
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

            RuleGroupEditorHelper.DrawMatrixCell(rect, index, value, _intGrid, false);
            return value;
        }
    }
}