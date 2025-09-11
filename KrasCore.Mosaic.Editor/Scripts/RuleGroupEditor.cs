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
            var root = new VisualElement();
            root.styleSheets.Add(AssetDatabase.LoadAssetAtPath<ThemeStyleSheet>(defaultTheme));
            root.AddToClassList("unity-editor"); // Enable Editor related styles


            var targetObject = target as RuleGroup;
            var serializedObject = new SerializedObject(targetObject);

            var list = serializedObject.FindProperty(nameof(RuleGroup.rules));
            
            var builder = new ListViewBuilder<RuleGroup.Rule>()
            {
                DataSource = targetObject,
                List = targetObject.rules,
                MakeItem = () =>
                {
                    var newListEntry = EditorResources.RuleGroupElementAsset.Instantiate();
                    
                    var newListEntryLogic = new RuleController();
                    newListEntryLogic.SetVisualElement(newListEntry);
                    newListEntry.userData = newListEntryLogic;

                    return newListEntry;
                },
                BindItem = (element, index) =>
                {
                    ((RuleController)element.userData).BindData(index, list, targetObject.rules[index]);
                },
                SerializedListProperty = list
            };
            root.Add(builder.Build());

            return root;
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