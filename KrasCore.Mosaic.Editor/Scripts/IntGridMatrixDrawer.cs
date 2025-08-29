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

            var root = new VisualElement { name = "IntGridMatrix_Root" };
            root.style.flexDirection = FlexDirection.Column;
            root.style.marginBottom = 4;
            
            var matrixContainer = new VisualElement { name = "IntGridMatrix_Container" };
            matrixContainer.style.position = Position.Relative;
            matrixContainer.style.flexGrow = 1;
            matrixContainer.style.width = Length.Percent(100);
            matrixContainer.style.borderBottomWidth = 1;
            matrixContainer.style.borderTopWidth = 1;
            matrixContainer.style.borderLeftWidth = 1;
            matrixContainer.style.borderRightWidth = 1;
            matrixContainer.style.borderBottomColor = new Color(0, 0, 0, 0.15f);
            matrixContainer.style.borderTopColor = new Color(0, 0, 0, 0.15f);
            matrixContainer.style.borderLeftColor = new Color(0, 0, 0, 0.15f);
            matrixContainer.style.borderRightColor = new Color(0, 0, 0, 0.15f);

            if (_matrixRectMethod != null)
            {
                _matrixRectMethod.Invoke(owner, new object[] { matrixContainer });
            }
            
            var errorBox = new HelpBox(
                "Matrix cannot be drawn. The collection size must be square.",
                HelpBoxMessageType.Error
            )
            {
                name = "IntGridMatrix_Error"
            };
            errorBox.style.display = DisplayStyle.None;

            // Overlay for readonly
            VisualElement readOnlyOverlay = null;
            if (attr.IsReadonly)
            {
                readOnlyOverlay = new VisualElement { name = "ReadOnlyOverlay" };
                readOnlyOverlay.style.position = Position.Absolute;
                readOnlyOverlay.style.left = 0;
                readOnlyOverlay.style.top = 0;
                readOnlyOverlay.style.right = 0;
                readOnlyOverlay.style.bottom = 0;
                readOnlyOverlay.style.backgroundColor =
                    new Color(0f, 0f, 0f, 0.3f);
                // Block input if readonly
                readOnlyOverlay.pickingMode = PickingMode.Position;
                matrixContainer.Add(readOnlyOverlay);
            }

            // Center icon element
            var centerIcon = new VisualElement { name = "IntGridMatrix_CenterIcon" };
            centerIcon.style.position = Position.Absolute;
            centerIcon.pickingMode = PickingMode.Ignore;
            centerIcon.style.backgroundPositionX = new BackgroundPosition(
                BackgroundPositionKeyword.Center
            );
            centerIcon.style.backgroundPositionY = new BackgroundPosition(
                BackgroundPositionKeyword.Center
            );
            centerIcon.style.backgroundSize = new BackgroundSize(
                BackgroundSizeType.Contain
            );
            if (EditorResources.MatrixCenterTexture != null)
            {
                centerIcon.style.backgroundImage = new StyleBackground(
                    EditorResources.MatrixCenterTexture  as Texture2D
                );
            }

            // Cells container on top of center icon
            var cellsContainer = new VisualElement { name = "CellsContainer" };
            cellsContainer.style.position = Position.Absolute;
            cellsContainer.style.left = 0;
            cellsContainer.style.top = 0;
            cellsContainer.style.right = 0;
            cellsContainer.style.bottom = 0;
            matrixContainer.Add(cellsContainer);
            matrixContainer.Add(centerIcon);

            root.Add(matrixContainer);
            root.Add(errorBox);

            // Build/refresh grid whenever geometry or data changes
            void Refresh()
            {
                // Get the actual IntGridMatrix instance
                var matrixObj = GetFieldValue<IntGridMatrix>(
                    owner,
                    fieldInfo
                );

                if (matrixObj == null)
                {
                    errorBox.text = "Matrix is null.";
                    errorBox.style.display = DisplayStyle.Flex;
                    cellsContainer.Clear();
                    return;
                }

                // Validate square size
                var length = matrixObj.singleGridMatrix.Length;
                var n = (int)Mathf.Sqrt(length);

                if (n * n != length)
                {
                    errorBox.text =
                        "Matrix cannot be drawn. The collection size must be square.";
                    errorBox.style.display = DisplayStyle.Flex;
                    cellsContainer.Clear();
                    return;
                }

                errorBox.style.display = DisplayStyle.None;

                var currentValues = matrixObj.GetCurrentMatrix();
                var currentSize = matrixObj.GetCurrentSize();

                var intGrid = matrixObj.intGrid;
                var useDual = intGrid != null && intGrid.useDualGrid;

                var bounds = matrixContainer.contentRect;
                float W = Mathf.Max(1f, bounds.width);

                // Solve for square size s:
                // N*s + (N-1)*(s*paddingFrac) = W
                // s = W / (N + (N-1)*paddingFrac)
                float paddingFrac = Mathf.Max(0f, attr.Padding);
                float denom = n + (n - 1) * paddingFrac;
                float s = denom > 0.0001f ? W / denom : 1f;
                
                float p = s * paddingFrac;

                // Layout center icon (same size as a cell, centered)
                centerIcon.style.width = s;
                centerIcon.style.height = s;
                centerIcon.style.left = (W - s) * 0.5f;
                centerIcon.style.top = (W - s) * 0.5f;
                centerIcon.style.display =
                    EditorResources.MatrixCenterTexture != null
                        ? DisplayStyle.Flex
                        : DisplayStyle.None;

                // Build or reuse cells
                int cellCount = currentSize * currentSize;
                EnsureChildCount(cellsContainer, cellCount);

                // Draw each cell
                for (int i = 0; i < currentSize; i++)
                {
                    for (int j = 0; j < currentSize; j++)
                    {
                        int idx = i * currentSize + j;
                        var cell = (VisualElement)cellsContainer[idx];
                        cell.name = $"Cell_{i}_{j}";
                        cell.style.position = Position.Absolute;
                        cell.style.width = s;
                        cell.style.height = s;
                        cell.style.backgroundColor =
                            EditorResources.BackgroundCellColor;

                        float x = j * (s + p);
                        float y = i * (s + p);

                        if (useDual)
                        {
                            x -= s * 0.5f;
                            y -= s * 0.5f;
                            x = Mathf.Max(x, 0f);
                            y = Mathf.Max(y, 0f);

                            if ((i == 0 && j == 0) || (i == currentSize - 1 && j == currentSize - 1) ||
                                (i == 0 && j == currentSize - 1) || (i == currentSize - 1 && j == 0))
                            {
                                cell.style.width = s / 2f;
                                cell.style.height = s / 2f;
                            }
                            else if (i == 0 || i == currentSize - 1)
                            {
                                cell.style.height = s / 2f;
                            }
                            else if (j == 0 || j == currentSize - 1)
                            {
                                cell.style.width = s / 2f;
                            }
                        }
                        
                        cell.style.left = x;
                        cell.style.top = y;

                        // Prepare inner icon (child[0]) for textures
                        VisualElement icon;
                        if (cell.childCount == 0)
                        {
                            icon = new VisualElement { name = "Icon" };
                            icon.style.position = Position.Relative;
                            icon.style.alignSelf = Align.Center;
                            icon.style.backgroundSize = new BackgroundSize(
                                BackgroundSizeType.Contain
                            );
                            cell.Add(icon);
                        }
                        else
                        {
                            icon = cell[0];
                        }

                        var ratio = new Vector2(cell.style.width.value.value, cell.style.height.value.value) / s;
                        
                        // 60% pad like original DrawBuiltInCellTexture
                        var inner = s * 0.6f * ratio;
                        icon.style.width = inner.x;
                        icon.style.height = inner.y;
                        icon.style.top = (s * ratio.y - inner.y) / 2f;
                        

                        var slot = new Ptr<IntGridValue>(ref currentValues[idx]);
                        if (_onBeforeDrawCellMethod != null)
                        {
                            _onBeforeDrawCellMethod.Invoke(owner, new object[] { cell, slot });
                        }

                        // Draw based on IntGridValue
                        DrawCell(cell, icon, slot.Ref, intGrid);
                    }
                }

                // Place readonly overlay as last child, so it captures input
                if (readOnlyOverlay != null)
                {
                    readOnlyOverlay.BringToFront();
                }
            }

            // Rebuild on geometry changes (width changes)
            matrixContainer.RegisterCallback<GeometryChangedEvent>(_ => Refresh());

            // Also refresh when data changes (best-effort)
            root.TrackSerializedObjectValue(property.serializedObject, _ => Refresh());

            // Initial
            root.schedule.Execute(Refresh);

            return root;
        }
        
        // ---- Helpers ----

        private static void EnsureChildCount(VisualElement parent, int count)
        {
            int diff = count - parent.childCount;
            if (diff > 0)
            {
                for (int i = 0; i < diff; i++)
                {
                    parent.Add(new VisualElement());
                }
            }
            else if (diff < 0)
            {
                // Remove extra children
                for (int i = parent.childCount - 1; i >= count; i--)
                {
                    parent.RemoveAt(i);
                }
            }
        }

        private void DrawCell(
            VisualElement cell,
            VisualElement icon,
            IntGridValue value,
            IntGridDefinition intGrid
        )
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

            // Clear icon
            icon.style.display = DisplayStyle.None;
            icon.style.backgroundImage = StyleKeyword.None;

            if (Mathf.Abs(value) == RuleGridConsts.AnyIntGridValue)
            {
                // Any cell: paint BG then icon
                cell.style.backgroundColor = EditorResources.BackgroundCellColor;
                icon.style.display = DisplayStyle.Flex;
                icon.style.backgroundImage = new StyleBackground(EditorResources.AnyTexture as Texture2D);
                return;
            }

            if (value == 0)
            {
                // Empty BG only
                return;
            }

            var def = IntGridValueToDefinition(value, intGrid);
            if (def == null)
            {
                // Negative or unknown -> draw "Not" icon + red border
                DrawBuiltInCell(cell, icon, EditorResources.NotTexture, Color.red);
                return;
            }

            if (def.texture == null)
            {
                // Solid color cell
                cell.style.backgroundColor = def.color;
            }
            else
            {
                // Texture over background
                cell.style.backgroundColor = EditorResources.BackgroundCellColor;
                icon.style.display = DisplayStyle.Flex;
                icon.style.backgroundImage = new StyleBackground(def.texture as Texture2D);
            }

            if (value < 0)
            {
                // Negative -> red border + Not icon overlay
                DrawBuiltInCell(cell, icon, EditorResources.NotTexture, Color.red);
            }
        }

        private static void DrawBuiltInCell(
            VisualElement cell,
            VisualElement icon,
            Texture texture,
            Color borderColor
        )
        {
            cell.style.borderBottomWidth = 2;
            cell.style.borderTopWidth = 2;
            cell.style.borderLeftWidth = 2;
            cell.style.borderRightWidth = 2;
            cell.style.borderBottomColor = borderColor;
            cell.style.borderTopColor = borderColor;
            cell.style.borderLeftColor = borderColor;
            cell.style.borderRightColor = borderColor;

            if (texture != null)
            {
                icon.style.backgroundImage = new StyleBackground(texture as Texture2D);
            }
        }
        
        private static IntGridValueDefinition IntGridValueToDefinition(
            IntGridValue v,
            IntGridDefinition intGrid
        )
        {
            if (v == 0 || intGrid == null || intGrid.IntGridValuesDict == null)
                return null;

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