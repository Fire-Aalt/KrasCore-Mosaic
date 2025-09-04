using System;
using System.Collections.Generic;
using UnityEngine.UIElements;

namespace KrasCore.Mosaic.Authoring
{
    public class IntGridDragger : PointerManipulator
    {
        private bool _active;
        private int _pointerId;
        private VisualElement _currentDragHover;
        private VisualElement _currentHover;

        private int _pressedButton;
        
        public Action<VisualElement> HoverEnter;
        public Action<VisualElement> HoverLeave;
        public Action<VisualElement, bool> DragEnter;
        public Action<VisualElement> DragLeave;
        public Action DragStop;

        private readonly HashSet<VisualElement> _visitedSet = new();
        
        public IntGridDragger()
        {
            _pointerId = -1;
            activators.Add(new ManipulatorActivationFilter { button = MouseButton.LeftMouse });
            activators.Add(new ManipulatorActivationFilter { button = MouseButton.RightMouse });
            _active = false;
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
            if (_active)
            {
                e.StopImmediatePropagation();
                return;
            }

            if (CanStartManipulation(e))
            {
                _pointerId = e.pointerId;
                _pressedButton = e.button;

                _active = true;
                target.CapturePointer(_pointerId);
                
                TryEnter(e.target);
                
                e.StopPropagation();
            }
        }
        
        private void OnPointerUp(PointerUpEvent e)
        {
            if (!_active || !target.HasPointerCapture(_pointerId) || !CanStopManipulation(e))
                return;

            _visitedSet.Clear();
            _pressedButton = -1;
            _active = false;
            target.ReleaseMouse();
            e.StopPropagation();
            DragStop?.Invoke();
        }
        
        private void OnPointerEnter(PointerEnterEvent e)
        {
            TryEnter(e.target);

            e.StopPropagation();
        }

        private void TryEnter(IEventHandler eTarget)
        {
            var selected = FindTarget(eTarget as VisualElement);
            if (selected != null && _active)
            {
                if (!_visitedSet.Contains(selected))
                {
                    DragEnter?.Invoke(selected, _pressedButton == 1);
                    _currentDragHover = selected;
                    _visitedSet.Add(selected);
                }
                else if (!ReferenceEquals(selected, _currentHover))
                {
                    HoverEnter?.Invoke(selected);
                    _currentHover = selected;
                }
            }
        }

        private void OnPointerLeave(PointerLeaveEvent e)
        {
            var deseleted = FindTarget(e.target as VisualElement);
            if (deseleted != null && _active)
            {
                if (ReferenceEquals(deseleted, _currentDragHover))
                {
                    DragLeave?.Invoke(deseleted);
                    _currentDragHover = null;
                }
                else if (ReferenceEquals(deseleted, _currentHover))
                {
                    HoverLeave?.Invoke(deseleted);
                    _currentHover = null;
                }
            }
            
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