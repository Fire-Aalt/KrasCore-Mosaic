using KrasCore.Mosaic.Authoring;
using KrasCore.Mosaic.Data;
using Unity.AppUI.UI;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using Toggle = Unity.AppUI.UI.Toggle;

namespace KrasCore.Mosaic.Editor
{
    public class RuleController
    {
        private Toggle _enabledToggle;
        
        private IntGridMatrixView _intGridMatrixView;

        private TouchSliderFloat _chanceSlider;
        private TransformationButton _horizontalRuleTransformation;
        private TransformationButton _verticalRuleTransformation;
        private TransformationButton _rotationRuleTransformation;
        
        private TransformationButton _horizontalResultTransformation;
        private TransformationButton _verticalResultTransformation;
        private TransformationButton _rotationResultTransformation;
        
        private RuleGroup.Rule _rule;
        private SerializedProperty _ruleProperty;
        
        public void SetVisualElement(VisualElement visualElement)
        {
            _enabledToggle = visualElement.Q<Toggle>("EnabledToggle");
            
            var matrixCol = visualElement.Q<VisualElement>("MatrixCol");
            _intGridMatrixView = new IntGridMatrixView(true);
            matrixCol.Add(_intGridMatrixView);
            
            _chanceSlider = visualElement.Q<TouchSliderFloat>("ChanceSlider");
            var ruleTransformations = visualElement.Q<VisualElement>("RuleTransformations");
            
            _horizontalRuleTransformation = CreateIconButton(Transformation.MirrorX, ruleTransformations, EditorResources.HorizontalSprite);
            _verticalRuleTransformation = CreateIconButton(Transformation.MirrorY, ruleTransformations, EditorResources.VerticalSprite);
            _rotationRuleTransformation = CreateIconButton(Transformation.Rotated, ruleTransformations, EditorResources.RotatedSprite);
            
            var resultTransformations = visualElement.Q<VisualElement>("ResultTransformations");
            
            _horizontalResultTransformation = CreateIconButton(Transformation.MirrorX, resultTransformations, EditorResources.HorizontalSprite);
            _verticalResultTransformation = CreateIconButton(Transformation.MirrorY, resultTransformations, EditorResources.VerticalSprite);
            _rotationResultTransformation = CreateIconButton(Transformation.Rotated, resultTransformations, EditorResources.RotatedSprite);
        }

        private static TransformationButton CreateIconButton(Transformation transformation, VisualElement root, Texture image)
        {
            var iconButton = new TransformationButton(transformation)
            {
                image = image
            };
            root.Add(iconButton);
            return iconButton;
        }
    
        public void BindData(int index, SerializedProperty list, RuleGroup.Rule rule)
        {
            _rule = rule;
            _ruleProperty = list.GetArrayElementAtIndex(index);

            _enabledToggle.value = rule.enabled.HasFlag(RuleGroup.Enabled.Enabled);
            _enabledToggle.RegisterValueChangedCallback(OnEnableFieldChange);

            _chanceSlider.value = rule.ruleChance;
            _chanceSlider.RegisterValueChangedCallback(OnChanceFieldChange);
            
            var matrixProperty = _ruleProperty.FindPropertyRelative(nameof(RuleGroup.Rule.ruleMatrix));
                    
            _intGridMatrixView.Bind(matrixProperty);
            _intGridMatrixView.RegisterCallback<ClickEvent>(OnMatrixClicked);
            
            var ruleTransformationProperty = _ruleProperty.FindPropertyRelative(nameof(RuleGroup.Rule.ruleTransformation));
            
            _horizontalRuleTransformation.Bind(ruleTransformationProperty);
            _verticalRuleTransformation.Bind(ruleTransformationProperty);
            _rotationRuleTransformation.Bind(ruleTransformationProperty);
            
            var resultTransformationProperty = _ruleProperty.FindPropertyRelative(nameof(RuleGroup.Rule.resultTransformation));
            
            _horizontalResultTransformation.Bind(resultTransformationProperty);
            _verticalResultTransformation.Bind(resultTransformationProperty);
            _rotationResultTransformation.Bind(resultTransformationProperty);
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
        
        private void OnMatrixClicked(ClickEvent clickEvent)
        {
            if (clickEvent.button != 0) return;
            IntGridMatrixWindow.OpenWindow(_rule);
        }
        
        private void OnChanceFieldChange(ChangeEvent<float> evt)
        {
            _rule.ruleChance = evt.newValue;
            _ruleProperty.serializedObject.Update();
        }
    }
}