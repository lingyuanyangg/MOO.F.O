using System.Collections;
using UnityEngine;
using UnityEngine.Rendering;

public class PopArtShockwave : MonoBehaviour
{
    private LineRenderer line;
    private Material material;

    public static void Spawn(Vector3 position, float radius)
    {
        var ring = new GameObject("Pop Purple Shockwave");
        ring.transform.position = position + Vector3.up * 0.10f;
        ring.AddComponent<PopArtShockwave>().Initialize(radius);
    }

    private void Initialize(float radius)
    {
        line = gameObject.AddComponent<LineRenderer>();
        line.useWorldSpace = false;
        line.loop = true;
        line.positionCount = 49;
        line.alignment = LineAlignment.View;
        line.shadowCastingMode = ShadowCastingMode.Off;
        line.receiveShadows = false;
        line.numCornerVertices = 3;
        Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
        if (shader == null) shader = Shader.Find("Unlit/Color");
        material = new Material(shader) { name = "Runtime Purple Shockwave" };
        Color color = new Color(2.7f, 0.05f, 2.2f, 1f);
        material.color = color;
        if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
        line.material = material;
        StartCoroutine(Animate(radius));
    }

    private IEnumerator Animate(float radius)
    {
        const float duration = 0.38f;
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            float currentRadius = Mathf.Lerp(0.18f, radius, 1f - Mathf.Pow(1f - t, 3f));
            for (int i = 0; i < line.positionCount; i++)
            {
                float angle = i / (float)(line.positionCount - 1) * Mathf.PI * 2f;
                line.SetPosition(i, new Vector3(Mathf.Cos(angle) * currentRadius, 0f, Mathf.Sin(angle) * currentRadius));
            }
            line.startWidth = line.endWidth = Mathf.Lerp(0.22f, 0.015f, t);
            Color color = new Color(2.7f, 0.05f, 2.2f, 1f - t);
            line.startColor = line.endColor = color;
            yield return null;
        }
        Destroy(gameObject);
    }

    private void OnDestroy()
    {
        if (material != null) Destroy(material);
    }
}
