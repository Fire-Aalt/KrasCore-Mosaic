using System;
using System.Collections;
using System.Reflection;
using KrasCore.Mosaic;
using Sirenix.OdinInspector.Editor;
using UnityEditor;
using UnityEngine;

public class MatrixDrawer<T> : OdinAttributeDrawer<MatrixAttribute, T>
{
    protected override void DrawPropertyLayout(GUIContent label)
    {
        if (ValueEntry.WeakSmartValue is not IList collection)
        {
            EditorGUI.LabelField(EditorGUILayout.GetControlRect(), "Matrix attribute is only supported for arrays or lists.");
            return;
        }
        var elementType = collection[0].GetType();
        var targetObject = Property.ParentValues[0];

        GetProperties(collection, out var matrixWidth, out var matrixSize, out var squareSize, out var padding);
        
        var matrixRect = EditorGUILayout.GetControlRect(GUILayout.Height(matrixWidth));
        
        if (!string.IsNullOrEmpty(Attribute.MatrixRectMethod))
        {
            var method = targetObject.GetType().GetMethod(Attribute.MatrixRectMethod,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (method != null)
            {
                method.Invoke(targetObject, new object[] { matrixRect });
            }
            else
            {
                Debug.LogWarning($"Method '{Attribute.MatrixRectMethod}' not found on target object '{targetObject}'.");
            }
        }

        MethodInfo drawCellMethod = null;
        if (!string.IsNullOrEmpty(Attribute.DrawCellMethod))
        {
            drawCellMethod = targetObject.GetType().GetMethod(Attribute.DrawCellMethod,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (drawCellMethod != null)
            {
                if (drawCellMethod.ReturnType != elementType)
                {
                    throw new Exception($"Method '{nameof(Attribute.DrawCellMethod)}' must return a {elementType}.");
                }
            }
            else
            {
                throw new Exception($"A valid {nameof(Attribute.DrawCellMethod)} must be provided");
            }
        }
        else
        {
            throw new Exception($"A valid {nameof(Attribute.DrawCellMethod)} must be provided");
        }

        // Draw each square
        for (int i = 0; i < matrixSize; i++)
        {
            for (int j = 0; j < matrixSize; j++)
            {
                int index = i * matrixSize + j;
                if (index >= collection.Count) break;

                var squareRect = new Rect(
                    matrixRect.x + j * (squareSize + padding),
                    matrixRect.y + i * (squareSize + padding),
                    squareSize,
                    squareSize
                );
                
                collection[index] = drawCellMethod.Invoke(targetObject, new[] { squareRect, index, collection[index] });
            }
        }
    }

    private void GetProperties(IList collection, out float matrixWidth, out int matrixSize, out float squareSize, out float padding)
    {
        matrixSize = (int)Mathf.Sqrt(collection.Count);
        if (matrixSize * matrixSize != collection.Count)
            throw new Exception("A collection has a non square size. Matrix cannot be drawn");
    
        squareSize = EditorGUILayout.GetControlRect(false, 0f).size.x / matrixSize;

        padding = squareSize * Attribute.Padding;
        
        matrixWidth = matrixSize * (squareSize + padding);
    }
}