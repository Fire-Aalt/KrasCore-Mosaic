#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using Sirenix.OdinInspector;
using Sirenix.OdinInspector.Editor;
using Sirenix.Utilities.Editor;
using UnityEditor;
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
        [Matrix(nameof(DrawMatrixCell))]
        [SerializeField] private int[] _matrix;
        

        //TODO: add random behavior (UI problems)
        // [HorizontalGroup("Split", width: 0.2f)]
        // [HideLabel, EnumToggleButtons]
        // [SerializeField] private RandomBehavior _randomBehavior;
        
        [ListDrawerSettings(HideAddButton = true,  NumberOfItemsPerPage = 5)]
        [HorizontalGroup("Split", width: 0.2f)]
        [VerticalGroup("Split/Sprites")]
        [LabelText("List")]
        [Title("Sprites")]
        [SerializeField] private List<SpriteResult> _tileSprites;
        
        [HorizontalGroup("Split", width: 0.2f)]
        [VerticalGroup("Split/Sprites")]
        [SerializeField] private List<Sprite> _convertSprites = new();

        [ListDrawerSettings(HideAddButton = true, NumberOfItemsPerPage = 5)]
        [HorizontalGroup("Split", width: 0.2f)]
        [VerticalGroup("Split/Entities")]
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
            SaveChanges();
            
            _matrix = target.ruleMatrix;
            _tileEntities = target.TileEntities;
            _tileSprites = target.TileSprites;
            
            _intGrid = target.BoundIntGrid;
            _target = target;
            _selectedIntGridValue = 1;
        }

        protected override void OnDisable()
        {
            base.OnDisable();
            
            SaveChanges();
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
#endif