using System.Collections.Generic;
using KrasCore.Mosaic.Data;
using Sirenix.Utilities;
using Sirenix.Utilities.Editor;
using UnityEditor;
using UnityEngine;

namespace KrasCore.Mosaic.Authoring
{
    [InitializeOnLoad]
    public static class EditorHelper
    {
        public static readonly Color BackgroundCellColor = new(0f, 0f, 0f, 0.5f);
        
        public static readonly Material TextureMat;

        public static readonly Texture NotTexture;
        public static readonly Texture AnyTexture;
        public static readonly Texture MatrixCenterTexture;
        
        static EditorHelper()
        {
            TextureMat = Resources.Load("default") as Material;
            
            NotTexture = Resources.Load("not") as Texture;
            AnyTexture = Resources.Load("any") as Texture;
            MatrixCenterTexture = Resources.Load("matrixCenter") as Texture;
        }
        
        public static short IntGridValueDrawer(short intGridValue, List<IntGridValueDefinition> intGridValues)
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
    }
}