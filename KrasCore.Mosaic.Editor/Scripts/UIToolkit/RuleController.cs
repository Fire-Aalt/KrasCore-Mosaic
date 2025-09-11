using KrasCore.Mosaic.Authoring;
using UnityEditor;
using UnityEngine.UIElements;
using Toggle = Unity.AppUI.UI.Toggle;

namespace KrasCore.Mosaic.Editor
{
    public class RuleController
    {
        private IntGridMatrixView _intGridMatrixView;
        private Toggle _enabledToggle;
    
        public void SetVisualElement(VisualElement visualElement)
        {
            var matrixCol = visualElement.Q<VisualElement>("MatrixCol");
            _enabledToggle = visualElement.Q<Toggle>("EnabledToggle");
            
            _intGridMatrixView = new IntGridMatrixView();
            var fieldInfo = typeof(RuleGroup.Rule).GetField(nameof(RuleGroup.Rule.ruleMatrix));
            var matrix = _intGridMatrixView.Create(fieldInfo, new IntGridMatrixAttribute() {IsReadonly = true, MatrixRectMethod = nameof(RuleGroup.Rule.MatrixControlRect)});
                    
            matrixCol.Add(matrix);
        }
        private RuleGroup.Rule _rule;
        private SerializedProperty _ruleProperty;
    
        public void BindData(int index, SerializedProperty list, RuleGroup.Rule rule)
        {
            _rule = rule;
            _ruleProperty = list.GetArrayElementAtIndex(index);

            _enabledToggle.value = rule.enabled.HasFlag(RuleGroup.Enabled.Enabled);
            _enabledToggle.RegisterValueChangedCallback(OnEnableFieldChange);
                    
            var matrixProperty = _ruleProperty.FindPropertyRelative(nameof(RuleGroup.Rule.ruleMatrix));
                    
            _intGridMatrixView.Bind(matrixProperty, rule);
        }
        
        private void OnEnableFieldChange(ChangeEvent<bool> evt)
        {
            if (evt.newValue)
            {
                _rule.enabled = RuleGroup.Enabled.Enabled;
            }
            else
            {
                _rule.enabled ^= RuleGroup.Enabled.Enabled;
            }
            _ruleProperty.serializedObject.Update();
        }
    }
}