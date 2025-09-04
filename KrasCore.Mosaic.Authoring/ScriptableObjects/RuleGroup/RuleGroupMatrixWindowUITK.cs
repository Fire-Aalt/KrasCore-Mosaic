using System.Reflection;
using KrasCore.Mosaic.Editor;
using UnityEditor;
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
        private SerializedObject _serializedObject;
        
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
            _serializedObject = new SerializedObject(_ruleGroup);
            
            Create();
        }
        
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
                
                var pf = IntGridValueSelectorDrawer.Create(fieldInfo, property);
                box.Add(pf);
                colSelect.Add(box);
            }
            
            var targetRuleProperty = _serializedObject.FindProperty(nameof(RuleGroup.rules)).GetArrayElementAtIndex(_ruleIndex);
            
            // Column 2: Matrix
            {
                var matrixProperty = targetRuleProperty.FindPropertyRelative(nameof(RuleGroup.Rule.ruleMatrix));
                var fieldInfo = TargetRule.GetType().GetField(nameof(RuleGroup.Rule.ruleMatrix));
                
                var matrix = new IntGridMatrixDrawer().Create(this, fieldInfo,
                    new IntGridMatrixAttribute(), matrixProperty);
                colMatrix.Add(matrix);
                
                var dragger = new IntGridDragger
                {
                    DragEnter = (cell, pressed) =>
                    {
                        for (int i = 0; i < cell.parent.childCount; i++)
                        {
                            if (ReferenceEquals(cell.parent[i], cell))
                            {
                                if (pressed == IntGridDragger.Pressed.RightMouseButton)
                                    RightClick(i, _serializedObject);
                                else
                                    LeftClick(i, _serializedObject);
                                break;
                            }
                        }
                    },
                    HoverEnter = (cell) => { cell.AddToClassList("int-grid-matrix-cell-hover"); },
                    HoverLeave = (cell) => { cell.RemoveFromClassList("int-grid-matrix-cell-hover"); },
                    DragStop = () => _rightClickMode = DragMode.None
                };
                matrix.AddManipulator(dragger);
            }
            
            // Column 3: Sprites
            {
                var spritesListView = new ListViewBuilder<Sprite, SpriteResult>("SpritesListView", "Tile Sprites",
                    _ruleGroup, targetRuleProperty, nameof(RuleGroup.Rule.TileSprites), TargetRule.TileSprites,
                    sprite => new SpriteResult(sprite));
                
                colSprites.Add(spritesListView.Root);
            }
            
            // Column 4: Prefabs
            {
                var prefabsListView = new ListViewBuilder<GameObject, PrefabResult>("PrefabsListView", "Tile Entities",
                    _ruleGroup, targetRuleProperty, nameof(RuleGroup.Rule.TileEntities), TargetRule.TileEntities,
                    prefab => new PrefabResult(prefab));
                
                colEntities.Add(prefabsListView.Root);
            }
        }
        
        private enum DragMode
        {
            None,
            Clear,
            Set
        }

        private DragMode _rightClickMode = DragMode.None;
        
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

        private void LeftClick(int cellIndex, SerializedObject serializedObject)
        {
            ref var slot = ref TargetRule.ruleMatrix.GetCurrentMatrix()[cellIndex];

            if (slot != _selectedIntGridValue.value)
            {
                if (slot < 0)
                    slot = 0;
                else
                    slot = _selectedIntGridValue.value;
            }

            serializedObject.Update();
        }

        private void RightClick(int cellIndex, SerializedObject serializedObject)
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

            serializedObject.Update();
        }
    }
}