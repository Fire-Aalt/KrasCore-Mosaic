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
            root.AddToClassList("unity-editor");

            var targetObject = (RuleGroup)target;

            {
                var boundIntGridField = new ObjectField("Bound IntGrid");
                boundIntGridField.SetEnabled(false);
                boundIntGridField.BindProperty(serializedObject.FindProperty(nameof(RuleGroup.intGrid)));
                root.Add(boundIntGridField);
            }

            {
                var list = serializedObject.FindProperty(nameof(RuleGroup.rules));
                var listViewBuilder = new ListViewBuilder<RuleGroup.Rule>()
                {
                    ListLabel = "Rules",
                    DataSource = targetObject,
                    List = targetObject.rules,
                    MakeItem = () =>
                    {
                        var entry = EditorResources.RuleGroupElementAsset.Instantiate();
                        entry.styleSheets.Add(AssetDatabase.LoadAssetAtPath<ThemeStyleSheet>(defaultTheme));
                        
                        var controller = new RuleController();
                        controller.SetVisualElement(entry);
                        entry.userData = controller;

                        return entry;
                    },
                    BindItem = (element, index) =>
                    {
                        targetObject.rules[index].Bind(targetObject, index);
                        ((RuleController)element.userData).BindData(index, list, targetObject.rules[index]);
                    },
                    SerializedListProperty = list
                };
                root.Add(listViewBuilder.Build());
            }

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