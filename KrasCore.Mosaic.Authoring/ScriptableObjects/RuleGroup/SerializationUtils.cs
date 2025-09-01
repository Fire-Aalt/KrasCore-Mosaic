using UnityEditor;

namespace KrasCore.Mosaic.Authoring
{
    public static class SerializationUtils
    {
        public static object GetParentObject(SerializedProperty property)
        {
            var path = property.propertyPath;
            var i = path.LastIndexOf('.');
            
            if (i < 0)
            {
                return property.serializedObject.targetObject;
            }
            
            var parent = property.serializedObject.FindProperty(path.Substring(0, i));
            return parent.boxedValue;
        }
    }
}