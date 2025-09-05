using KrasCore.Editor;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace KrasCore.Mosaic.Editor
{
    [InitializeOnLoad]
    public static class EditorResources
    {
        public static readonly Texture NotTexture;
        public static readonly Texture AnyTexture;
        public static readonly Texture MatrixCenterTexture;

        public static readonly StyleSheet StyleSheet;
        public static readonly VisualTreeAsset WeightedListElementAsset;
        public static readonly VisualTreeAsset RuleGroupElementAsset;
        
        private const string ValidationRoot = "KrasCore.Mosaic.Editor";
        
        static EditorResources()
        {
            NotTexture = AssetDatabaseUtils.LoadEditorResource<Texture>("not.png", ValidationRoot);
            AnyTexture = AssetDatabaseUtils.LoadEditorResource<Texture>("any.png", ValidationRoot);
            MatrixCenterTexture = AssetDatabaseUtils.LoadEditorResource<Texture>("matrixCenter.png", ValidationRoot);
            
            StyleSheet = AssetDatabaseUtils.LoadEditorResource<StyleSheet>("IntGridMatrix.uss", ValidationRoot);
            WeightedListElementAsset = AssetDatabaseUtils.LoadEditorResource<VisualTreeAsset>("WeightedListViewItem.uxml", ValidationRoot);
            RuleGroupElementAsset = AssetDatabaseUtils.LoadEditorResource<VisualTreeAsset>("RuleGroupElement.uxml", ValidationRoot);
        }
    }
}