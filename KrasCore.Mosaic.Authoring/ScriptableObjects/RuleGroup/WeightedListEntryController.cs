using Unity.Properties;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace KrasCore.Mosaic.Authoring
{
    public class WeightedListEntryController
    {
        private ObjectField _objectField;
        private IntegerField _weightField;
        private Image _image;
    
        public void SetVisualElement(VisualElement visualElement)
        {
            _objectField = visualElement.Q<ObjectField>("ObjectField");
            _weightField = visualElement.Q<IntegerField>("WeightField");
            
            var imageHolder = visualElement.Q<VisualElement>("ImageHolder");
            _image = new Image { name = "WeightedListElementImage" };
            imageHolder.Add(_image);
        }
    
        public void SetSpriteData(int index, PropertyPath path)
        {
            var tileSpritesPath = PropertyPath.AppendName(path, "TileSprites");
            var spriteResultPath = PropertyPath.AppendIndex(tileSpritesPath, index);

            var spriteBinding = new DataBinding
            {
                dataSourcePath = PropertyPath.AppendName(spriteResultPath, nameof(SpriteResult.result)),
                bindingMode = BindingMode.TwoWay
            };
            
            _objectField.SetBinding("value", spriteBinding);
            _image.SetBinding("sprite", spriteBinding);
            
            _weightField.SetBinding("value", new DataBinding
            {
                dataSourcePath = PropertyPath.AppendName(spriteResultPath, nameof(SpriteResult.weight)),
                bindingMode = BindingMode.TwoWay
            });
        }
    }
}