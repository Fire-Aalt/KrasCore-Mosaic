using System.Collections.Generic;
using KrasCore.Mosaic.Authoring;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine.UIElements;

namespace KrasCore.Mosaic.Editor
{
    [CustomEditor(typeof(RuleGroup))]
    public class RuleGroupEditor : UnityEditor.Editor
    {
        public static HashSet<RuleGroup> InspectedTargets = new();
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
                        controller.SetVisualElement(_target, entry);
                        entry.userData = controller;

                        return entry;
                    },
                    BindItem = (element, index) =>
                    {
                        if (index >= _target.rules.Count) return;
                        
                        ((RuleController)element.userData).BindData(index, list, _target.rules[index]);
                    },
                    CreateDataItem = () =>
                    {
                        var rule = new RuleGroup.Rule();
                        rule.Bind(_target);
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
            InspectedTargets.Add((RuleGroup)target);
        }

        private void OnDisable()
        {
            InspectedTargets.Remove(_target);
        }
    }
}