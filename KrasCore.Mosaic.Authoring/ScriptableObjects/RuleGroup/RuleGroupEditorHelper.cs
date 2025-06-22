using System.Collections.Generic;
using KrasCore.Mosaic.Data;
using Sirenix.Utilities;
using Sirenix.Utilities.Editor;
using UnityEditor;
using UnityEngine;

namespace KrasCore.Mosaic.Authoring
{
    [InitializeOnLoad]
    public static class RuleGroupEditorHelper
    {
        public static readonly Color BackgroundCellColor = new(0f, 0f, 0f, 0.5f);
        
        public static readonly Material TextureMat;

        public static readonly Texture NotTexture;
        public static readonly Texture AnyTexture;
        public static readonly Texture MatrixCenterTexture;
        
        static RuleGroupEditorHelper()
        {
            TextureMat = Resources.Load("default") as Material;
            
            NotTexture = Resources.Load("not") as Texture;
            AnyTexture = Resources.Load("any") as Texture;
            MatrixCenterTexture = Resources.Load("matrixCenter") as Texture;
        }
        
        public static int IntGridValueDrawer(int intGridValue, List<IntGridValue> intGridValues)
        {
            SirenixEditorGUI.BeginBox();
            for (int i = 0; i < intGridValues.Count; i++)
            {
                if (IntGridButton(0, intGridValues[i].name, intGridValue == intGridValues[i].value, intGridValues[i].texture, intGridValues[i].color))
                {
                    intGridValue = intGridValues[i].value;
                }
            }
            
            if (IntGridButton(0, "Any Value/No Value", intGridValue == RuleGridConsts.AnyIntGridValue, AnyTexture, Color.white))
            {
                intGridValue = RuleGridConsts.AnyIntGridValue;
            }
            SirenixEditorGUI.EndBox();
            
            return intGridValue;
        }
        
        private static bool IntGridButton(int indent, string text, bool isActive, Texture icon, Color cellColor)
        {
            bool flag1 = false;
            Rect rect = EditorGUILayout.BeginHorizontal(SirenixGUIStyles.MenuButtonBackground);
            bool flag2 = rect.Contains(Event.current.mousePosition);
            if (isActive)
                SirenixEditorGUI.DrawSolidRect(rect, flag2 ? SirenixGUIStyles.MenuButtonActiveBgColor : SirenixGUIStyles.MenuButtonActiveBgColor);
            else
                SirenixEditorGUI.DrawSolidRect(rect, flag2 ? SirenixGUIStyles.MenuButtonHoverColor : SirenixGUIStyles.MenuButtonColor);
            SirenixEditorGUI.DrawBorders(rect, 0, 0, 0, 1, SirenixGUIStyles.MenuButtonBorderColor);
            if (Event.current.type == UnityEngine.EventType.MouseDown)
            {
                if (flag2)
                {
                    Event.current.Use();
                    flag1 = true;
                }
                GUIHelper.RequestRepaint();
            }
            GUIStyle style = new GUIStyle(EditorStyles.label);
            style.fixedHeight = 40f;
            if (isActive)
                style.normal.textColor = Color.white;
            GUILayout.Space((float) (indent * 10));

            var iconRect = new Rect(rect.position + new Vector2(0f, 3f), new Vector2(40f, 40f));
            if (icon != null)
            {
                EditorGUI.DrawPreviewTexture(iconRect, icon, TextureMat, ScaleMode.ScaleToFit);
            }
            else
            {
                EditorGUI.DrawRect(iconRect, cellColor);
            }
            GUILayout.Space(45f);
            
            GUILayout.Label(new GUIContent(text), style);
            EditorGUILayout.EndHorizontal();
            return flag1;
        }

        
        
        public static void DrawMatrixCell(Rect rect, int index, int matrixValue, IntGrid intGrid, bool isReadOnly)
        {
            DrawIntGridValue(rect, matrixValue, intGrid);
            DrawNotTextureIfNeeded(rect, matrixValue);
            
            if (index == RuleGroup.Rule.MatrixSize / 2 * RuleGroup.Rule.MatrixSize + RuleGroup.Rule.MatrixSize / 2)
            {
                EditorGUI.DrawPreviewTexture(rect, MatrixCenterTexture, TextureMat, ScaleMode.ScaleToFit);
            }
            
            if (isReadOnly)
            {
                EditorGUI.DrawRect(rect, new Color(0f, 0f, 0f, 0.2f));
            }
        }

        private static void DrawIntGridValue(Rect rect, int matrixValue, IntGrid intGrid)
        {
            if (Mathf.Abs(matrixValue) == RuleGridConsts.AnyIntGridValue)
            {
                EditorGUI.DrawRect(rect.Padding(1), BackgroundCellColor);
                DrawBuiltInCellTexture(rect, AnyTexture, Color.white);
                return;
            }

            if (matrixValue == 0)
            {
                EditorGUI.DrawRect(rect.Padding(1), BackgroundCellColor);
                return;
            }
            
            var intGridValue = IntGridToIndex(matrixValue, intGrid);
            if (intGridValue.texture == null)
            {
                EditorGUI.DrawRect(rect.Padding(1), intGridValue.color);
            }
            else
            {
                EditorGUI.DrawRect(rect.Padding(1), BackgroundCellColor);
                EditorGUI.DrawPreviewTexture(rect, intGridValue.texture, TextureMat, ScaleMode.ScaleToFit);
            }
        }
        
        private static void DrawNotTextureIfNeeded(Rect rect, int value)
        {
            if (value < 0)
            {
                DrawBuiltInCellTexture(rect, NotTexture, Color.red);
            }
        }

        private static void DrawBuiltInCellTexture(Rect rect, Texture texture, Color borderColor)
        {
            SirenixEditorGUI.DrawBorders(rect.Padding(4f), 2, 2, 2, 2, borderColor);
            var size = rect.size;
            rect.size *= 0.6f;
            rect.position += (size - rect.size) * 0.5f;
            EditorGUI.DrawPreviewTexture(rect, texture, TextureMat, ScaleMode.ScaleToFit);
        }
        
        public static IntGridValue IntGridToIndex(int intGridValue, IntGrid intGrid)
        {
            return intGridValue != 0 ? intGrid.IntGridValuesDict[Mathf.Abs(intGridValue)] : null;
        }
    }
}