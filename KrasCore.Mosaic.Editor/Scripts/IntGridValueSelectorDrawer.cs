using System;
using KrasCore.Mosaic.Authoring;
using KrasCore.Mosaic.Data;
using Sirenix.OdinInspector.Editor;
using Sirenix.Utilities.Editor;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace KrasCore.Mosaic.Editor
{
    [CustomPropertyDrawer(typeof(IntGridValueSelectorDrawerAttribute))]
    public class IntGridValueSelectorDrawer : PropertyDrawer
    {
        public StyleSheet StyleSheet;
        
        private static object GetParentObject(SerializedProperty property)
        {
            var path = property.propertyPath;
            var i = path.LastIndexOf('.');
            
            if (i < 0)
            {
                return property.serializedObject.targetObject;
            }
            
            var parent = property.serializedObject.FindProperty(path.Substring(0, i));
            return parent.boxedValue;
        }
        
        public override VisualElement CreatePropertyGUI(SerializedProperty property)
        {
            var owner = GetParentObject(property);
            
            var root = new VisualElement { name = "IntGridValueSelector_Root" };
            root.styleSheets.Add(StyleSheet);

            var selectorContainer = new VisualElement { name = "IntGridValueSelector_Container" };
            
            root.Add(selectorContainer);
            
            // Build/refresh grid whenever geometry or data changes
            void Refresh()
            {
                if (fieldInfo.GetValue(owner) is not IntGridValueSelector selectorObj)
                {
                    throw new Exception("Selector is null");
                }
                var intGrid = selectorObj.intGrid;
                
                var size = root.contentRect.width;
                
                selectorContainer.style.width = size;
                selectorContainer.style.height = size;
                
                EnsureCellsCount(selectorContainer, intGrid);

                for (int i = 0; i < intGrid.intGridValues.Count; i++)
                {
                    var val =  intGrid.intGridValues[i];
                    CreateButton(selectorContainer, i, val.texture, val.color, val.name, selectorObj);
                }
                
                CreateButton(selectorContainer, intGrid.intGridValues.Count, EditorResources.AnyTexture, Color.white, "Any Value/No Value", selectorObj);
            }

            // Rebuild on geometry changes (width changes)
            root.RegisterCallback<GeometryChangedEvent>(_ => Refresh());

            // Also refresh when data changes (best-effort)
            root.TrackSerializedObjectValue(property.serializedObject, _ => Refresh());

            // Initial
            Refresh();

            return root;
        }

        private void CreateButton(VisualElement root, int i, Texture texture, Color color, string name, IntGridValueSelector selectorObj)
        {
            var button = root[i];
            button.ClearBindings();
                    
            var icon = button[0];
            var text = button[1] as Label;

            icon.style.backgroundImage = StyleKeyword.None;
            icon.style.backgroundColor = StyleKeyword.None;
            text.text = "";
                    
            if (texture != null)
            {
                icon.style.backgroundImage = texture as Texture2D;
            }
            else
            {
                icon.style.backgroundColor = color;
            }
            text.text = name;
            
                    
            button.RegisterCallback<ClickEvent, ClickData>(Clicked, new ClickData()
            {
                Index = i,
                Root = root,
                Selector = selectorObj
            });
        }

        private struct ClickData
        {
            public int Index;
            public VisualElement Root;
            public IntGridValueSelector Selector;
        }
        
        private void Clicked(ClickEvent clickEvent, ClickData clickData)
        {
            for (int i = 0; i < clickData.Root.childCount; i++)
            {
                clickData.Root[i].style.backgroundColor = Color.clear;
            }

            var values = clickData.Selector.intGrid.intGridValues;
            
            clickData.Root[clickData.Index].style.backgroundColor = Color.cadetBlue;
            
            clickData.Selector.value = clickData.Index == values.Count
                ? RuleGridConsts.AnyIntGridValue
                : values[clickData.Index].value;
        }
        
        private static void EnsureCellsCount(VisualElement cellsMatrix, IntGridDefinition cellsCount)
        {
            var buttonsCount = cellsCount.intGridValues.Count + 1;
            if (cellsMatrix.childCount == buttonsCount)
            {
                return;
            }
            
            cellsMatrix.Clear();
            
            for (int i = 0; i < buttonsCount; i++)
            {
                var button = new VisualElement { name = $"Button_{i}" };
                var icon = new VisualElement { name = "Icon" };
                var text = new Label { name = "Text" };
                
                button.Add(icon);
                button.Add(text);
                cellsMatrix.Add(button);
            }
        }
    }
}