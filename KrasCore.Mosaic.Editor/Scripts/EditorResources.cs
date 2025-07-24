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
        
        static EditorResources()
        {
            TextureMat = Resources.Load("default") as Material;
            
            NotTexture = Resources.Load("not") as Texture;
            AnyTexture = Resources.Load("any") as Texture;
            MatrixCenterTexture = Resources.Load("matrixCenter") as Texture;
        }
    }
}