using System;
using System.Reflection;
using BovineLabs.Core.Utility;
using KrasCore.Mosaic.Authoring;
using KrasCore.Mosaic.Data;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace KrasCore.Mosaic.Editor
{
    [CustomPropertyDrawer(typeof(IntGridMatrixAttribute))]
    public class IntGridMatrixDrawer : PropertyDrawer
    {
        public VisualElement Create(object methodsOwner, FieldInfo fieldInf, IntGridMatrixAttribute attr, SerializedProperty property)
        {
            ReflectionUtils.TryGetCallMethod(methodsOwner, attr.MatrixRectMethod, out var matrixRectMethod);

            var root = new VisualElement { name = "IntGridMatrix_Root" };
            root.styleSheets.Add(EditorResources.StyleSheet);

            var matrixContainer = new VisualElement { name = "IntGridMatrix_Container" };

            if (matrixRectMethod != null)
            {
                matrixRectMethod.Invoke(methodsOwner, new object[] { matrixContainer });
            }
            
            var centerIcon = new VisualElement
            {
                name = "IntGridMatrix_CenterIcon",
                pickingMode = PickingMode.Ignore,
                style =
                {
                    backgroundImage = new StyleBackground(EditorResources.MatrixCenterTexture as Texture2D)
                }
            };

            var cellsContainer = new VisualElement { name = "CellsContainer" };
            matrixContainer.Add(cellsContainer);
            matrixContainer.Add(centerIcon);

            VisualElement readOnlyOverlay = null;
            if (attr.IsReadonly)
            {
                readOnlyOverlay = new VisualElement { name = "ReadOnlyOverlay" };
                matrixContainer.Add(readOnlyOverlay);
            }
            
            root.Add(matrixContainer);
            
            // Build/refresh grid whenever geometry or data changes
            void Refresh()
            {
                var targetObject = SerializationUtils.GetParentObject(property);
                if (fieldInf.GetValue(targetObject) is not IntGridMatrix matrixObj)
                {
                    throw new Exception("Matrix is null");
                }
                
                var length = matrixObj.singleGridMatrix.Length;
                var n = (int)Mathf.Sqrt(length);

                if (n * n != length)
                {
                    throw new Exception("Matrix cannot be drawn. The collection size must be square.");
                }

                var currentValues = matrixObj.GetCurrentMatrix();
                var currentSize = matrixObj.GetCurrentSize();
                var intGrid = matrixObj.intGrid;
                
                var size = root.contentRect.width;
                
                matrixContainer.style.width = size;
                matrixContainer.style.height = size;

                size = matrixContainer.contentRect.width;
                
                if (readOnlyOverlay != null)
                {
                    readOnlyOverlay.style.width = size;
                    readOnlyOverlay.style.height = size;
                }
                
                var padding = Mathf.Max(1, size * attr.Padding);
                var paddingCount = intGrid.useDualGrid ? n : n - 1;
                var sizeNoPadding = size - padding * paddingCount; 
                var cellSize = sizeNoPadding / n;

                centerIcon.style.width = cellSize;
                centerIcon.style.height = cellSize;
                centerIcon.style.left = (size - cellSize) * 0.5f;
                centerIcon.style.top = (size - cellSize) * 0.5f;

                var cellCount = currentSize * currentSize;
                EnsureCellsCount(cellsContainer, cellCount);

                // Draw each cell
                for (int i = 0; i < currentSize; i++)
                {
                    for (int j = 0; j < currentSize; j++)
                    {
                        var cellIndex = i * currentSize + j;
                        var cell = cellsContainer[cellIndex];
                        
                        var x = j * (cellSize + padding);
                        var y = i * (cellSize + padding);

                        var finalCellSize = new Vector2(cellSize, cellSize);
                        
                        if (intGrid.useDualGrid)
                        {
                            x -= cellSize * 0.5f;
                            y -= cellSize * 0.5f;
                            x = Mathf.Max(x, 0f);
                            y = Mathf.Max(y, 0f);

                            if ((i == 0 && j == 0) || (i == currentSize - 1 && j == currentSize - 1) ||
                                (i == 0 && j == currentSize - 1) || (i == currentSize - 1 && j == 0))
                            {
                                finalCellSize.x /= 2f;
                                finalCellSize.y /= 2f;
                            }
                            else if (i == 0 || i == currentSize - 1)
                            {
                                finalCellSize.y /= 2f;
                            }
                            else if (j == 0 || j == currentSize - 1)
                            {
                                finalCellSize.x /= 2f;
                            }
                        }
                        
                        cell.style.width = finalCellSize.x;
                        cell.style.height = finalCellSize.y;
                        cell.style.left = x;
                        cell.style.top = y;

                        DrawCell(cell, currentValues[cellIndex], intGrid, size);
                    }
                }
            }

            root.RegisterCallback<GeometryChangedEvent>(_ => Refresh());
            root.TrackPropertyValue(property, _ => Refresh());
            Refresh();

            return root;
        }
        
        public override VisualElement CreatePropertyGUI(SerializedProperty property)
        {
            var attr = (IntGridMatrixAttribute)attribute;
            var owner = SerializationUtils.GetParentObject(property);
            
            return Create(owner, fieldInfo, attr, property);
        }
        
        private static void EnsureCellsCount(VisualElement cellsMatrix, int cellsCount)
        {
            if (cellsMatrix.childCount == cellsCount)
            {
                return;
            }
            
            cellsMatrix.Clear();
            
            for (int i = 0; i < cellsCount; i++)
            {
                var cell = new VisualElement { name = $"Cell_{i}" };
                cell.AddToClassList("int-grid-matrix-cell");
                cellsMatrix.Add(cell);
                
                var cellIcon = new VisualElement { name = "CellIcon" };
                cellIcon.AddToClassList("int-grid-matrix-cell-icon");
                cell.Add(cellIcon);
                
                var notIcon = new VisualElement { name = "NotIcon" };
                notIcon.AddToClassList("int-grid-matrix-cell-icon");
                cell.Add(notIcon);
            }
        }

        private void DrawCell(VisualElement cell, IntGridValue value, IntGridDefinition intGrid, float size)
        {
            var cellIcon = cell[0];
            var notIcon = cell[1];
            
            cellIcon.style.display = DisplayStyle.None;
            notIcon.style.display = DisplayStyle.None;
            
            if (value == 0)
            {
                return;
            }
            
            if (Mathf.Abs(value) == RuleGridConsts.AnyIntGridValue)
            {
                DrawIconWithBorders(cellIcon, EditorResources.AnyTexture, Color.white, size);
            }
            else
            {
                var def = IntGridValueToDefinition(value, intGrid);
                if (def != null)
                {
                    if (def.texture == null)
                    {
                        DrawCellColor(cellIcon, def.color, size);
                    }
                    else
                    {
                        DrawIconWithBorders(cellIcon, def.texture, def.color, size);
                    }
                }
            }
            
            if (value < 0)
            {
                DrawIconWithBorders(notIcon, EditorResources.NotTexture, Color.red, size);
            }
        }

        private static void DrawCellColor(VisualElement icon, Color color, float size)
        {
            DrawIconWithBorders(icon, null, color, size);
            icon.style.backgroundColor = color;
        }

        private static void DrawIconWithBorders(VisualElement icon, Texture texture, Color borderColor, float size)
        {
            var borderSize = size * 0.005f;

            icon.style.backgroundColor = Color.clear;

            icon.style.borderBottomWidth = borderSize;
            icon.style.borderTopWidth = borderSize;
            icon.style.borderLeftWidth = borderSize;
            icon.style.borderRightWidth = borderSize;
            icon.style.borderBottomColor = borderColor;
            icon.style.borderTopColor = borderColor;
            icon.style.borderLeftColor = borderColor;
            icon.style.borderRightColor = borderColor;

            icon.style.display = DisplayStyle.Flex;
            
            icon.style.backgroundImage = texture != null 
                ? new StyleBackground(texture as Texture2D) 
                : StyleKeyword.None;
        }
        
        private static IntGridValueDefinition IntGridValueToDefinition(IntGridValue v, IntGridDefinition intGrid)
        {
            var key = Mathf.Abs(v);
            intGrid.IntGridValuesDict.TryGetValue(key, out var def);
            return def;
        }
    }
}