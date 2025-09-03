
// Assets/Editor/RuleGroupMatrixWindowUITK.cs

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using KrasCore.Mosaic.Editor;
using Unity.Properties;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace KrasCore.Mosaic.Authoring
{
    public class RuleGroupMatrixWindowUITK : EditorWindow
    {
        public static int NumberOfActiveInspectorWindows;

        [SerializeField, HideInInspector] 
        private IntGridValueSelector _selectedIntGridValue;
        
        private SerializedObject _window;
        private SerializedObject _obj;
        
        private RuleGroup _ruleGroup;
        private int _ruleIndex;
        
        private RuleGroup.Rule TargetRule => _ruleGroup.rules[_ruleIndex];

        public static void OpenWindow(RuleGroup.Rule target)
        {
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
            _selectedIntGridValue = new IntGridValueSelector
            {
                intGrid = target.BoundIntGridDefinition
            };

            _ruleGroup = target.RuleGroup;
            _ruleIndex = target.RuleIndex;
            
            _window = new SerializedObject(this);
            _obj = new SerializedObject(_ruleGroup);
            
            Create();
        }

        private ListView _spritesListView;
        
        private void Create()
        {
            var root = rootVisualElement;
            root.Clear();
            root.style.flexDirection = FlexDirection.Column;
            root.styleSheets.Add(EditorResources.StyleSheet);

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
                        width = Length.Percent(grow * 100f),
                        marginLeft = 4,
                        marginRight = 4
                    }
                };

            var colSelect = MakeCol(0.2f); // 20%
            var colMatrix = MakeCol(0.4f); // 40%
            var colSprites = MakeCol(0.2f); // 20%
            var colEntities = MakeCol(0.2f); // 20%

            row.Add(colSelect);
            row.Add(colMatrix);
            row.Add(colSprites);
            row.Add(colEntities);

            {
                var box = new GroupBox { text = "Select IntGrid Value" };
                
                
                var fieldInfo = GetType().GetField("_selectedIntGridValue",BindingFlags.NonPublic | BindingFlags.Instance);
                
                var pf = IntGridValueSelectorDrawer.Create(fieldInfo, _window.FindProperty("_selectedIntGridValue"));
                box.Add(pf);
                colSelect.Add(box);
            }
            
            // Column 2: Matrix (uses your PropertyDrawer if it's a Unity drawer)
            {
                var matrixProperty = _obj.FindProperty("rules").GetArrayElementAtIndex(_ruleIndex)
                    .FindPropertyRelative("ruleMatrix");

                var fieldInfo = TargetRule.GetType().GetField("ruleMatrix");
                
                var pf = new IntGridMatrixDrawer().Create(this, fieldInfo,
                    new IntGridMatrixAttribute(nameof(OnBeforeDrawMatrixCell)), matrixProperty);
                colMatrix.Add(pf);
            }

            //Column 3: Sprites
            {
                const float borderSize = 4;
                
                _spritesListView = new ListView
                {
                    name = "SpritesListView",
                    allowAdd = true,
                    allowRemove = true,
                    reorderable = true,
                    makeHeader = () =>
                    {
                        var spritesToolbar = new Toolbar();
                        
                        spritesToolbar.AddToClassList("list-view-header");
                        
                        var lbl = new Label { text = "Tile Sprites" };
                        var spacer = new ToolbarSpacer();
                        var addBtn = new ToolbarButton(OnAddClicked) { text = "Add" };
                        var removeBtn = new ToolbarButton(OnRemoveClicked) { text = "Remove" };
                        
                        lbl.AddToClassList("list-view-label");
                        spacer.AddToClassList("list-view-header-spacer");
                        addBtn.AddToClassList("list-view-button");
                        removeBtn.AddToClassList("list-view-button");
                        
                        spritesToolbar.Add(lbl);
                        spritesToolbar.Add(spacer);
                        spritesToolbar.Add(addBtn);
                        spritesToolbar.Add(removeBtn);
                        
                        return spritesToolbar;
                    },
                    selectionType = SelectionType.Multiple,
                    fixedItemHeight = 24 * 2 + borderSize * 2,
                    dataSource = _ruleGroup,
                    makeItem = () =>
                    {
                        var newListEntry = EditorResources.WeightedListElementAsset.Instantiate();
    
                        var newListEntryLogic = new WeightedListEntryController();
                        newListEntryLogic.SetVisualElement(newListEntry);
                        newListEntry.userData = newListEntryLogic;
                    
                        return newListEntry;
                    }
                };

                var tileSpritesSer = _obj.FindProperty(nameof(RuleGroup.rules)).GetArrayElementAtIndex(_ruleIndex)
                    .FindPropertyRelative("TileSprites");
                
                var path = PropertyPath.AppendIndex(PropertyPath.FromName(nameof(RuleGroup.rules)), _ruleIndex);
                
                _spritesListView.bindItem = (item, index) =>
                {
                    (item.userData as WeightedListEntryController).SetSpriteData(index, path, tileSpritesSer);
                };

                _spritesListView.BindProperty(tileSpritesSer);
                RegisterDragAndDrop(_spritesListView);
            
                colSprites.Add(_spritesListView);
            }
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

        
        private void RegisterDragAndDrop(VisualElement target)
        {
            target.RegisterCallback<DragUpdatedEvent>(evt =>
            {
                if (TryGetDraggedAssets(out _))
                {
                    DragAndDrop.visualMode = DragAndDropVisualMode.Copy;
                    evt.StopPropagation();
                }
            });

            target.RegisterCallback<DragPerformEvent>(evt =>
            {
                if (!TryGetDraggedAssets(out var assets)) return;

                DragAndDrop.AcceptDrag();

                var sprites = assets.Where(o => IsAsset(o) && o is Sprite)
                    .Select(sprite => new SpriteResult(1, sprite as Sprite))
                    .ToList();
                
                AddObjects(sprites);
                evt.StopPropagation();
            });
        }
        
        private void AddObjects(List<SpriteResult> sprites)
        {
            if (sprites.Count == 0) return;

            TargetRule.TileSprites.AddRange(sprites);
            _spritesListView.Rebuild();

            _spritesListView.SetSelection(new[] { TargetRule.TileSprites.Count - 1 });
            _spritesListView.ScrollToItem(TargetRule.TileSprites.Count - 1);
        }

        private static bool IsAsset(UnityEngine.Object o)
        {
            return o != null && EditorUtility.IsPersistent(o);
        }

        private bool TryGetDraggedAssets(out List<UnityEngine.Object> assets)
        {
            assets = DragAndDrop.objectReferences
                .Where(IsAsset)
                .Distinct()
                .ToList();

            return assets.Count > 0;
        }
        
        
        
        
        
        private void OnAddClicked()
        {
            TargetRule.TileSprites.Add(new SpriteResult(1, null));
            _spritesListView.Rebuild();
            var last = TargetRule.TileSprites.Count - 1;
            if (last >= 0)
            {
                _spritesListView.SetSelection(new[] { last });
                _spritesListView.ScrollToItem(last);
            }
        }

        private void OnRemoveClicked()
        {
            var indices = _spritesListView.selectedIndices
                .OrderByDescending(x => x)
                .ToList();

            if (indices.Count == 0) return;

            foreach (var i in indices)
            {
                if (i >= 0 && i < TargetRule.TileSprites.Count)
                    TargetRule.TileSprites.RemoveAt(i);
            }

            _spritesListView.Rebuild();
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

            if (TargetRule != null)
            {
                EditorUtility.SetDirty(TargetRule.RuleGroup);
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
            ref var slot = ref TargetRule.ruleMatrix.GetCurrentMatrix()[cellIndex];
            slot = _selectedIntGridValue.value;

            serializedObject.ApplyModifiedProperties();
            serializedObject.Update();
        }

        private void RightClick(int cellIndex, SerializedObject serializedObject)
        {
            ref var slot = ref TargetRule.ruleMatrix.GetCurrentMatrix()[cellIndex];

            if (slot == 0)
                slot = (short)-_selectedIntGridValue.value;
            else
                slot = 0;

            serializedObject.ApplyModifiedProperties();
            serializedObject.Update();
        }
    }
}