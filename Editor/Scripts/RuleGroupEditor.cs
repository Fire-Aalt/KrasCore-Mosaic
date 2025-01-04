using Mosaic.Runtime;
using Sirenix.OdinInspector.Editor;
using UnityEditor;

namespace Mosaic.Editor
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