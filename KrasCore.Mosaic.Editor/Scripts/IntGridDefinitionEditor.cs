using BovineLabs.Core.Editor.Inspectors;
using KrasCore.Editor;
using KrasCore.Mosaic.Authoring;
using UnityEditor;
using UnityEngine.UIElements;

namespace KrasCore.Mosaic.Editor
{
    [CustomEditor(typeof(IntGridDefinition))]
    public class IntGridDefinitionEditor : ElementEditor
    {
        private IntGridDefinition _target;
        
        protected override void PostElementCreation(VisualElement root, bool createdElements)
        {
            _target = (IntGridDefinition)target;

            var btn = new Button(CreateRuleGroup)
            {
                text = "Create Rule Group"
            };
            root.Add(btn);
        }
        
        private void CreateRuleGroup()
        {
            var instance = AssetDatabaseUtils.CreateNewScriptableObjectAsset<RuleGroup>(name + "Group", _target);
            instance.intGrid = _target;
            _target.ruleGroups.Add(instance);
            serializedObject.Update();
        }
    }
}