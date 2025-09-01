using System;
using System.Collections.Generic;
using BovineLabs.Core.Utility;
using KrasCore.Mosaic.Data;
using Sirenix.OdinInspector;
using Sirenix.OdinInspector.Editor;
using Sirenix.Utilities.Editor;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace KrasCore.Mosaic.Authoring
{
    public class RuleGroupMatrixWindow : OdinEditorWindow
    {
        public static int NumberOfActiveInspectorWindows;
        
        [HorizontalGroup("Split", width: 0.2f)]
        [BoxGroup("Split/Select", centerLabel: true, LabelText = "Select IntGrid Value")]
        [IntGridValueSelectorDrawer]
        [SerializeField] private IntGridValueSelector _selectedIntGridValue;
        
        [HorizontalGroup("Split", width: 0.4f)]
        [IntGridMatrix(OnBeforeDrawCellMethod = nameof(OnBeforeDrawMatrixCell))]
        [SerializeField] private IntGridMatrix _matrix;
        
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
        
        private RuleGroup.Rule _target;
        
        public static void OpenWindow(RuleGroup.Rule target)
        {
            var window = GetWindow<RuleGroupMatrixWindow>(true, "Rule Matrix Window", true);
            window.Init(target);
            window.Show();
        }

        protected override void OnEnable()
        {
            base.OnEnable();
            AssemblyReloadEvents.beforeAssemblyReload += CloseWindowOnDomainReload;
        }

        protected override void OnDisable()
        {
            base.OnDisable();
            AssemblyReloadEvents.beforeAssemblyReload -= CloseWindowOnDomainReload;
        }

        private void CloseWindowOnDomainReload()
        {
            Close();
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

            _selectedIntGridValue = new IntGridValueSelector
            {
                intGrid = target.BoundIntGridDefinition,
                value = 1
            };
            _target = target;
        }
        
        public override void SaveChanges()
        {
            base.SaveChanges();
            
            if (_target != null)
            {
                EditorUtility.SetDirty(_target.RuleGroup);
            }
        }

        
        [Button]
        public void Print()
        {
            var s = "";
            for (int i = 0; i < _matrix.dualGridMatrix.Length; i++)
            {
                s += _matrix.dualGridMatrix[i].value + "|";
            }
            Debug.Log(_matrix.GetHashCode() + " : " + s);
        }

        
        private void OnInspectorUpdate()
        {
            SaveChanges();
            if (NumberOfActiveInspectorWindows == 0)
                Close();
        }
        
        private void OnBeforeDrawMatrixCell(VisualElement cell, int cellIndex, SerializedObject serializedObject)
        {
            var leftMouse = new Clickable(_ => LeftClick(cellIndex, serializedObject));
            leftMouse.activators.Add(new ManipulatorActivationFilter { button = MouseButton.LeftMouse });
            cell.AddManipulator(leftMouse);
            
            var rightMouse = new Clickable(_ => RightClick(cellIndex, serializedObject));
            rightMouse.activators.Add(new ManipulatorActivationFilter { button = MouseButton.RightMouse });
            cell.AddManipulator(rightMouse);
        }

        private void LeftClick(int cellIndex, SerializedObject serializedObject)
        {
            ref var slot = ref _matrix.GetCurrentMatrix()[cellIndex];
            
            slot = _selectedIntGridValue.value;

            serializedObject.ApplyModifiedProperties();
            serializedObject.Update();
        }        
        
        private void RightClick(int cellIndex, SerializedObject serializedObject)
        {
            ref var slot = ref _matrix.GetCurrentMatrix()[cellIndex];

            if (slot == 0)
            {
                slot = (short)-_selectedIntGridValue.value;
            }
            else
            {
                slot = 0;
            }
        }
    }
}