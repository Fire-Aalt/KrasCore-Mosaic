using KrasCore.Mosaic.Authoring;
using UnityEditor;
using UnityEngine.UIElements;

namespace KrasCore.Mosaic.Editor
{
    [CustomEditor(typeof(RuleGroup))]
    public class RuleGroupEditor : UnityEditor.Editor
    {
        public override VisualElement CreateInspectorGUI()
        {
            const string defaultTheme = "Packages/com.unity.dt.app-ui/PackageResources/Styles/Themes/App UI - Editor Dark - Small.tss";
            var element = EditorResources.RuleGroupElementAsset.Instantiate();
            element.styleSheets.Add(AssetDatabase.LoadAssetAtPath<ThemeStyleSheet>(defaultTheme));
            element.AddToClassList("unity-editor"); // Enable Editor related styles
            
            return element;
        }


        private void OnEnable()
        {
            IntGridMatrixWindow.NumberOfActiveInspectorWindows++;
        }

        private void OnDisable()
        {
            IntGridMatrixWindow.NumberOfActiveInspectorWindows--;
        }
    }
}