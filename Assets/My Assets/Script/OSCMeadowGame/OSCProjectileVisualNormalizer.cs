using System.Collections;
using UnityEngine;

[DisallowMultipleComponent]
public class OSCProjectileVisualNormalizer : MonoBehaviour
{
    private Transform projectileRoot;
    private float targetSize = 1.35f;

    public void Initialize(Transform root, float size)
    {
        projectileRoot = root;
        targetSize = Mathf.Max(0.1f, size);
    }

    private IEnumerator Start()
    {
        yield return null;
        NormalizeRenderedBounds();
        yield return null;
        NormalizeRenderedBounds();
        Destroy(this);
    }

    private void NormalizeRenderedBounds()
    {
        Renderer[] renderers = GetComponentsInChildren<Renderer>(true);
        if (renderers.Length == 0 || projectileRoot == null) return;

        Bounds bounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++)
            bounds.Encapsulate(renderers[i].bounds);

        float largest = Mathf.Max(bounds.size.x, bounds.size.y, bounds.size.z);
        if (largest <= 0.0001f) return;

        transform.localScale *= targetSize / largest;

        bounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++)
            bounds.Encapsulate(renderers[i].bounds);
        transform.position += projectileRoot.position - bounds.center;
    }
}
