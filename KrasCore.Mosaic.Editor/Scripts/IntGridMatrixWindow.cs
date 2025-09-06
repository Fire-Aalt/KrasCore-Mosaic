using System.Reflection;
using KrasCore.Mosaic.Authoring;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace KrasCore.Mosaic.Editor
{
    public class IntGridMatrixWindow : EditorWindow
    {
        public static int NumberOfActiveInspectorWindows;

        [SerializeField, HideInInspector] 
        private IntGridValueSelector _selectedIntGridValue;
        
        private SerializedObject _window;
        private SerializedObject _serializedObject;
        
        private RuleGroup _ruleGroup;
        private int _ruleIndex;
        private DragMode _rightClickMode = DragMode.None;
        
        private RuleGroup.Rule TargetRule => _ruleGroup.rules[_ruleIndex];
        
        [InitializeOnLoadMethod]
        private static void RegisterCallbacks()
        {
            RuleGroup.Rule.OnMatrixClicked += OpenWindow;
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

        private static void OpenWindow(RuleGroup.Rule target)
        {
            var wnd = GetWindow<IntGridMatrixWindow>(
                true,
                "Rule Matrix Window",
                true
            );
            wnd.Init(target);
            wnd.Show();
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
            _serializedObject = new SerializedObject(_ruleGroup);
            
            CreateUI();
        }
        
        private void CreateUI()
        {
            var root = rootVisualElement;
            root.Clear();
            
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
                new()
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

            // Column 1: IntGridValue selector
            {
                var box = new GroupBox { name = "IntGridSelectorBox" };
                
                var property = _window.FindProperty(nameof(_selectedIntGridValue));
                var fieldInfo = GetType().GetField(nameof(_selectedIntGridValue),BindingFlags.NonPublic | BindingFlags.Instance);
                
                var intGridSelector = IntGridValueSelectorDrawer.Create(fieldInfo, property);
                box.Add(intGridSelector);
                colSelect.Add(box);
            }
            
            var targetRuleProperty = _serializedObject.FindProperty(nameof(RuleGroup.rules)).GetArrayElementAtIndex(_ruleIndex);
            
            // Column 2: Matrix
            {
                var matrixProperty = targetRuleProperty.FindPropertyRelative(nameof(RuleGroup.Rule.ruleMatrix));
                var fieldInfo = TargetRule.GetType().GetField(nameof(RuleGroup.Rule.ruleMatrix));

                var matrixView = new IntGridMatrixView();
                
                var matrixRoot = matrixView.Create(fieldInfo, new IntGridMatrixAttribute());
                matrixView.Bind(matrixProperty, this);
                
                colMatrix.Add(matrixRoot);
                
                var dragger = new IntGridMatrixManipulator
                {
                    DragEnter = OnDragEnter,
                    HoverEnter = (cell) => { cell.AddToClassList("int-grid-matrix-cell-hover"); },
                    HoverLeave = (cell) => { cell.RemoveFromClassList("int-grid-matrix-cell-hover"); },
                    DragStop = () => _rightClickMode = DragMode.None
                };
                matrixRoot.AddManipulator(dragger);
            }
            
            // Column 3: Sprites
            {
                var tileSpritesSerializedList = targetRuleProperty.FindPropertyRelative(nameof(RuleGroup.Rule.TileSprites));
                
                var spritesListView = new WeightedListViewBuilder<Sprite, SpriteResult>("SpritesListView", "Tile Sprites",
                    _ruleGroup, tileSpritesSerializedList, TargetRule.TileSprites, sprite => new SpriteResult(sprite));
                
                colSprites.Add(spritesListView.Build());
            }
            
            // Column 4: Prefabs
            {
                var tileEntitiesSerializedList = targetRuleProperty.FindPropertyRelative(nameof(RuleGroup.Rule.TileEntities));
                
                var prefabsListView = new WeightedListViewBuilder<GameObject, PrefabResult>("PrefabsListView", "Tile Entities",
                    _ruleGroup, tileEntitiesSerializedList, TargetRule.TileEntities, prefab => new PrefabResult(prefab));
                
                colEntities.Add(prefabsListView.Build());
            }
        }
        
        private void Update()
        {
            foreach (var spriteResult in TargetRule.TileSprites)
            {
                spriteResult.Validate();
            }
            foreach (var entityResult in TargetRule.TileEntities)
            {
                entityResult.Validate();
            }
            
            if (NumberOfActiveInspectorWindows == 0)
            {
                Close();
            }
        }

        private void OnDragEnter(VisualElement cell, IntGridMatrixManipulator.Pressed pressed)
        {
            var hc = cell.parent.childCount;
            for (int i = 0; i < hc; i++)
            {
                if (cell.parent[i] == cell)
                {
                    if (pressed == IntGridMatrixManipulator.Pressed.RightMouseButton)
                        RightClick(i);
                    else
                        LeftClick(i);
                    return;
                }
            }
        }
        
        private void LeftClick(int cellIndex)
        {
            ref var slot = ref TargetRule.ruleMatrix.GetCurrentMatrix()[cellIndex];

            if (slot != _selectedIntGridValue.value)
            {
                if (slot < 0)
                    slot = 0;
                else
                    slot = _selectedIntGridValue.value;
            }

            _serializedObject.Update();
        }

        private void RightClick(int cellIndex)
        {
            ref var slot = ref TargetRule.ruleMatrix.GetCurrentMatrix()[cellIndex];

            if (_rightClickMode == DragMode.None)
            {
                if (slot == 0) _rightClickMode = DragMode.Set;
                else if (slot != 0) _rightClickMode = DragMode.Clear;
            }

            slot = _rightClickMode switch
            {
                DragMode.Set => -_selectedIntGridValue.value,
                DragMode.Clear => 0,
                _ => slot
            };

            _serializedObject.Update();
        }
        
        private enum DragMode
        {
            None,
            Clear,
            Set
        }
    }
}