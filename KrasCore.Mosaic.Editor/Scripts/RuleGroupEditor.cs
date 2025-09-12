using KrasCore.Mosaic.Authoring;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace KrasCore.Mosaic.Editor
{
    [CustomEditor(typeof(RuleGroup))]
    public class RuleGroupEditor : UnityEditor.Editor
    {
        private RuleGroup _target;
        
        public override VisualElement CreateInspectorGUI()
        {
            _target = (RuleGroup)target;
            
            const string defaultTheme = "Packages/com.unity.dt.app-ui/PackageResources/Styles/Themes/App UI - Editor Dark - Small.tss";
            var root = new VisualElement();

            root.styleSheets.Add(EditorResources.StyleSheet);
            root.AddToClassList("unity-editor");

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
                    DataSource = _target,
                    List = _target.rules,
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
                        if (index >= _target.rules.Count) return;
                        
                        _target.rules[index].Bind(_target, index);
                        ((RuleController)element.userData).BindData(index, list, _target.rules[index]);
                    },
                    CreateDataItem = () =>
                    {
                        var rule = new RuleGroup.Rule();
                        rule.Bind(_target, _target.rules.Count - 1);
                        return rule;
                    },
                    SerializedListProperty = list
                };
                var listView = listViewBuilder.Build();
                root.Add(listView);
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