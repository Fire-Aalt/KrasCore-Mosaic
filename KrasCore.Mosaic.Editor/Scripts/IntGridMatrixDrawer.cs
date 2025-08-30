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
    public class IntGridMatrixUITKDrawer : PropertyDrawer
    {
        // State per drawer instance
        private MethodInfo _matrixRectMethod;
        private MethodInfo _onBeforeDrawCellMethod;
        
        // UI Toolkit path
        public override VisualElement CreatePropertyGUI(SerializedProperty property)
        {
            var attr = (IntGridMatrixAttribute)attribute;

            // Target owner object: the object that contains the field
            var owner = GetParentObject(property);

            ReflectionUtils.TryGetCallMethod(owner, attr.MatrixRectMethod, out _matrixRectMethod);
            ReflectionUtils.TryGetCallMethod(owner, attr.OnBeforeDrawCellMethod, out _onBeforeDrawCellMethod);

            var root = new VisualElement
            {
                name = "IntGridMatrix_Root",
                style =
                {
                    position = Position.Relative,
                    display = DisplayStyle.Flex,
                    flexGrow = 1,
                    width = Length.Percent(100),
                }
            };

            var matrixContainer = new VisualElement
            {
                name = "IntGridMatrix_Container",
                style =
                {
                    position = Position.Relative,
                    display = DisplayStyle.Flex,
                    borderBottomWidth = 5,
                    borderTopWidth = 5,
                    borderLeftWidth = 5,
                    borderRightWidth = 5,
                    borderBottomColor = new Color(0, 0, 0, 0.15f),
                    borderTopColor = new Color(0, 0, 0, 0.15f),
                    borderLeftColor = new Color(0, 0, 0, 0.15f),
                    borderRightColor = new Color(0, 0, 0, 0.15f)
                }
            };

            if (_matrixRectMethod != null)
            {
                _matrixRectMethod.Invoke(owner, new object[] { matrixContainer });
            }
            
            // Center icon element
            var centerIcon = new VisualElement
            {
                name = "IntGridMatrix_CenterIcon",
                pickingMode = PickingMode.Ignore,
                style =
                {
                    position = Position.Absolute,
                    display = DisplayStyle.Flex,
                    backgroundSize = new BackgroundSize(BackgroundSizeType.Contain),
                    backgroundImage = new StyleBackground(EditorResources.MatrixCenterTexture as Texture2D)
                }
            };

            // Cells container on top of center icon
            var cellsContainer = new VisualElement
            {
                name = "CellsContainer",
                style =
                {
                    position = Position.Absolute,
                }
            };
            matrixContainer.Add(cellsContainer);
            matrixContainer.Add(centerIcon);

            // Overlay for readonly
            VisualElement readOnlyOverlay = null;
            if (attr.IsReadonly)
            {
                readOnlyOverlay = new VisualElement
                {
                    name = "ReadOnlyOverlay",
                    style =
                    {
                        position = Position.Absolute,
                        display = DisplayStyle.Flex,
                        backgroundColor = new Color(0f, 0f, 0f, 0.3f)
                    }
                };
                matrixContainer.Add(readOnlyOverlay);
            }
            
            root.Add(matrixContainer);
            
            // Build/refresh grid whenever geometry or data changes
            void Refresh()
            {
                var matrixObj = GetFieldValue<IntGridMatrix>(owner, fieldInfo);

                if (matrixObj == null)
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
                
                size -= 5 * 2;
                if (readOnlyOverlay != null)
                {
                    readOnlyOverlay.style.width = size;
                    readOnlyOverlay.style.height = size;
                }
                
                var padding = size * attr.Padding;
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
                        
                        var cellIcon = cell[0];
                        cellIcon.style.width = finalCellSize.x;
                        cellIcon.style.height = finalCellSize.y;
                        
                        var notIcon = cell[1];
                        notIcon.style.width = finalCellSize.x;
                        notIcon.style.height = finalCellSize.y;
                        
                        cell.style.left = x;
                        cell.style.top = y;

                        var slot = new Ptr<IntGridValue>(ref currentValues[cellIndex]);
                        if (_onBeforeDrawCellMethod != null)
                        {
                            _onBeforeDrawCellMethod.Invoke(owner, new object[] { cell, slot });
                        }

                        // Draw based on IntGridValue
                        DrawCell(cell, slot.Ref, intGrid);
                    }
                }
            }

            // Rebuild on geometry changes (width changes)
            root.RegisterCallback<GeometryChangedEvent>(_ => Refresh());

            // Also refresh when data changes (best-effort)
            root.TrackSerializedObjectValue(property.serializedObject, _ => Refresh());

            // Initial
            Refresh();

            return root;
        }
        
        // ---- Helpers ----
        private static void EnsureCellsCount(VisualElement cellsMatrix, int cellsCount)
        {
            if (cellsMatrix.childCount == cellsCount)
            {
                return;
            }
            
            cellsMatrix.Clear();
            
            for (int i = 0; i < cellsCount; i++)
            {
                var cell = new VisualElement
                {
                    name = $"Cell_{i}",
                    style =
                    {
                        position = Position.Absolute,
                        backgroundColor = EditorResources.BackgroundCellColor
                    }
                };
                var cellIcon = new VisualElement
                {
                    name = "CellIcon",
                    style =
                    {
                        position = Position.Absolute,
                        alignSelf = Align.Center,
                        backgroundSize = new BackgroundSize(BackgroundSizeType.Contain),
                    }
                };
                var notIcon = new VisualElement
                {
                    name = "NotIcon",
                    style =
                    {
                        position = Position.Absolute,
                        alignSelf = Align.Center,
                        backgroundSize = new BackgroundSize(BackgroundSizeType.Contain),
                    }
                };
                cell.Add(cellIcon);
                cell.Add(notIcon);
                cellsMatrix.Add(cell);
            }
        }

        private void DrawCell(VisualElement cell, IntGridValue value, IntGridDefinition intGrid)
        {
            // Reset visuals
            cell.style.borderBottomWidth = 0;
            cell.style.borderTopWidth = 0;
            cell.style.borderLeftWidth = 0;
            cell.style.borderRightWidth = 0;
            cell.style.borderBottomColor = Color.clear;
            cell.style.borderTopColor = Color.clear;
            cell.style.borderLeftColor = Color.clear;
            cell.style.borderRightColor = Color.clear;

            // Default background
            cell.style.backgroundColor = EditorResources.BackgroundCellColor;

            var cellIcon = cell[0];
            var notIcon = cell[1];
            
            // Clear icon
            cellIcon.style.display = DisplayStyle.None;
            cellIcon.style.backgroundImage = StyleKeyword.None;

            notIcon.style.display = DisplayStyle.None;
            notIcon.style.backgroundImage = StyleKeyword.None;
            
            if (value == 0)
            {
                return;
            }
            
            if (Mathf.Abs(value) == RuleGridConsts.AnyIntGridValue)
            {
                DrawBuiltInCell(cell, cellIcon, EditorResources.AnyTexture, Color.white);
            }
            else
            {
                var def = IntGridValueToDefinition(value, intGrid);
                if (def != null)
                {
                    if (def.texture == null)
                    {
                        // Solid color cell
                        cell.style.backgroundColor = def.color;
                    }
                    else
                    {
                        // Texture over background
                        DrawBuiltInCell(cell, cellIcon, def.texture, def.color);
                    }
                }
            }
            
            if (value < 0)
            {
                // Negative or unknown -> draw "Not" icon + red border
                DrawBuiltInCell(cell, notIcon, EditorResources.NotTexture, Color.red);
            }
        }

        private static void DrawBuiltInCell(VisualElement cell, VisualElement icon, Texture texture, Color borderColor)
        {
            cell.style.borderBottomWidth = 2;
            cell.style.borderTopWidth = 2;
            cell.style.borderLeftWidth = 2;
            cell.style.borderRightWidth = 2;
            cell.style.borderBottomColor = borderColor;
            cell.style.borderTopColor = borderColor;
            cell.style.borderLeftColor = borderColor;
            cell.style.borderRightColor = borderColor;

            icon.style.top = -2;
            
            icon.style.display = DisplayStyle.Flex;
            icon.style.backgroundImage = new StyleBackground(texture as Texture2D);
        }
        
        private static IntGridValueDefinition IntGridValueToDefinition(IntGridValue v, IntGridDefinition intGrid)
        {
            var key = Mathf.Abs(v);
            intGrid.IntGridValuesDict.TryGetValue(key, out var def);
            return def;
        }

        private static T GetFieldValue<T>(object owner, FieldInfo fi) where T : class
        {
            if (owner == null || fi == null) return null;

            try
            {
                var val = fi.GetValue(owner);
                return val as T;
            }
            catch
            {
                return null;
            }
        }

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
    }
}