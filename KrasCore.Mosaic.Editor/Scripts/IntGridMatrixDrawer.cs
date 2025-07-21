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
        private IntGridMatrix Matrix => ValueEntry.SmartValue;
        private IntGridDefinition IntGrid => Matrix.intGrid;
        
        private object _targetObject;
        private MethodInfo _matrixRectMethod;
        private MethodInfo _onBeforeDrawCellMethod;
        
        private int _singleGridMatrixSize;
        private float _matrixSizePixels;
        private float _squareSizePixels;
        private float _paddingPixels;
        
        private IntGridValue[] _currentGridMatrix;
        private int _currentGridMatrixSize;
        
        protected override void Initialize()
        {
            base.Initialize();
            
            _targetObject = Property.ParentValues[0];
            
            ReflectionUtils.TryGetCallMethod(_targetObject, Attribute.MatrixRectMethod, out _matrixRectMethod);
            ReflectionUtils.TryGetCallMethod(_targetObject, Attribute.OnBeforeDrawCellMethod, out _onBeforeDrawCellMethod);
        }

        protected override void DrawPropertyLayout(GUIContent label)
        {
            SetProperties();
            
            var matrixRect = EditorGUILayout.GetControlRect(false, _matrixSizePixels);
            matrixRect.size = new Vector2(_matrixSizePixels, _matrixSizePixels);
            
            if (_matrixRectMethod != null)
            {
                _matrixRectMethod.Invoke(_targetObject, new object[] { matrixRect });
            }

            var parameters = new object[2];

            var currentMatrixRect = IntGrid.useDualGrid
                ? matrixRect.Expand(_squareSizePixels / 2f)
                : matrixRect;
            
            for (int i = 0; i < _currentGridMatrixSize; i++)
            {
                for (int j = 0; j < _currentGridMatrixSize; j++)
                {
                    var index = i * _currentGridMatrixSize + j;

                    var squareRect = new Rect(
                        currentMatrixRect.x + j * (_squareSizePixels + _paddingPixels),
                        currentMatrixRect.y + i * (_squareSizePixels + _paddingPixels),
                        _squareSizePixels,
                        _squareSizePixels
                    );

                    if (IntGrid.useDualGrid)
                    {
                        squareRect = squareRect.EncapsulateWithin(matrixRect);
                    }

                    if (_onBeforeDrawCellMethod != null)
                    {
                        parameters[0] = squareRect;
                        parameters[1] = _currentGridMatrix[index];
                        _currentGridMatrix[index] = (IntGridValue)_onBeforeDrawCellMethod.Invoke(_targetObject, parameters);
                    }

                    DrawMatrixCell(squareRect, _currentGridMatrix[index]);
                }
            }

            var centerSquare = matrixRect.Padding(_matrixSizePixels / 2f - _squareSizePixels / 2f);
            EditorGUI.DrawPreviewTexture(centerSquare, EditorHelper.MatrixCenterTexture, EditorHelper.TextureMat, ScaleMode.ScaleToFit);
            
            if (Attribute.IsReadonly)
            {
                EditorGUI.DrawRect(matrixRect, new Color(0f, 0f, 0f, 0.3f));
            }
        }

        private void SetProperties()
        {
            _singleGridMatrixSize = (int)Mathf.Sqrt(Matrix.singleGridMatrix.Length);
            if (_singleGridMatrixSize * _singleGridMatrixSize != Matrix.singleGridMatrix.Length)
                throw new Exception("A collection has a non square size. Matrix cannot be drawn");
        
            _squareSizePixels = EditorGUILayout.GetControlRect(false, 0f).size.x / _singleGridMatrixSize;
            _paddingPixels = _squareSizePixels * Attribute.Padding;
            _matrixSizePixels = _singleGridMatrixSize * (_squareSizePixels + _paddingPixels);

            _currentGridMatrix = Matrix.GetCurrentMatrix();
            _currentGridMatrixSize = Matrix.GetCurrentSize();
        }
        
        private void DrawMatrixCell(Rect rect, IntGridValue matrixValue)
        {
            DrawIntGridValue(rect, matrixValue);
            if (matrixValue < 0)
            {
                DrawBuiltInCellTexture(rect, EditorHelper.NotTexture, Color.red);
            }
        }

        private void DrawIntGridValue(Rect rect, IntGridValue value)
        {
            if (Mathf.Abs(value) == RuleGridConsts.AnyIntGridValue)
            {
                EditorGUI.DrawRect(rect.Padding(1), EditorHelper.BackgroundCellColor);
                DrawBuiltInCellTexture(rect, EditorHelper.AnyTexture, Color.white);
                return;
            }

            if (value == 0)
            {
                EditorGUI.DrawRect(rect.Padding(1), EditorHelper.BackgroundCellColor);
                return;
            }
            
            var intGridValue = IntGridToIndex(value);
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

        private IntGridValueDefinition IntGridToIndex(IntGridValue intGridValue)
        {
            return intGridValue != 0 ? IntGrid.IntGridValuesDict[Mathf.Abs(intGridValue)] : null;
        }
    }
}
