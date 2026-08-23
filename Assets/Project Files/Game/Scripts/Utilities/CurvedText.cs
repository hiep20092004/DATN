using System;
using Cysharp.Threading.Tasks;
using Sirenix.OdinInspector;
using TMPro;
using UnityEngine;

[RequireComponent(typeof(TextMeshProUGUI))]
public class CurvedText : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI textMeshPro;
    [SerializeField] private float radius = 50f;
    [SerializeField] private float arcAngle = 180f;

    private void Start()
    {
        if (textMeshPro == null)
            textMeshPro = GetComponent<TextMeshProUGUI>();
        ApplyCurve();
    }

    [Button]
    public void ApplyCurve()
    {
        textMeshPro.ForceMeshUpdate();

        var textInfo = textMeshPro.textInfo;
        var meshInfo = textInfo.meshInfo[0];

        for (int i = 0; i < textInfo.characterCount; i++)
        {
            var charInfo = textInfo.characterInfo[i];
            if (!charInfo.isVisible) continue;

            int vertexIndex = charInfo.vertexIndex;

            float t = (float)i / (textInfo.characterCount - 1);
            float angle = Mathf.Lerp(-arcAngle / 2, arcAngle / 2, t) * Mathf.Deg2Rad;

            Vector3 center = Vector3.zero;
            for (int j = 0; j < 4; j++)
            {
                center += meshInfo.vertices[vertexIndex + j];
            }

            center /= 4f;

            for (int j = 0; j < 4; j++)
            {
                Vector3 vertex = meshInfo.vertices[vertexIndex + j];

                vertex -= center;
                float cosAngle = Mathf.Cos(angle);
                float sinAngle = Mathf.Sin(angle);
                float rotatedX = vertex.x * cosAngle - vertex.y * sinAngle;
                float rotatedY = vertex.x * sinAngle + vertex.y * cosAngle;
                vertex = new Vector3(rotatedX, rotatedY, vertex.z);
                vertex += center;

                float x = vertex.x;
                float y = vertex.y + radius;

                float newX = x * cosAngle - y * sinAngle;
                float newY = x * sinAngle + y * cosAngle;

                meshInfo.vertices[vertexIndex + j] = new Vector3(newX, newY - radius, vertex.z);
            }
        }

        textMeshPro.UpdateVertexData();
    }
}