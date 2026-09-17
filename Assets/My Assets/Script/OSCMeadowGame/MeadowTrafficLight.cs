using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class MeadowTrafficLight : MonoBehaviour
{
    public enum Signal { Red, Yellow, Green }
    public OSCMeadowGameManager gameManager;
    public Renderer lampRenderer;
    public int lampMaterialIndex;
    public Transform laserOrigin;
    public Material lampMaterial;
    public Material laserMaterial;
    [Header("Timing")]
    [Min(0.1f)] public float chanceInterval = 5f;
    [Range(0f, 1f)] public float redChance = 0.1f;
    [Min(0.1f)] public float yellowDuration = 1f;
    [Min(0.1f)] public float minimumRedDuration = 1f;
    [Min(0.1f)] public float maximumRedDuration = 3f;
    [Range(0f, 1f)] public float shortRedDurationChance = 0.7f;
    [Header("Penalty")]
    [Min(0f)] public float laserDamage = 10f;
    [Min(0.1f)] public float penaltyCooldown = 0.8f;
    public Signal CurrentSignal { get; private set; } = Signal.Green;
    public static MeadowTrafficLight Instance { get; private set; }
    public static bool IsRed => Instance != null && Instance.isActiveAndEnabled && Instance.Running && Instance.CurrentSignal == Signal.Red;
    bool Running => gameManager != null && gameManager.IsMatchStarted && !gameManager.IsMatchOver;
    readonly Dictionary<int, float> nextPenalty = new Dictionary<int, float>();
    readonly List<GameObject> beams = new List<GameObject>();
    Material runtimeLamp;
    MaterialPropertyBlock lampProperties;
    Material[] originalMaterials;
    bool wasRunning;
    float nextTransition;

    void Awake()
    {
        Instance = this;
        if (gameManager == null) gameManager = FindFirstObjectByType<OSCMeadowGameManager>();
        if (lampRenderer != null && lampMaterial != null)
        {
            originalMaterials = lampRenderer.sharedMaterials;
            var materials = (Material[])originalMaterials.Clone();
            runtimeLamp = new Material(lampMaterial);
            materials[lampMaterialIndex] = runtimeLamp;
            lampRenderer.sharedMaterials = materials;
        }
        ResetSignal();
    }

    public void ResetSignal()
    {
        StopAllCoroutines();
        foreach (var beam in beams) if (beam != null) Destroy(beam);
        beams.Clear();
        nextPenalty.Clear();
        SetSignal(Signal.Green);
        nextTransition = Time.time + Mathf.Max(0.1f, chanceInterval);
        wasRunning = false;
    }

    void Update()
    {
        if (!Running)
        {
            if (wasRunning) ResetSignal();
            return;
        }
        if (!wasRunning)
        {
            nextTransition = Time.time + Mathf.Max(0.1f, chanceInterval);
            wasRunning = true;
        }
        if (Time.time < nextTransition) return;
        if (CurrentSignal == Signal.Green)
        {
            if (Random.value < redChance)
            {
                SetSignal(Signal.Yellow);
                nextTransition = Time.time + Mathf.Max(0.1f, yellowDuration);
            }
            else nextTransition = Time.time + Mathf.Max(0.1f, chanceInterval);
        }
        else if (CurrentSignal == Signal.Yellow)
        {
            SetSignal(Signal.Red);
            nextTransition = Time.time + SampleRedDuration();
            var alien = FindFirstObjectByType<OSCAlienController>();
            if (alien != null && !alien.IsRespawning) alien.CancelTrafficAttack();
        }
        else
        {
            SetSignal(Signal.Green);
            nextTransition = Time.time + Mathf.Max(0.1f, chanceInterval);
        }
    }

    public float SampleRedDuration()
    {
        float minimum = Mathf.Max(0.1f, minimumRedDuration);
        float maximum = Mathf.Max(minimum, maximumRedDuration);
        float midpoint = Mathf.Lerp(minimum, maximum, 0.5f);
        return Random.value < shortRedDurationChance
            ? Random.Range(minimum, midpoint)
            : Random.Range(midpoint, maximum);
    }

    void SetSignal(Signal signal)
    {
        CurrentSignal = signal;
        if (runtimeLamp != null) runtimeLamp.SetFloat("_SignalIndex", (int)signal);
        if (lampRenderer != null)
        {
            Material[] activeMaterials = lampRenderer.sharedMaterials;
            if (lampMaterialIndex >= 0 && lampMaterialIndex < activeMaterials.Length &&
                activeMaterials[lampMaterialIndex] != null)
                activeMaterials[lampMaterialIndex].SetFloat("_SignalIndex", (int)signal);
            if (lampProperties == null) lampProperties = new MaterialPropertyBlock();
            lampRenderer.GetPropertyBlock(lampProperties, lampMaterialIndex);
            lampProperties.SetFloat("_SignalIndex", (int)signal);
            lampRenderer.SetPropertyBlock(lampProperties, lampMaterialIndex);
        }
    }

    // Check raw input before the controller consumes it. Neutral OSC packets are not violations.
    public static bool Block(MonoBehaviour actor, bool attempted)
    {
        if (!IsRed) return false;
        if (attempted && actor != null && actor.isActiveAndEnabled &&
            actor is IOSCCombatant combatant && !combatant.IsRespawning && combatant.Health > 0f)
            Instance.Punish(actor, combatant);
        return true;
    }

    public static void Hold(Rigidbody body)
    {
        if (body == null) return;
        var velocity = body.linearVelocity;
        body.linearVelocity = new Vector3(0f, body.useGravity ? velocity.y : 0f, 0f);
        body.angularVelocity = Vector3.zero;
    }

    void Punish(MonoBehaviour actor, IOSCCombatant target)
    {
        int id = actor.GetInstanceID();
        if (nextPenalty.TryGetValue(id, out float next) && Time.time < next) return;
        nextPenalty[id] = Time.time + Mathf.Max(0.1f, penaltyCooldown);
        Vector3 origin = laserOrigin != null ? laserOrigin.position : transform.position;
        Vector3 end = target.CombatCollider != null
            ? target.CombatCollider.ClosestPoint(origin) : target.CombatTransform.position;
        StartCoroutine(FlashLaser(origin, end));
        target.ReceiveCombatDamage(laserDamage, this);
    }

    IEnumerator FlashLaser(Vector3 origin, Vector3 end)
    {
        var beam = new GameObject("Traffic Penalty Laser");
        beams.Add(beam);
        var line = beam.AddComponent<LineRenderer>();
        line.sharedMaterial = laserMaterial;
        line.useWorldSpace = true;
        line.positionCount = 2;
        line.SetPositions(new[] { origin, end });
        line.numCapVertices = 6;
        line.startWidth = line.endWidth = 0.15f;
        float elapsed = 0f;
        while (elapsed < 0.22f && Running)
        {
            elapsed += Time.deltaTime;
            line.startWidth = line.endWidth = Mathf.Lerp(0.15f, 0.01f, elapsed / 0.22f);
            yield return null;
        }
        beams.Remove(beam);
        Destroy(beam);
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
        foreach (var beam in beams) if (beam != null) Destroy(beam);
        if (lampRenderer != null && originalMaterials != null) lampRenderer.sharedMaterials = originalMaterials;
        if (runtimeLamp != null) Destroy(runtimeLamp);
    }
}
