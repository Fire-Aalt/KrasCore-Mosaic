using System;
using System.Collections.Generic;
using System.Reflection;
using KrasCore.Mosaic.Authoring;
using KrasCore.Mosaic.Data;
using Sirenix.OdinInspector.Editor;
using Sirenix.Utilities;
using Sirenix.Utilities.Editor;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;

namespace KrasCore.Mosaic.Editor
{
    public class IntGridMatrixDrawer : OdinAttributeDrawer<IntGridMatrixAttribute, IntGridMatrix>
    {
        private IntGridValue[] MatrixArray => ValueEntry.SmartValue.matrix;
        private IntGridDefinition IntGrid => ValueEntry.SmartValue.intGrid;
        
        private object _targetObject;
        private MethodInfo _matrixRectMethod;
        private MethodInfo _onBeforeDrawCellMethod;
        
        protected override void Initialize()
        {
            base.Initialize();
            
            _targetObject = Property.ParentValues[0];
            
            ReflectionUtils.TryGetCallMethod(_targetObject, Attribute.MatrixRectMethod, out _matrixRectMethod);
            ReflectionUtils.TryGetCallMethod(_targetObject, Attribute.OnBeforeDrawCellMethod, out _onBeforeDrawCellMethod);
        }

        protected override void DrawPropertyLayout(GUIContent label)
        {
            GetProperties(out var matrixWidth, out var matrixSize, out var squareSize, out var padding);
            
            var matrixRect = EditorGUILayout.GetControlRect(GUILayout.Height(matrixWidth));
            matrixRect.size = new Vector2(matrixWidth, matrixWidth);
            
            if (_matrixRectMethod != null)
            {
                _matrixRectMethod.Invoke(_targetObject, new object[] { matrixRect });
            }

            var parameters = new object[2];
            
            // Draw each square
            for (int i = 0; i < matrixSize; i++)
            {
                for (int j = 0; j < matrixSize; j++)
                {
                    int index = i * matrixSize + j;

                    var squareRect = new Rect(
                        matrixRect.x + j * (squareSize + padding),
                        matrixRect.y + i * (squareSize + padding),
                        squareSize,
                        squareSize
                    );

                    if (_onBeforeDrawCellMethod != null)
                    {
                        parameters[0] = squareRect;
                        parameters[1] = MatrixArray[index];
                        MatrixArray[index] = (IntGridValue)_onBeforeDrawCellMethod.Invoke(_targetObject, parameters);
                    }
                }
            }

            if (IntGrid.useDualGrid)
            {
                var values = new List<short>(4); 
                var secondGridRect = matrixRect.Expand(squareSize / 2f);
                
                for (int i = 0; i < matrixSize + 1; i++)
                {
                    for (int j = 0; j < matrixSize + 1; j++)
                    {
                        var squareRect = new Rect(
                            secondGridRect.x + j * (squareSize + padding),
                            secondGridRect.y + i * (squareSize + padding),
                            squareSize,
                            squareSize
                        );
                        squareRect = squareRect.EncapsulateWithin(matrixRect);
                        
                        values.Clear();
                        if (ValidIndex(i - 1, j - 1, out var newIndex) 
                            && Filter(MatrixArray[newIndex].RightTop, out var filteredValue))
                            values.Add(filteredValue);
                        
                        if (ValidIndex(i - 1, j, out newIndex) 
                            && Filter(MatrixArray[newIndex].LeftTop, out filteredValue))
                            values.Add(filteredValue);
                        
                        if (ValidIndex(i, j - 1, out newIndex) 
                            && Filter(MatrixArray[newIndex].RightBottom, out filteredValue))
                            values.Add(filteredValue);
                        
                        if (ValidIndex(i, j, out newIndex) 
                            && Filter(MatrixArray[newIndex].LeftBottom, out filteredValue))
                            values.Add(filteredValue);

                        short initialValue = 0;
                        var allIdentical = true;
                        foreach (var value in values)
                        {
                            if (initialValue == 0)
                            {
                                initialValue = value;
                            }
                            else if (initialValue != value)
                            {
                                allIdentical = false;
                                break;
                            }
                        }

                        if (allIdentical)
                        {
                            DrawIntGridCell(squareRect, initialValue);
                        }
                    }
                }

                bool ValidIndex(int iIndex, int jIndex, out int newIndex)
                {
                    if (iIndex >= 0 && iIndex < matrixSize && jIndex >= 0 && jIndex < matrixSize)
                    {
                        newIndex = iIndex * matrixSize + jIndex;
                        return true;
                    }
                    newIndex = -1;
                    return false;
                }
                
                
                bool Filter(short value, out short filteredValue)
                {
                    if (value != 0)
                    {
                        filteredValue = value;
                        return true;
                    }
                    filteredValue = 0;
                    return false;
                }
            }
            
            // Draw each square
            for (int i = 0; i < matrixSize; i++)
            {
                for (int j = 0; j < matrixSize; j++)
                {
                    int index = i * matrixSize + j;

                    var squareRect = new Rect(
                        matrixRect.x + j * (squareSize + padding),
                        matrixRect.y + i * (squareSize + padding),
                        squareSize,
                        squareSize
                    );
                    
                    
                    DrawMatrixCell(squareRect, index, MatrixArray[index]);
                }
            }
        }

        private void GetProperties(out float matrixWidth, out int matrixSize, out float squareSize, out float padding)
        {
            matrixSize = (int)Mathf.Sqrt(MatrixArray.Length);
            if (matrixSize * matrixSize != MatrixArray.Length)
                throw new Exception("A collection has a non square size. Matrix cannot be drawn");
        
            squareSize = EditorGUILayout.GetControlRect(false, 0f).size.x / matrixSize;
            padding = squareSize * Attribute.Padding;
            matrixWidth = matrixSize * (squareSize + padding);
        }
        
        private void DrawMatrixCell(Rect rect, int index, IntGridValue matrixValue)
        {
            if (!IntGrid.useDualGrid)
            {
                DrawIntGridCell(rect, matrixValue.Solid);
            }
            else
            {
                rect.Subdivide(out var leftBottom, out var rightBottom, out var leftTop, out var rightTop);
                DrawIntGridCell(leftBottom, matrixValue.LeftBottom);
                DrawIntGridCell(rightBottom, matrixValue.RightBottom);
                DrawIntGridCell(leftTop, matrixValue.LeftTop);
                DrawIntGridCell(rightTop, matrixValue.RightTop);
            }
            
            if (index == RuleGroup.Rule.MatrixSizeHalf * RuleGroup.Rule.MatrixSize + RuleGroup.Rule.MatrixSizeHalf)
            {
                EditorGUI.DrawPreviewTexture(rect, EditorHelper.MatrixCenterTexture, EditorHelper.TextureMat, ScaleMode.ScaleToFit);
            }
            
            if (Attribute.IsReadonly)
            {
                EditorGUI.DrawRect(rect, new Color(0f, 0f, 0f, 0.2f));
            }
        }
        
        private void DrawIntGridCell(Rect rect, short slotValue)
        {
            DrawIntGridValue(rect, slotValue);
            if (slotValue < 0)
            {
                DrawBuiltInCellTexture(rect, EditorHelper.NotTexture, Color.red);
            }
        }

        private void DrawIntGridValue(Rect rect, short slotValue)
        {
            if (Mathf.Abs(slotValue) == RuleGridConsts.AnyIntGridValue)
            {
                EditorGUI.DrawRect(rect.Padding(1), EditorHelper.BackgroundCellColor);
                DrawBuiltInCellTexture(rect, EditorHelper.AnyTexture, Color.white);
                return;
            }

            if (slotValue == 0)
            {
                EditorGUI.DrawRect(rect.Padding(1), EditorHelper.BackgroundCellColor);
                return;
            }
            
            var intGridValue = IntGridToIndex(slotValue);
            if (intGridValue.texture == null)
            {
                EditorGUI.DrawRect(rect.Padding(1), intGridValue.color);
            }
            else
            {
                EditorGUI.DrawRect(rect.Padding(1), EditorHelper.BackgroundCellColor);
                EditorGUI.DrawPreviewTexture(rect, intGridValue.texture, EditorHelper.TextureMat, ScaleMode.ScaleToFit);
            }
        }

        private void DrawBuiltInCellTexture(Rect rect, Texture texture, Color borderColor)
        {
            SirenixEditorGUI.DrawBorders(rect.Padding(4f), 2, 2, 2, 2, borderColor);
            var size = rect.size;
            rect.size *= 0.6f;
            rect.position += (size - rect.size) * 0.5f;
            EditorGUI.DrawPreviewTexture(rect, texture, EditorHelper.TextureMat, ScaleMode.ScaleToFit);
        }

        private IntGridValueDefinition IntGridToIndex(short intGridValue)
        {
            return intGridValue != 0 ? IntGrid.IntGridValuesDict[Mathf.Abs(intGridValue)] : null;
        }
    }
}
