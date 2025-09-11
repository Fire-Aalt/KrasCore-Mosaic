using KrasCore.Mosaic.Authoring;
using UnityEditor;
using UnityEditor.UIElements;
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

            root.styleSheets.Add(EditorResources.StyleSheet);
            root.AddToClassList("unity-editor"); // Enable Editor related styles


            var targetObject = target as RuleGroup;
            var serializedObject = new SerializedObject(targetObject);

            var list = serializedObject.FindProperty(nameof(RuleGroup.rules));
            
            var pr = new ObjectField("Bound IntGrid");
            pr.SetEnabled(false);
            pr.BindProperty(serializedObject.FindProperty(nameof(RuleGroup.intGrid)));
            root.Add(pr);
            
            var builder = new ListViewBuilder<RuleGroup.Rule>()
            {
                ListLabel = "Rules",
                DataSource = targetObject,
                List = targetObject.rules,
                MakeItem = () =>
                {
                    var newListEntry = EditorResources.RuleGroupElementAsset.Instantiate();
                    newListEntry.styleSheets.Add(AssetDatabase.LoadAssetAtPath<ThemeStyleSheet>(defaultTheme));
                    
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