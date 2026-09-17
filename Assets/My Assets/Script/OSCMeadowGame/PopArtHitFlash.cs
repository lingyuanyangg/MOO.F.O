using UnityEngine;

[DisallowMultipleComponent]
public class PopArtHitFlash : MonoBehaviour
{
    public Color flashColor = new Color(3.2f, 0.12f, 1.85f, 1f);
    public float flashDuration = 0.14f;

    private Renderer[] renderers;
    private MaterialPropertyBlock[] originalBlocks;
    private MaterialPropertyBlock workingBlock;
    private float remaining;
    private bool applied;

    private void Awake()
    {
        renderers = GetComponentsInChildren<Renderer>(true);
        originalBlocks = new MaterialPropertyBlock[renderers.Length];
        for (int i = 0; i < renderers.Length; i++)
        {
            originalBlocks[i] = new MaterialPropertyBlock();
            renderers[i].GetPropertyBlock(originalBlocks[i]);
        }
        workingBlock = new MaterialPropertyBlock();
    }

    public void Trigger()
    {
        remaining = Mathf.Max(0.02f, flashDuration);
        ExperimentalSpectralDirector.NotifyHit(transform.position);
    }

    private void LateUpdate()
    {
        if (remaining > 0f)
        {
            remaining -= Time.deltaTime;
            float pulse = Mathf.Clamp01(remaining / Mathf.Max(0.02f, flashDuration));
            // A bright hit remains readable, but no longer feeds an extreme white value into Bloom.
            Color current = Color.Lerp(Color.white * 1.15f, flashColor * 0.72f, pulse);

            for (int i = 0; i < renderers.Length; i++)
            {
                Renderer target = renderers[i];
                if (target == null) continue;
                target.GetPropertyBlock(workingBlock);
                workingBlock.SetColor("_BaseColor", current);
                workingBlock.SetColor("_Color", current);
                target.SetPropertyBlock(workingBlock);
                workingBlock.Clear();
            }
            applied = true;
        }
        else if (applied)
        {
            RestoreOriginalBlocks();
        }
    }

    private void OnDisable()
    {
        RestoreOriginalBlocks();
    }

    private void RestoreOriginalBlocks()
    {
        if (renderers == null) return;
        for (int i = 0; i < renderers.Length; i++)
            if (renderers[i] != null)
                renderers[i].SetPropertyBlock(originalBlocks[i]);
        applied = false;
    }
}
