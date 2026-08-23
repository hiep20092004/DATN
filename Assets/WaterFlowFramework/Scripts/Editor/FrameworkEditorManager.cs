#if UNITY_EDITOR
using UnityEditor;

namespace WaterFlow.Framework.Editor
{
    public static class FrameworkEditorManager
    {
        [MenuItem("WaterFlow Framework/Resolve")]
        public static void ResolveGameSystem()
        {
            CreateCustomEnum.CheckCreateEnums();
        }
    }
}
#endif