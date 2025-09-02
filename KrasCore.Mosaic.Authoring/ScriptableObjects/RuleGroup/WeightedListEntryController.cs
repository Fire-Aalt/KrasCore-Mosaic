using Unity.Properties;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace KrasCore.Mosaic.Authoring
{
    public class WeightedListEntryController
    {
        private IntegerField _weightField;
        private ObjectField _objectField;
        private Image _image;
    
        // This function retrieves a reference to the 
        // character name label inside the UI element.
        public void SetVisualElement(VisualElement visualElement)
        {
            _objectField = visualElement.Q<ObjectField>("ObjectField");
            _weightField = visualElement.Q<IntegerField>("WeightField");
            
            var imageHolder = visualElement.Q<VisualElement>("ImageHolder");
            _image = new Image
            {
                name = "WeightedListElementImage"
            };
            imageHolder.Add(_image);
        }
    
        // This function receives the character whose name this list 
        // element is supposed to display. Since the elements list 
        // in a `ListView` are pooled and reused, it's necessary to 
        // have a `Set` function to change which character's data to display.
        public void SetCharacterData(int index, PropertyPath path)
        {
            var tileSpritesPath = PropertyPath.AppendName(path, "TileSprites");
            var spriteResultPath = PropertyPath.AppendIndex(tileSpritesPath, index);

            var spriteBinding = new DataBinding
            {
                dataSourcePath = PropertyPath.AppendName(spriteResultPath, nameof(SpriteResult.result)),
                bindingMode = BindingMode.TwoWay
            };
            
            Debug.Log(_image);
            _image.SetBinding("sprite", spriteBinding);
            _objectField.SetBinding("value", spriteBinding);
            
            _weightField.SetBinding("value", new DataBinding
            {
                dataSourcePath = PropertyPath.AppendName(spriteResultPath, nameof(SpriteResult.weight)),
                bindingMode = BindingMode.TwoWay
            });
        }
    }
}