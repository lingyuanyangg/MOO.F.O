using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class PopArtSceneStyler : MonoBehaviour
{
    private readonly List<Material> runtimeMaterials = new List<Material>();

    private static readonly Color DeepPurple = new Color(0.055f, 0.008f, 0.12f, 1f);
    private static readonly Color[] RockColors =
    {
        new Color(0.20f, 0.035f, 0.48f, 1f),
        new Color(0.025f, 0.075f, 0.30f, 1f),
        new Color(0.48f, 0.10f, 0.68f, 1f)
    };
    private static readonly Color[] RockGlowColors =
    {
        new Color(0.05f, 2.8f, 3.8f, 1f),
        new Color(3.4f, 0.025f, 1.65f, 1f),
        new Color(1.45f, 3.2f, 0.015f, 1f),
        new Color(3.2f, 0.75f, 0.02f, 1f),
        new Color(0.35f, 0.05f, 3.8f, 1f)
    };
    private static readonly Color[] FenceColors =
    {
        new Color(1.0f, 0.16f, 0.055f, 1f),
        new Color(1.0f, 0.06f, 0.44f, 1f),
        new Color(1.0f, 0.72f, 0.025f, 1f)
    };

    private void Start()
    {
        ApplyScenePalette();
        ApplyGrassPalette();
    }

    private void OnDestroy()
    {
        foreach (Material material in runtimeMaterials)
            if (material != null) Destroy(material);
    }

    private void ApplyScenePalette()
    {
        Shader toonShader = Shader.Find("Hand2/PopArtToonOutline");
        Shader glassShader = Shader.Find("Hand2/PopArtSpectralGlass");
        if (toonShader == null)
        {
            Debug.LogError("PopArtSceneStyler: PopArtToonOutline shader is missing.");
            return;
        }

        int rockIndex = 0;
        int fenceIndex = 0;
        foreach (Renderer renderer in FindObjectsByType<Renderer>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            string path = GetPath(renderer.transform);
            if (path.Contains("Faceted Meadow Rocks"))
            {
                int styleIndex = rockIndex++;
                ApplyToon(renderer, toonShader, RockColors[styleIndex % RockColors.Length], false, false, RockGlowColors[styleIndex % RockGlowColors.Length], 1.45f, 0.012f, new Color(0.01f,0.025f,0.12f));
            }
            else if (path.Contains("Wooden Arena Fence"))
            {
                ApplyToon(renderer, toonShader, FenceColors[fenceIndex++ % FenceColors.Length], false, false, Color.black, 0f, 0.010f, DeepPurple);
            }
            else if (path.Contains("OSC Cow/Cow Visual"))
            {
                ApplyToon(renderer, toonShader, Color.white, true, false, new Color(1.6f, 0.22f, 0.85f, 1f), 0.22f, 0.0020f, new Color(0.38f,0.02f,0.72f));
            }
            else if (path.Contains("OSC UFO/UFO Visual"))
            {
                if (IsUfoGlass(renderer) && glassShader != null)
                    ApplyUfoGlass(renderer, glassShader);
                else
                    ApplyToon(renderer, toonShader, new Color(0.05f, 0.82f, 1.0f, 1f), false, false, new Color(3.0f, 0.04f, 1.5f, 1f), 1.15f, 0.0025f, new Color(1.5f,0.015f,0.72f));
            }
            else if (path.Contains("OSC Alien/Alien Visual"))
            {
                ApplyToon(renderer, toonShader, new Color(0.62f, 1.0f, 0.05f, 1f), false, false, new Color(0.1f, 2.1f, 2.8f, 1f), 0.7f, 0.008f, new Color(0.01f,1.4f,1.8f));
            }
            else if (renderer.name == "Grass Soil Ground")
            {
                ApplyToon(renderer, toonShader, Color.white, false, true, Color.black, 0f, 0f, DeepPurple);
            }
        }
    }

    private static bool IsUfoGlass(Renderer renderer)
    {
        if (renderer.name == "Cylinder.001") return true;
        foreach (Material material in renderer.sharedMaterials)
        {
            if (material == null) continue;
            string materialName = material.name.ToLowerInvariant();
            if (material.renderQueue >= 3000 || materialName.Contains("glass")) return true;
        }
        return false;
    }

    private void ApplyUfoGlass(Renderer renderer, Shader shader)
    {
        Material[] sources = renderer.sharedMaterials;
        Material[] replacements = new Material[sources.Length];
        for (int i = 0; i < sources.Length; i++)
        {
            Material source = sources[i];
            Material glass = new Material(shader) { name = "Runtime Spectral UFO Glass" };
            runtimeMaterials.Add(glass);
            if (source != null)
            {
                if (source.HasProperty("_BaseMap"))
                {
                    glass.SetTexture("_BaseMap", source.GetTexture("_BaseMap"));
                    glass.SetTextureScale("_BaseMap", source.GetTextureScale("_BaseMap"));
                    glass.SetTextureOffset("_BaseMap", source.GetTextureOffset("_BaseMap"));
                }
                else if (source.HasProperty("_MainTex"))
                    glass.SetTexture("_BaseMap", source.GetTexture("_MainTex"));
            }
            glass.SetColor("_BaseColor", Color.white);
            glass.SetColor("_GlassTint", new Color(0.025f, 0.88f, 1.0f, 1f));
            glass.SetColor("_FresnelColor", new Color(2.8f, 0.035f, 1.55f, 1f));
            glass.SetFloat("_GlassOpacity", 0.12f);
            glass.SetFloat("_TextureOpacity", 0.9f);
            glass.SetFloat("_FresnelPower", 2.7f);
            glass.SetFloat("_FresnelStrength", 0.75f);
            glass.SetFloat("_FresnelOpacity", 0.2f);
            replacements[i] = glass;
        }
        renderer.materials = replacements;
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        renderer.receiveShadows = false;
    }

    private void ApplyGrassPalette()
    {
        foreach (Renderer renderer in FindObjectsByType<Renderer>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            foreach (Material material in renderer.materials)
            {
                if (material == null || material.shader == null || material.shader.name != "Hand2/StylizedWindGrass") continue;
                material.SetColor("_BottomColor", new Color(0.08f, 0.46f, 0.025f, 1f));
                material.SetColor("_TopColor", new Color(0.62f, 1.18f, 0.035f, 1f));
                material.SetColor("_NearTint", new Color(0.60f, 1.24f, 0.08f, 1f));
                material.SetColor("_FarTint", new Color(1.20f, 1.12f, 0.035f, 1f));
                material.SetFloat("_DistanceGradientStart", 7f);
                material.SetFloat("_DistanceGradientEnd", 34f);
                material.SetFloat("_ToonSteps", 3f);
            }
        }
    }

    private void ApplyToon(Renderer renderer, Shader shader, Color tint, bool duotone, bool gradient, Color fresnel, float fresnelStrength, float outlineWidth, Color outlineColor)
    {
        Material[] sources = renderer.sharedMaterials;
        Material[] replacements = new Material[sources.Length];
        for (int i = 0; i < sources.Length; i++)
        {
            Material source = sources[i];
            Material material = new Material(shader) { name = "Runtime Pop Toon - " + renderer.name };
            runtimeMaterials.Add(material);
            if (source != null)
            {
                if (source.HasProperty("_BaseMap"))
                {
                    material.SetTexture("_BaseMap", source.GetTexture("_BaseMap"));
                    material.SetTextureScale("_BaseMap", source.GetTextureScale("_BaseMap"));
                    material.SetTextureOffset("_BaseMap", source.GetTextureOffset("_BaseMap"));
                }
                else if (source.HasProperty("_MainTex"))
                    material.SetTexture("_BaseMap", source.GetTexture("_MainTex"));
            }
            bool colorizeTexture = !gradient && !duotone;
            material.SetColor("_BaseColor", colorizeTexture ? Color.white : tint);
            material.SetColor("_ShadowColor", new Color(0.29f, 0.065f, 0.46f, 1f));
            material.SetFloat("_ToonSteps", 3f);
            material.SetFloat("_UseDuotone", duotone || colorizeTexture ? 1f : 0f);
            material.SetColor("_DarkColor", colorizeTexture
                ? Color.Lerp(DeepPurple, tint, 0.62f)
                : new Color(0.045f, 0.008f, 0.105f, 1f));
            material.SetColor("_LightColor", colorizeTexture
                ? tint * 1.22f
                : new Color(1.0f, 0.76f, 0.88f, 1f));
            material.SetFloat("_UseGradient", gradient ? 1f : 0f);
            material.SetColor("_NearColor", new Color(0.20f, 1.15f, 0.04f, 1f));
            material.SetColor("_FarColor", new Color(1.05f, 1.22f, 0.025f, 1f));
            material.SetFloat("_GradientStart", 7f);
            material.SetFloat("_GradientEnd", 38f);
            material.SetColor("_FresnelColor", fresnel);
            material.SetFloat("_FresnelPower", 3f);
            material.SetFloat("_FresnelStrength", fresnelStrength);
            material.SetColor("_OutlineColor", outlineColor);
            material.SetFloat("_OutlineWidth", outlineWidth);
            replacements[i] = material;
        }
        renderer.materials = replacements;
    }

    public static void StylePoop(GameObject root)
    {
        if (root == null) return;
        Shader toon = Shader.Find("Hand2/PopArtToonOutline");
        if (toon == null) return;
        foreach (Renderer renderer in root.GetComponentsInChildren<Renderer>(true))
        {
            Material material = new Material(toon) { name = "Runtime Chocolate Acid Poop" };
            Material source = renderer.sharedMaterial;
            if (source != null && source.HasProperty("_BaseMap")) material.SetTexture("_BaseMap", source.GetTexture("_BaseMap"));
            material.SetColor("_BaseColor", new Color(0.34f, 0.075f, 0.018f, 1f));
            material.SetColor("_ShadowColor", new Color(0.10f, 0.012f, 0.12f, 1f));
            material.SetFloat("_ToonSteps", 2f);
            material.SetColor("_FresnelColor", new Color(1.15f, 2.8f, 0.01f, 1f));
            material.SetFloat("_FresnelPower", 2.2f);
            material.SetFloat("_FresnelStrength", 1.15f);
            material.SetColor("_OutlineColor", DeepPurple);
            material.SetFloat("_OutlineWidth", 0.012f);
            renderer.material = material;
        }
    }

    private static string GetPath(Transform transform)
    {
        string path = transform.name;
        while (transform.parent != null)
        {
            transform = transform.parent;
            path = transform.name + "/" + path;
        }
        return path;
    }
}
