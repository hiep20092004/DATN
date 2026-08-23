using WaterFlow.Core;
using UnityEditor;

namespace WaterFlow.Game
{
    /// <summary>
    /// Keeps <see cref="BlockFigureGeometryCache"/> warm in the editor when not playing (domain reload / tooling).
    /// </summary>
    internal static class BlockFigureGeometryCacheEditorBootstrap
    {
        [InitializeOnLoadMethod]
        private static void OnEditorLoaded()
        {
            BlocksVisualsData data = EditorUtils.GetAsset<BlocksVisualsData>();
            BlockFigureGeometryCache.Rebuild(data);
        }
    }
}
