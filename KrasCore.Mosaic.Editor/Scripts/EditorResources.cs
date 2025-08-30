using KrasCore.Editor;
using UnityEditor;
using UnityEngine;

namespace KrasCore.Mosaic.Editor
{
    [InitializeOnLoad]
    public static class EditorResources
    {
        public static readonly Color BackgroundCellColor = new(0f, 0f, 0f, 0.5f);
        
        public static readonly Material TextureMat;

        public static readonly Texture NotTexture;
        public static readonly Texture AnyTexture;
        public static readonly Texture MatrixCenterTexture;

        private const string ValidationRoot = "KrasCore.Mosaic.Editor";
        
        static EditorResources()
        {
            TextureMat = AssetDatabaseUtils.LoadEditorResource<Material>("default.mat", ValidationRoot);
            
            NotTexture = AssetDatabaseUtils.LoadEditorResource<Texture>("not.png", ValidationRoot);
            AnyTexture = AssetDatabaseUtils.LoadEditorResource<Texture>("any.png", ValidationRoot);
            MatrixCenterTexture = AssetDatabaseUtils.LoadEditorResource<Texture>("matrixCenter.png", ValidationRoot);
        }
    }
}