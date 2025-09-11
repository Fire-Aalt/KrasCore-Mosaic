using KrasCore.Mosaic.Authoring;
using KrasCore.Mosaic.Data;
using Unity.AppUI.UI;
using UnityEditor;
using UnityEngine.UIElements;
using Toggle = Unity.AppUI.UI.Toggle;

namespace KrasCore.Mosaic.Editor
{
    public class RuleController
    {
        private IntGridMatrixView _intGridMatrixView;
        private TouchSliderFloat _chanceSlider;
        private Toggle _enabledToggle;

        private Image _horizontalRuleTransformation;
        private Image _verticalRuleTransformation;
        private Image _rotationRuleTransformation;
        
        
        public void SetVisualElement(VisualElement visualElement)
        {
            var matrixCol = visualElement.Q<VisualElement>("MatrixCol");
            _enabledToggle = visualElement.Q<Toggle>("EnabledToggle");
            _chanceSlider = visualElement.Q<TouchSliderFloat>("ChanceSlider");
            
            _intGridMatrixView = new IntGridMatrixView();
            var fieldInfo = typeof(RuleGroup.Rule).GetField(nameof(RuleGroup.Rule.ruleMatrix));
            var matrix = _intGridMatrixView.Create(fieldInfo, new IntGridMatrixAttribute() {IsReadonly = true, MatrixRectMethod = nameof(RuleGroup.Rule.MatrixControlRect)});
                    
            matrixCol.Add(matrix);
            var ruleTransformations = visualElement.Q<VisualElement>("RuleTransformations");

            _horizontalRuleTransformation = new Image();
            _horizontalRuleTransformation.image = EditorResources.HorizontalSprite;
            _horizontalRuleTransformation.AddToClassList("icon-button");
            _verticalRuleTransformation = new Image();
            _verticalRuleTransformation.image = EditorResources.VerticalSprite;
            _verticalRuleTransformation.AddToClassList("icon-button");
            _rotationRuleTransformation = new Image();
            _rotationRuleTransformation.image = EditorResources.RotatedSprite;
            _rotationRuleTransformation.AddToClassList("icon-button");
            
            ruleTransformations.Add(_horizontalRuleTransformation);
            ruleTransformations.Add(_verticalRuleTransformation);
            ruleTransformations.Add(_rotationRuleTransformation);
        }
        
        private RuleGroup.Rule _rule;
        private SerializedProperty _ruleProperty;
    
        public void BindData(int index, SerializedProperty list, RuleGroup.Rule rule)
        {
            _rule = rule;
            _ruleProperty = list.GetArrayElementAtIndex(index);

            _enabledToggle.value = rule.enabled.HasFlag(RuleGroup.Enabled.Enabled);
            _enabledToggle.RegisterValueChangedCallback(OnEnableFieldChange);

            _chanceSlider.value = rule.ruleChance;
            _chanceSlider.RegisterValueChangedCallback(OnChanceFieldChange);
            
            var matrixProperty = _ruleProperty.FindPropertyRelative(nameof(RuleGroup.Rule.ruleMatrix));
                    
            _intGridMatrixView.Bind(matrixProperty, rule);
            
            BindIconButton(_horizontalRuleTransformation, Transformation.MirrorX);
            BindIconButton(_verticalRuleTransformation, Transformation.MirrorY);
            BindIconButton(_rotationRuleTransformation, Transformation.Rotated);
        }

        private void BindIconButton(Image iconButton, Transformation transformation)
        {
            if (_rule.ruleTransformation.HasFlag(transformation))
                EnableIconButton(iconButton);
            else
                DisableIconButton(iconButton);
            iconButton.RegisterCallback<ClickEvent, Data>(OnRuleTransformClicked, new Data
            {
                Image = iconButton,
                Transform = transformation
            });
        }

        private struct Data
        {
            public VisualElement Image;
            public Transformation Transform;
        }

        private const string Disabled = "icon-button-disabled";
        private const string Enabled = "icon-button-enabled";
        
        private void OnRuleTransformClicked(ClickEvent _, Data data)
        {
            _rule.ruleTransformation ^= data.Transform;
            if (_rule.ruleTransformation.HasFlag(data.Transform))
            {
                EnableIconButton(data.Image);
            }
            else
            {
                DisableIconButton(data.Image);
            }
            
            _ruleProperty.serializedObject.Update();
        }

        private static void EnableIconButton(VisualElement image)
        {
            image.RemoveFromClassList(Disabled);
            image.AddToClassList(Enabled);
        }
        
        private static void DisableIconButton(VisualElement image)
        {
            image.RemoveFromClassList(Enabled);
            image.AddToClassList(Disabled);
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
        
        private void OnChanceFieldChange(ChangeEvent<float> evt)
        {
            _rule.ruleChance = evt.newValue;
            _ruleProperty.serializedObject.Update();
        }
    }
}