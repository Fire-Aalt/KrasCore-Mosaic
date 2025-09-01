
// Assets/Editor/RuleGroupMatrixWindowUITK.cs

using System;
using System.Collections.Generic;
using KrasCore.Mosaic.Editor;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace KrasCore.Mosaic.Authoring
{
    public class RuleGroupMatrixWindowUITK : EditorWindow
    {
        public static int NumberOfActiveInspectorWindows;

        // These fields stay serialized on the EditorWindow so we can bind them.
        [IntGridValueSelectorDrawer]
        [SerializeField, HideInInspector] private IntGridValueSelector _selectedIntGridValue;

        private List<SpriteResult> _tileSprites;
        private List<EntityResult> _tileEntities;

        
        private SerializedObject _window;
        private SerializedObject _obj;
        private RuleGroup.Rule _target => _ruleGroup.rules[index];
        private RuleGroup _ruleGroup;
        private int index;

        public static void OpenWindow(RuleGroup.Rule target)
        {
            Debug.Log("OpenWindow + " + target.GetHashCode());
            var wnd = GetWindow<RuleGroupMatrixWindowUITK>(
                true,
                "Rule Matrix Window",
                true
            );
            
            wnd.Init(target);
            wnd.Show();
        }

        private void OnEnable()
        {
            AssemblyReloadEvents.beforeAssemblyReload += CloseOnDomainReload;
        }


        
        private void OnDisable()
        {
            AssemblyReloadEvents.beforeAssemblyReload -= CloseOnDomainReload;
        }

        private void CloseOnDomainReload()
        {
            Close();
        }

        private void Init(RuleGroup.Rule target)
        {
            // Mirror your Odin window's Init
            _tileEntities = target.TileEntities;
            _tileSprites = target.TileSprites;

            _selectedIntGridValue = new IntGridValueSelector
            {
                intGrid = target.BoundIntGridDefinition,
                value = 1
            };

            _ruleGroup = target.RuleGroup;
            Debug.Log("Init" + _ruleGroup.GetHashCode());
            
            for (int i = 0; i < target.RuleGroup.rules.Count; i++)
            {
                if (target.Equals(target.RuleGroup.rules[i]))
                {
                    index = i;
                    break;
                }
            }

            _window = new SerializedObject(this);
            _obj = new SerializedObject(_ruleGroup);
            
            Create();
        }

        // Build the UI Toolkit layout
        private void Create()
        {
            var root = rootVisualElement;
            root.Clear();
            root.style.flexDirection = FlexDirection.Column;

            // Optional: top toolbar
            var toolbar = new Toolbar();
            var printBtn = new ToolbarButton(Print) { text = "Print Matrix" };
            toolbar.Add(printBtn);
            root.Add(toolbar);

            // 4 columns: 0.2 | 0.4 | 0.2 | 0.2
            var row = new VisualElement
            {
                style =
                {
                    flexDirection = FlexDirection.Row,
                    flexGrow = 1
                }
            };
            root.Add(row);

            VisualElement MakeCol(float grow) =>
                new VisualElement
                {
                    style =
                    {
                        flexDirection = FlexDirection.Column,
                        flexGrow = grow,
                        marginLeft = 4,
                        marginRight = 4
                    }
                };

            var colSelect = MakeCol(2); // 20%
            var colMatrix = MakeCol(4); // 40%
            // var colSprites = MakeCol(2); // 20%
            // var colEntities = MakeCol(2); // 20%

            row.Add(colSelect);
            row.Add(colMatrix);
            // row.Add(colSprites);
            // row.Add(colEntities);
            Debug.Log(_target);
            Debug.Log(_target.RuleGroup);
            


            // {
            //     var box = new GroupBox { text = "Select IntGrid Value" };
            //     var pf = new PropertyField(_window.FindProperty("_selectedIntGridValue"));
            //     box.Add(pf);
            //     colSelect.Add(box);
            // }
            
            // Column 2: Matrix (uses your PropertyDrawer if it's a Unity drawer)
            {
                var matrixProperty = _obj.FindProperty("rules").GetArrayElementAtIndex(index)
                    .FindPropertyRelative("ruleMatrix");

                var fieldInfo = _target.GetType().GetField("ruleMatrix");
                
                var pf = new IntGridMatrixDrawer().Create(this, _target, fieldInfo,
                    new IntGridMatrixAttribute(nameof(OnBeforeDrawMatrixCell)), matrixProperty);
                colMatrix.Add(pf);
            }

            // Column 3: Sprites
            // {
            //     var spritesGroup = new GroupBox { text = "Sprites" };
            //
            //     var tileSpritesPF = new PropertyField(_so.FindProperty("_tileSprites"));
            //     spritesGroup.Add(tileSpritesPF);
            //
            //     var convertSpritesPF =
            //         new PropertyField(_so.FindProperty("_convertSprites"))
            //         {
            //             name = "ConvertSprites"
            //         };
            //     spritesGroup.Add(convertSpritesPF);
            //
            //     // Enforce "Assets only" on ObjectFields under this section
            //     convertSpritesPF.RegisterCallback<AttachToPanelEvent>(_ =>
            //     {
            //         foreach (var of in convertSpritesPF.Query<ObjectField>().ToList())
            //         {
            //             of.allowSceneObjects = false;
            //         }
            //     });
            //
            //     colSprites.Add(spritesGroup);
            // }
            //
            // // Column 4: Entities
            // {
            //     var entitiesGroup = new GroupBox { text = "Entities" };
            //
            //     var tileEntitiesPF = new PropertyField(_so.FindProperty("_tileEntities"));
            //     entitiesGroup.Add(tileEntitiesPF);
            //
            //     var convertPrefabsPF =
            //         new PropertyField(_so.FindProperty("_convertPrefabs"))
            //         {
            //             name = "ConvertPrefabs"
            //         };
            //     entitiesGroup.Add(convertPrefabsPF);
            //
            //     convertPrefabsPF.RegisterCallback<AttachToPanelEvent>(_ =>
            //     {
            //         foreach (var of in convertPrefabsPF.Query<ObjectField>().ToList())
            //         {
            //             of.allowSceneObjects = false;
            //         }
            //     });
            //
            //     colEntities.Add(entitiesGroup);
            // }
            //
            // // Bind all fields
            // root.Bind(_so);

            // Mirror your auto-save behavior
            EditorApplication.update -= AutoSaveAndAutoClose;
            EditorApplication.update += AutoSaveAndAutoClose;
        }

        private void AutoSaveAndAutoClose()
        {
            SaveChanges();
            if (NumberOfActiveInspectorWindows == 0)
            {
                EditorApplication.update -= AutoSaveAndAutoClose;
                Close();
            }
        }

        public void SaveChanges()
        {
            // Sync window fields into the serialized object and mark target dirty
            // if (_so == null) return;
            //
            // _so.ApplyModifiedPropertiesWithoutUndo();
            // _so.Update();

            if (_target != null)
            {
                EditorUtility.SetDirty(_target.RuleGroup);
            }

            // Optional: auto-convert items like your old OnValidate
            DoConversionsIfNeeded();
        }

        private void DoConversionsIfNeeded()
        {
            // Sprites
            // if (_convertSprites != null && _convertSprites.Count > 0)
            // {
            //     foreach (var toConvert in _convertSprites)
            //     {
            //         if (toConvert != null)
            //             _tileSprites.Add(new SpriteResult(1, toConvert));
            //     }
            //     _convertSprites.Clear();
            // }
            //
            // _tileSprites?.RemoveAll(s => s.result == null);
            // if (_tileSprites != null)
            // {
            //     foreach (var r in _tileSprites) r.Validate();
            // }
            //
            // // Entities
            // if (_convertPrefabs != null && _convertPrefabs.Count > 0)
            // {
            //     foreach (var toConvert in _convertPrefabs)
            //     {
            //         if (toConvert != null)
            //             _tileEntities.Add(new EntityResult(1, toConvert));
            //     }
            //     _convertPrefabs.Clear();
            // }
            //
            // _tileEntities?.RemoveAll(s => s.result == null);
            // if (_tileEntities != null)
            // {
            //     foreach (var r in _tileEntities) r.Validate();
            // }
        }

        private void Print()
        {
            if (_target.ruleMatrix.dualGridMatrix == null) return;
            var s = "";
            for (int i = 0; i < _target.ruleMatrix.dualGridMatrix.Length; i++)
            {
                s += _target.ruleMatrix.dualGridMatrix[i].value + "|";
            }
            Debug.Log(_target.ruleMatrix.GetHashCode() + " : " + _target.GetHashCode()  + " | " + s);
        }

        // Keep your existing callback so your matrix drawer can call into it
        private void OnBeforeDrawMatrixCell(VisualElement cell, int cellIndex, SerializedObject serializedObject)
        {
            var leftMouse = new Clickable(_ => LeftClick(cellIndex, serializedObject));
            leftMouse.activators.Add(
                new ManipulatorActivationFilter { button = MouseButton.LeftMouse }
            );
            cell.AddManipulator(leftMouse);

            var rightMouse = new Clickable(_ => RightClick(cellIndex, serializedObject));
            rightMouse.activators.Add(
                new ManipulatorActivationFilter { button = MouseButton.RightMouse }
            );
            cell.AddManipulator(rightMouse);
        }

        private void LeftClick(int cellIndex, SerializedObject serializedObject)
        {
            ref var slot = ref _target.ruleMatrix.GetCurrentMatrix()[cellIndex];
            slot = _selectedIntGridValue.value;

            serializedObject.ApplyModifiedProperties();
            serializedObject.Update();
        }

        private void RightClick(int cellIndex, SerializedObject serializedObject)
        {
            ref var slot = ref _target.ruleMatrix.GetCurrentMatrix()[cellIndex];

            if (slot == 0)
                slot = (short)-_selectedIntGridValue.value;
            else
                slot = 0;

            serializedObject.ApplyModifiedProperties();
            serializedObject.Update();
        }
    }
}