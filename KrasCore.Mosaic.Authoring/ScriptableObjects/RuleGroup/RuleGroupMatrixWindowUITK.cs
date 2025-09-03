using System;
using System.Collections.Generic;
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
                
                _matrix = new IntGridMatrixDrawer().Create(this, fieldInfo,
                    new IntGridMatrixAttribute(nameof(OnBeforeDrawMatrixCell)), matrixProperty);
                colMatrix.Add(_matrix);


                var e = new ExampleDragger();
                e.DragEnter = (cell, rightButton) =>
                {
                    for (int i = 0; i < cell.parent.childCount; i++)
                    {
                        if (ReferenceEquals(cell.parent[i], cell))
                        {
                            if (rightButton)
                                RightClick(i, _serializedObject);
                            else
                                LeftClick(i, _serializedObject);
                            break;
                        }
                    }
                };
                _matrix.AddManipulator(e);
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
        private VisualElement _matrix;
        
        
        private void OnBeforeDrawMatrixCell(VisualElement cell, int cellIndex, SerializedObject serializedObject)
        {
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
    
    public class ExampleDragger : PointerManipulator
    {
        protected bool m_Active;
        private int m_PointerId;
        private VisualElement currentDragHover;
        private VisualElement currentHover;

        private int _pressedButton;
        
        public Action<VisualElement> HoverEnter;
        public Action<VisualElement> HoverLeave;
        public Action<VisualElement, bool> DragEnter;
        public Action<VisualElement> DragLeave;
        
        public ExampleDragger()
        {
            m_PointerId = -1;
            activators.Add(new ManipulatorActivationFilter { button = MouseButton.LeftMouse });
            activators.Add(new ManipulatorActivationFilter { button = MouseButton.RightMouse });
            m_Active = false;
        }

        protected override void RegisterCallbacksOnTarget()
        {
            target.RegisterCallback<PointerDownEvent>(OnPointerDown, TrickleDown.TrickleDown);
            target.RegisterCallback<PointerUpEvent>(OnPointerUp, TrickleDown.TrickleDown);
            target.RegisterCallback<PointerEnterEvent>(OnPointerEnter, TrickleDown.TrickleDown);
            target.RegisterCallback<PointerLeaveEvent>(OnPointerLeave, TrickleDown.TrickleDown);
        }

        protected override void UnregisterCallbacksFromTarget()
        {
            target.UnregisterCallback<PointerDownEvent>(OnPointerDown);
            target.UnregisterCallback<PointerUpEvent>(OnPointerUp);
            target.UnregisterCallback<PointerEnterEvent>(OnPointerEnter);
            target.UnregisterCallback<PointerLeaveEvent>(OnPointerLeave);
        }
        
        private void OnPointerDown(PointerDownEvent e)
        {
            if (m_Active)
            {
                e.StopImmediatePropagation();
                return;
            }

            if (CanStartManipulation(e))
            {
                m_PointerId = e.pointerId;
                _pressedButton = e.button;

                m_Active = true;
                target.CapturePointer(m_PointerId);
                e.StopPropagation();
            }
        }
        
        private void OnPointerEnter(PointerEnterEvent e)
        {
            var target = FindTarget(e.target as VisualElement);
            if (target != null && m_Active)
            {
                if (!ReferenceEquals(target, currentDragHover))
                {
                    DragEnter?.Invoke(target, _pressedButton == 1);
                    currentDragHover = target;
                }
                else if (!ReferenceEquals(target, currentHover))
                {
                    HoverEnter?.Invoke(target);
                    currentHover = target;
                }
            }
            
            e.StopPropagation();
        }
        
        private void OnPointerLeave(PointerLeaveEvent e)
        {
            var target = FindTarget(e.target as VisualElement);
            if (target != null && m_Active)
            {
                if (ReferenceEquals(target, currentDragHover))
                {
                    DragLeave?.Invoke(target);
                    currentDragHover = null;
                }
                else if (ReferenceEquals(target, currentHover))
                {
                    HoverLeave?.Invoke(target);
                    currentHover = null;
                }
            }
            
            e.StopPropagation();
        }

        private void OnPointerUp(PointerUpEvent e)
        {
            if (!m_Active || !target.HasPointerCapture(m_PointerId) || !CanStopManipulation(e))
                return;

            _pressedButton = -1;
            m_Active = false;
            target.ReleaseMouse();
            e.StopPropagation();
        }
        
        private VisualElement FindTarget(VisualElement ve)
        {
            for (var cur = ve; cur != null; cur = cur.hierarchy.parent)
            {
                if (cur.ClassListContains("int-grid-matrix-cell"))
                    return cur;
            }
            return null;
        }
    }
}