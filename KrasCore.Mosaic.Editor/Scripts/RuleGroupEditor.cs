using KrasCore.Mosaic.Authoring;
using Sirenix.OdinInspector.Editor;
using UnityEditor;

namespace KrasCore.Mosaic.Editor
{
    [CustomEditor(typeof(RuleGroup))]
    public class RuleGroupEditor : OdinEditor
    {
        protected override void OnEnable()
        {
            base.OnEnable();
            
            RuleGroupMatrixWindow.NumberOfActiveInspectorWindows++;
        }

        protected override void OnDisable()
        {
            base.OnDisable();

            RuleGroupMatrixWindow.NumberOfActiveInspectorWindows--;
        }
    }
}