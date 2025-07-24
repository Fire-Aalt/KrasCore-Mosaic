using KrasCore.Mosaic.Authoring;
using KrasCore.Mosaic.Data;
using Sirenix.OdinInspector.Editor;
using Sirenix.Utilities.Editor;
using UnityEditor;
using UnityEngine;

namespace KrasCore.Mosaic.Editor
{
    public class IntGridValueSelectorDrawer : OdinAttributeDrawer<IntGridValueSelectorDrawerAttribute, IntGridValueSelector>
    {
        private IntGridValueSelector Selector => ValueEntry.SmartValue;
        private IntGridDefinition IntGrid => Selector.intGrid;

        protected override void DrawPropertyLayout(GUIContent label)
        {
            SirenixEditorGUI.BeginBox();
            for (int i = 0; i < IntGrid.intGridValues.Count; i++)
            {
                if (IntGridButton(0, IntGrid.intGridValues[i].name, Selector.value == IntGrid.intGridValues[i].value,
                        IntGrid.intGridValues[i].texture, IntGrid.intGridValues[i].color))
                {
                    Selector.value = IntGrid.intGridValues[i].value;
                }
            }
            
            if (IntGridButton(0, "Any Value/No Value", Selector.value == RuleGridConsts.AnyIntGridValue, EditorResources.AnyTexture, Color.white))
            {
                Selector.value = RuleGridConsts.AnyIntGridValue;
            }
            SirenixEditorGUI.EndBox();
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
            if (Event.current.type == EventType.MouseDown)
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
                EditorGUI.DrawPreviewTexture(iconRect, icon, EditorResources.TextureMat, ScaleMode.ScaleToFit);
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