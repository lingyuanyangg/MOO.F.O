using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

[DisallowMultipleComponent]
public class ExperimentalSpectralDirector : MonoBehaviour
{
    private static ExperimentalSpectralDirector instance;
    public float skyIntensity = 1.25f;
    public float glitchStrength = 0.72f;
    public float lightCycleSpeed = 0.23f;
    public float skyNoiseSeed = 13.37f;
    public float lightNoiseSeed = 71.91f;
    [Header("Exposure Guard")]
    public float bloomIntensity = 0.82f;
    public float bloomThreshold = 1.05f;
    public float bloomClamp = 5.5f;
    public float postExposure = -0.28f;
    private readonly List<Material> materials = new List<Material>();
    private Light keyLight;
    private Camera mainCamera;
    private Material glitchMaterial;
    private float hitGlitch;
    private Vector2 hitCenter = new Vector2(0.5f,0.5f);

    private void Start()
    {
        instance = this;
        Camera camera = Camera.main;
        mainCamera = camera;
        if (camera != null)
        {
            CreateCameraQuad(camera, "Spectral Sky Curtain", "Hand2/SpectralSky", 80f, skyIntensity, false);
            CreateCameraQuad(camera, "Spectral Scan Glitch", "Hand2/SpectralGlitchOverlay", camera.nearClipPlane + 0.035f, glitchStrength, true);
        }
        foreach (Light light in FindObjectsByType<Light>(FindObjectsInactive.Include))
            if (light.type == LightType.Directional) { keyLight = light; break; }
        RenderSettings.ambientMode = AmbientMode.Trilight;
        RenderSettings.ambientSkyColor = new Color(0.10f,0.32f,0.55f);
        RenderSettings.ambientEquatorColor = new Color(0.42f,0.06f,0.50f);
        RenderSettings.ambientGroundColor = new Color(0.16f,0.015f,0.30f);
        foreach (Rigidbody body in FindObjectsByType<Rigidbody>(FindObjectsInactive.Include))
            if ((body.GetComponentInParent<OSCCowController>() != null || body.GetComponentInParent<OSCUFOController>() != null || body.GetComponentInParent<OSCAlienController>() != null) && body.GetComponent<SpectralMotionTrails>() == null)
                body.gameObject.AddComponent<SpectralMotionTrails>();
        ConfigureExposureGuard();
    }

    private void Update()
    {
        if (keyLight != null)
        {
            float noise = ValueNoise(Time.time * lightCycleSpeed, lightNoiseSeed);
            float secondNoise = ValueNoise(Time.time * lightCycleSpeed * 0.41f, lightNoiseSeed + 29.4f);
            float phase = Mathf.Repeat(noise * 2.25f + secondNoise * 1.35f, 3f);
            Color lemon = new Color(1f,0.92f,0.13f);
            Color cyan = new Color(0.05f,0.88f,1f);
            Color pink = new Color(1f,0.04f,0.62f);
            keyLight.color = phase < 1f ? Color.Lerp(lemon,cyan,Smooth(phase)) : phase < 2f ? Color.Lerp(cyan,pink,Smooth(phase-1f)) : Color.Lerp(pink,lemon,Smooth(phase-2f));
            // Keep the irregular colour cycling without letting bright phases wash out the frame.
            keyLight.intensity = Mathf.Lerp(0.82f,1.12f,secondNoise);
        }
        hitGlitch = Mathf.MoveTowards(hitGlitch,0f,Time.unscaledDeltaTime*7f);
        if (glitchMaterial != null)
        {
            glitchMaterial.SetFloat("_HitStrength",hitGlitch);
            glitchMaterial.SetVector("_HitCenter",new Vector4(hitCenter.x,hitCenter.y,0,0));
        }
    }

    private static float Smooth(float t) => t*t*(3f-2f*t);

    private static float ValueNoise(float time, float seed)
    {
        float cell=Mathf.Floor(time); float f=Smooth(time-cell);
        float a=Mathf.Repeat(Mathf.Sin((cell+seed)*78.233f)*43758.5453f,1f);
        float b=Mathf.Repeat(Mathf.Sin((cell+1f+seed)*78.233f)*43758.5453f,1f);
        return Mathf.Lerp(a,b,f);
    }

    public static void NotifyHit(Vector3 worldPosition)
    {
        if (instance == null) return;
        instance.hitGlitch = 1f;
        if (instance.mainCamera != null)
        {
            Vector3 p=instance.mainCamera.WorldToViewportPoint(worldPosition);
            instance.hitCenter=new Vector2(Mathf.Clamp01(p.x),Mathf.Clamp01(p.y));
        }
    }

    private void CreateCameraQuad(Camera camera, string name, string shaderName, float distance, float strength, bool overlay)
    {
        Material template = Resources.Load<Material>(overlay ? "Spectral/ScanOverlay" : "Spectral/SkyCurtain");
        Shader shader = template != null ? template.shader : Shader.Find(shaderName);
        if (shader == null || !shader.isSupported)
        {
            Debug.LogError("Spectral effect unavailable: " + shaderName);
            return;
        }
        GameObject quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
        quad.name = name;
        Destroy(quad.GetComponent<Collider>());
        quad.transform.SetParent(camera.transform,false);
        quad.transform.localPosition = new Vector3(0,0,distance);
        quad.transform.localRotation = Quaternion.identity;
        float height = 2f*distance*Mathf.Tan(camera.fieldOfView*0.5f*Mathf.Deg2Rad);
        quad.transform.localScale = new Vector3(height*camera.aspect*1.06f,height*1.06f,1f);
        Material material = template != null ? new Material(template) : new Material(shader);
        material.name = "Runtime " + name;
        if (material.HasProperty("_Intensity")) material.SetFloat("_Intensity",strength);
        if (material.HasProperty("_Strength")) material.SetFloat("_Strength",strength);
        if (material.HasProperty("_NoiseSeed")) material.SetFloat("_NoiseSeed",skyNoiseSeed);
        if (shaderName == "Hand2/SpectralGlitchOverlay") glitchMaterial = material;
        quad.GetComponent<Renderer>().material = material;
        quad.GetComponent<Renderer>().allowOcclusionWhenDynamic = false;
        Debug.Log("Spectral effect ready: " + name + " shader=" + shader.name);
        materials.Add(material);
    }

    private void ConfigureExposureGuard()
    {
        Volume volume = FindAnyObjectByType<Volume>();
        if (volume == null || volume.profile == null) return;

        if (!volume.profile.TryGet(out Bloom bloom))
            bloom = volume.profile.Add<Bloom>(true);
        bloom.active = true;
        bloom.intensity.Override(bloomIntensity);
        bloom.threshold.Override(bloomThreshold);
        bloom.scatter.Override(0.68f);
        bloom.clamp.Override(bloomClamp);

        if (!volume.profile.TryGet(out ColorAdjustments colorAdjustments))
            colorAdjustments = volume.profile.Add<ColorAdjustments>(true);
        colorAdjustments.active = true;
        colorAdjustments.postExposure.Override(postExposure);
        colorAdjustments.contrast.Override(8f);
        colorAdjustments.saturation.Override(6f);

        if (!volume.profile.TryGet(out Tonemapping tonemapping))
            tonemapping = volume.profile.Add<Tonemapping>(true);
        tonemapping.active = true;
        tonemapping.mode.Override(TonemappingMode.ACES);
    }

    private void OnDestroy(){ if(instance==this)instance=null; foreach(Material m in materials) if(m!=null) Destroy(m); }
}
