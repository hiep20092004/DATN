using UnityEngine;
using UnityEngine.UI;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace WaterFlow.Game
{
    [ExecuteAlways]
    [RequireComponent(typeof(Graphic))]
    public class UVScrollEffect : BaseMeshEffect
    {
        [SerializeField] private Vector2 scrollSpeed = new(0.06f, 0f);
        [SerializeField] private bool previewInEditor = true;
        [SerializeField] private bool useUnscaledTime = true;
        [SerializeField] private bool wrapOffset01 = true;

        private Vector2 scrollOffset;
        private float lastTime;

        protected override void OnEnable()
        {
            base.OnEnable();
            lastTime = GetCurrentTime();
            graphic?.SetVerticesDirty();

#if UNITY_EDITOR
            EditorApplication.update -= OnEditorUpdate;
            EditorApplication.update += OnEditorUpdate;
#endif
        }

        protected override void OnDisable()
        {
#if UNITY_EDITOR
            EditorApplication.update -= OnEditorUpdate;
#endif
            base.OnDisable();
        }

        private void OnValidate()
        {
            graphic?.SetVerticesDirty();
        }

        private void Update()
        {
            if (!ShouldAnimate())
            {
                return;
            }

            graphic?.SetVerticesDirty();
        }

        public override void ModifyMesh(VertexHelper vh)
        {
            if (!IsActive() || !ShouldAnimate())
            {
                return;
            }

            float currentTime = GetCurrentTime();
            float deltaTime = Mathf.Max(0f, currentTime - lastTime);
            lastTime = currentTime;

            scrollOffset += scrollSpeed * deltaTime;
            if (wrapOffset01)
            {
                scrollOffset.x = Mathf.Repeat(scrollOffset.x, 1f);
                scrollOffset.y = Mathf.Repeat(scrollOffset.y, 1f);
            }

            UIVertex vertex = default;
            int vertexCount = vh.currentVertCount;
            for (int i = 0; i < vertexCount; i++)
            {
                vh.PopulateUIVertex(ref vertex, i);
                vertex.uv0 += (Vector4)scrollOffset;
                vh.SetUIVertex(vertex, i);
            }
        }

        private bool ShouldAnimate()
        {
            if (Application.isPlaying)
            {
                return true;
            }

            return previewInEditor;
        }

        private float GetCurrentTime()
        {
            if (Application.isPlaying)
            {
                return useUnscaledTime ? Time.unscaledTime : Time.time;
            }

#if UNITY_EDITOR
            return (float)EditorApplication.timeSinceStartup;
#else
            return 0f;
#endif
        }

#if UNITY_EDITOR
        private void OnEditorUpdate()
        {
            if (!previewInEditor || Application.isPlaying)
            {
                return;
            }

            EditorApplication.QueuePlayerLoopUpdate();
            graphic?.SetVerticesDirty();
        }
#endif
    }
}
