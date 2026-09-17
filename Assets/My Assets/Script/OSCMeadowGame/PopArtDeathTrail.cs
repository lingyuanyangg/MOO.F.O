using UnityEngine;
using UnityEngine.Rendering;

public static class PopArtDeathTrail
{
    public static void Begin(Transform target, float duration = 1.25f)
    {
        if (target == null) return;
        var trailObject = new GameObject("RGB Death Trail");
        trailObject.transform.SetParent(target, false);
        trailObject.transform.localPosition = Vector3.up * 0.75f;
        var trail = trailObject.AddComponent<TrailRenderer>();
        trail.time = 0.42f;
        trail.minVertexDistance = 0.035f;
        trail.startWidth = 0.72f;
        trail.endWidth = 0.02f;
        trail.numCapVertices = 5;
        trail.numCornerVertices = 5;
        trail.shadowCastingMode = ShadowCastingMode.Off;
        trail.receiveShadows = false;
        var gradient = new Gradient();
        gradient.SetKeys(
            new[]
            {
                new GradientColorKey(new Color(0.05f, 2.6f, 3.2f), 0f),
                new GradientColorKey(new Color(3.0f, 0.04f, 1.8f), 0.48f),
                new GradientColorKey(new Color(2.7f, 2.2f, 0.02f), 1f)
            },
            new[] { new GradientAlphaKey(0.82f, 0f), new GradientAlphaKey(0f, 1f) });
        trail.colorGradient = gradient;
        Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
        if (shader == null) shader = Shader.Find("Unlit/Color");
        trail.material = new Material(shader) { name = "Runtime RGB Death Trail" };
        Object.Destroy(trailObject, duration);
    }
}
