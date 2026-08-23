using UnityEngine;

namespace WaterFlow.Game
{
    public class GeneratorLinkedBorderVisual : MonoBehaviour, IGeneratorLinkedVisual
    {
        private MeshRenderer[] renderers;

        public void Init(MeshRenderer[] meshRenderers)
        {
            renderers = meshRenderers;
            if (renderers == null)
                return;

            for (int i = 0; i < renderers.Length; i++)
            {
                MeshRenderer r = renderers[i];
                if (r != null)
                    r.enabled = false;
            }
        }

        public void OnGeneratorEmptied()
        {
            if (renderers == null)
                return;

            for (int i = 0; i < renderers.Length; i++)
            {
                MeshRenderer r = renderers[i];
                if (r != null)
                    r.enabled = true;
            }
        }
    }
}
