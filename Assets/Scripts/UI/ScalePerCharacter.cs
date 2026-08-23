using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using TMPro;
using UnityEngine;

public class ScalePerCharacter : MonoBehaviour
{
    [SerializeField]
    private ScaleParameters scaleParameters;

    public bool HideAtStart = false;

    [SerializeField]
    private List<TextMeshProUGUI> tmpTexts = new List<TextMeshProUGUI>();
    public CanvasGroup canvasGroup;

    // Data for each TMP
    private class TMPData
    {
        public TextMeshProUGUI tmp;
        public TMP_TextInfo textInfo;
        public Vector3[][] originalVertices;
        public Dictionary<int, float> charScales = new Dictionary<int, float>();
    }

    private List<TMPData> tmpDataList = new List<TMPData>();
    private bool isPlaying = false;

    private void Awake()
    {
        Initialize().Forget();
    }

    private async UniTask Initialize()
    {
        if (HideAtStart)
        {
            canvasGroup.alpha = 0f;
        }

        await UniTask.Yield();

        if (!gameObject.activeInHierarchy) return;

        // Initialize all TMPs
        tmpDataList.Clear();
        foreach (var tmp in tmpTexts)
        {
            if (tmp == null) continue;

            tmp.ForceMeshUpdate();
            var textInfo = tmp.textInfo;

            if (textInfo == null || textInfo.meshInfo == null || textInfo.meshInfo.Length == 0)
                continue;

            var data = new TMPData
            {
                tmp = tmp,
                textInfo = textInfo
            };
            CacheOriginalVertices(data);
            tmpDataList.Add(data);
        }
    }

    private void OnDisable()
    {
        DOTween.Kill(this);
        isPlaying = false;
    }

    private void OnEnable()
    {
        // Re-initialize when re-enabled to refresh cached references
        if (tmpDataList.Count > 0 && !isPlaying)
        {
            Initialize().Forget();
        }
    }

    public async UniTask Play()
    {
        isPlaying = true;
        
        await UniTask.WaitForSeconds(scaleParameters.startDelay);

        // Validate and refresh all TMP data before starting
        if (!ValidateAndRefreshTMPData())
        {
            isPlaying = false;
            return;
        }

        // Hide all characters for all TMPs
        foreach (var data in tmpDataList)
        {
            if (!IsDataValid(data)) continue;
            
            data.charScales.Clear();
            for (int i = 0; i < data.textInfo.characterCount; i++)
            {
                if (!data.textInfo.characterInfo[i].isVisible)
                    continue;
                data.charScales[i] = HideAtStart ? 0f : scaleParameters.startScale;
            }
            ApplyTextScales(data);
        }

        // Animate all TMPs at the same time
        await AnimateAllCharacters();
        isPlaying = false;
    }

    private void CacheOriginalVertices(TMPData data)
    {
        data.originalVertices = new Vector3[data.textInfo.meshInfo.Length][];
        for (int i = 0; i < data.textInfo.meshInfo.Length; i++)
        {
            var srcVertices = data.textInfo.meshInfo[i].vertices;
            data.originalVertices[i] = new Vector3[srcVertices.Length];
            Array.Copy(srcVertices, data.originalVertices[i], srcVertices.Length);
        }
    }

    private bool ValidateAndRefreshTMPData()
    {
        bool anyValid = false;
        
        for (int i = tmpDataList.Count - 1; i >= 0; i--)
        {
            var data = tmpDataList[i];
            
            // Check if TMP component still exists
            if (data.tmp == null || !data.tmp.gameObject.activeInHierarchy)
            {
                tmpDataList.RemoveAt(i);
                continue;
            }

            // Force mesh update and refresh textInfo reference
            data.tmp.ForceMeshUpdate();
            data.textInfo = data.tmp.textInfo;

            // Validate textInfo
            if (data.textInfo == null || data.textInfo.meshInfo == null || data.textInfo.meshInfo.Length == 0)
            {
                tmpDataList.RemoveAt(i);
                continue;
            }

            // Re-cache original vertices with fresh data
            CacheOriginalVertices(data);
            anyValid = true;
        }

        return anyValid;
    }

    private bool IsDataValid(TMPData data)
    {
        if (data == null) return false;
        if (data.tmp == null) return false;
        if (!data.tmp.gameObject.activeInHierarchy) return false;
        if (data.textInfo == null) return false;
        if (data.textInfo.meshInfo == null || data.textInfo.meshInfo.Length == 0) return false;
        if (data.originalVertices == null) return false;
        
        return true;
    }

    private async UniTask AnimateAllCharacters()
    {
        canvasGroup.alpha = 1f;

        // Find max character count across all TMPs
        int maxCharCount = 0;
        foreach (var data in tmpDataList)
        {
            if (!IsDataValid(data)) continue;
            
            if (data.textInfo.characterCount > maxCharCount)
                maxCharCount = data.textInfo.characterCount;
        }

        // Animate character by character, all TMPs in sync
        for (int i = 0; i < maxCharCount; i++)
        {
            if (!isPlaying || !gameObject.activeInHierarchy)
                break;

            int charIndex = i;

            foreach (var data in tmpDataList)
            {
                if (!IsDataValid(data)) continue;
                
                if (charIndex >= data.textInfo.characterCount)
                    continue;

                if (!data.textInfo.characterInfo[charIndex].isVisible)
                    continue;

                var capturedData = data;

                // Phase 1
                _ = DOTween.To(
                    () => capturedData.charScales.ContainsKey(charIndex) ? capturedData.charScales[charIndex] : 0f,
                    x =>
                    {
                        if (!IsDataValid(capturedData)) return;
                        
                        capturedData.charScales[charIndex] = x;
                        ApplyTextScales(capturedData);
                    },
                    scaleParameters.endScalePhase1,
                    scaleParameters.durationPhase1
                ).SetEase(scaleParameters.easePhase2).SetTarget(this);

                // Phase 2
                _ = DOTween.To(
                    () => capturedData.charScales.ContainsKey(charIndex) ? capturedData.charScales[charIndex] : 0f,
                    x =>
                    {
                        if (!IsDataValid(capturedData)) return;
                        
                        capturedData.charScales[charIndex] = x;
                        ApplyTextScales(capturedData);
                    },
                    scaleParameters.endScalePhase2,
                    scaleParameters.durationPhase2
                ).SetEase(scaleParameters.easePhase2).SetTarget(this).SetDelay(scaleParameters.durationPhase1);
            }

            await UniTask.WaitForSeconds(scaleParameters.charDelay);
        }
    }

    private void ApplyTextScales(TMPData data)
    {
        // Comprehensive validation before accessing mesh data
        if (!IsDataValid(data)) return;

        try
        {
            // Copy original vertices to current mesh
            for (int i = 0; i < data.textInfo.meshInfo.Length; i++)
            {
                if (i >= data.originalVertices.Length) break;
                if (data.textInfo.meshInfo[i].vertices == null) continue;
                if (data.originalVertices[i] == null) continue;
                
                Array.Copy(data.originalVertices[i], data.textInfo.meshInfo[i].vertices, 
                    Mathf.Min(data.originalVertices[i].Length, data.textInfo.meshInfo[i].vertices.Length));
            }

            // Apply scale to all characters
            foreach (var kvp in data.charScales)
            {
                int charIndex = kvp.Key;
                float scale = kvp.Value;

                if (charIndex >= data.textInfo.characterCount) continue;

                var charInfo = data.textInfo.characterInfo[charIndex];
                if (!charInfo.isVisible) continue;

                int materialIndex = charInfo.materialReferenceIndex;
                int vertexIndex = charInfo.vertexIndex;

                if (materialIndex >= data.textInfo.meshInfo.Length) continue;
                if (materialIndex >= data.originalVertices.Length) continue;

                Vector3[] vertices = data.textInfo.meshInfo[materialIndex].vertices;
                Vector3[] origVerts = data.originalVertices[materialIndex];

                if (vertices == null || origVerts == null) continue;
                if (vertexIndex + 3 >= vertices.Length || vertexIndex + 3 >= origVerts.Length) continue;

                // Calculate center from original vertices
                Vector3 charCenter = (origVerts[vertexIndex] + origVerts[vertexIndex + 2]) / 2f;

                for (int j = 0; j < 4; j++)
                {
                    Vector3 offset = origVerts[vertexIndex + j] - charCenter;
                    vertices[vertexIndex + j] = charCenter + offset * scale;
                }
            }

            // Final check before updating mesh
            if (data.tmp && data.tmp.gameObject.activeInHierarchy)
            {
                data.tmp.UpdateVertexData(TMP_VertexDataUpdateFlags.Vertices);
            }
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"Failed to apply text scales: {ex.Message}");
        }
    }
}

[Serializable]
public class ScaleParameters
{
    public float startDelay = 0f;
    public float startScale = 0f;
    public float endScalePhase1 = 1f;
    public float endScalePhase2 = 1f;
    public float durationPhase1 = 0.3f;
    public float durationPhase2 = 0.3f;
    public float charDelay = 0.05f;
    public AnimationCurve easePhase2 = AnimationCurve.EaseInOut(0, 0, 1, 1);
}