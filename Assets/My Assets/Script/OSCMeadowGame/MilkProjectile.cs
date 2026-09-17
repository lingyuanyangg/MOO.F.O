using UnityEngine;

[DisallowMultipleComponent]
public class MilkProjectile : MonoBehaviour
{
    public Transform target;
    public float speed = 23f;
    public float turnRate = 9f;
    public float damage = 14f;
    public float lifeTime = 1.2f;

    private Rigidbody body;
    private Collider targetCollider;
    private Vector3 travelDirection;
    private Vector3 streamOrigin;
    private LineRenderer streamCore;
    private LineRenderer streamGlow;
    private Material coreMaterial;
    private Material glowMaterial;
    private bool resolved;
    private MonoBehaviour targetBehaviour;
    private MonoBehaviour attacker;

    private void Awake()
    {
        body = GetComponent<Rigidbody>();
        if (body == null) body = gameObject.AddComponent<Rigidbody>();
        body.useGravity = false;
        body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        body.interpolation = RigidbodyInterpolation.Interpolate;

        var sphere = GetComponent<SphereCollider>();
        if (sphere != null) sphere.radius = 0.42f;

        Destroy(gameObject, lifeTime);
    }

    public void Initialize(
        IOSCCombatant lockedTarget,
        MonoBehaviour attackOwner,
        float projectileSpeed,
        float hitDamage,
        Vector3 origin)
    {
        targetBehaviour = lockedTarget as MonoBehaviour;
        attacker = attackOwner;
        target = lockedTarget != null ? lockedTarget.CombatTransform : null;
        targetCollider = lockedTarget != null ? lockedTarget.CombatCollider : null;
        speed = projectileSpeed;
        damage = hitDamage;
        streamOrigin = origin;
        travelDirection = target != null
            ? (GetTargetPoint() - transform.position).normalized
            : transform.forward;
        SetupWaterColumn();
    }

    private void SetupWaterColumn()
    {
        streamCore = gameObject.AddComponent<LineRenderer>();
        ConfigureLine(streamCore, 0.46f, 0.34f, new Color(1f, 1f, 1f, 1f), false);

        var glowObject = new GameObject("Milk Column Glow");
        glowObject.transform.SetParent(transform, false);
        streamGlow = glowObject.AddComponent<LineRenderer>();
        ConfigureLine(streamGlow, 0.82f, 0.58f, new Color(0.18f, 1.75f, 3.2f, 0.34f), true);

        UpdateWaterColumn();
    }

    private void ConfigureLine(
        LineRenderer line,
        float startWidth,
        float endWidth,
        Color color,
        bool additive)
    {
        line.useWorldSpace = true;
        line.positionCount = 2;
        line.alignment = LineAlignment.View;
        line.numCapVertices = 6;
        line.numCornerVertices = 4;
        line.textureMode = LineTextureMode.Stretch;
        line.startWidth = startWidth;
        line.endWidth = endWidth;
        line.startColor = color;
        line.endColor = color;
        line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        line.receiveShadows = false;

        Shader shader = Shader.Find("Hand2/SpectralEnergyLine");
        if (shader == null) shader = Shader.Find("Universal Render Pipeline/Unlit");
        var material = new Material(shader);
        Color hdr = additive
            ? new Color(0.18f, 2.7f, 3.8f, color.a)
            : new Color(3.2f, 2.9f, 3.15f, 1f);
        material.SetColor("_BaseColor", hdr);
        if (material.HasProperty("_PulseSpeed")) material.SetFloat("_PulseSpeed", additive ? 8.5f : 5.5f);
        if (material.HasProperty("_BandScale")) material.SetFloat("_BandScale", additive ? 26f : 18f);

        if (additive)
        {
            material.SetFloat("_Surface", 1f);
            material.SetFloat("_Blend", 1f);
            material.SetFloat("_SrcBlend", 5f);
            material.SetFloat("_DstBlend", 1f);
            material.SetFloat("_ZWrite", 0f);
            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.renderQueue = 3000;
            glowMaterial = material;
        }
        else
        {
            coreMaterial = material;
        }

        line.material = material;
    }

    private Vector3 GetTargetPoint()
    {
        if (targetCollider != null) return targetCollider.bounds.center;
        return target != null ? target.position : transform.position + travelDirection;
    }

    private void FixedUpdate()
    {
        if (resolved) return;

        if (target != null)
        {
            Vector3 targetPoint = GetTargetPoint();
            Vector3 desired = (targetPoint - transform.position).normalized;
            if (travelDirection.sqrMagnitude < 0.01f) travelDirection = desired;
            travelDirection = Vector3.RotateTowards(
                travelDirection,
                desired,
                turnRate * Time.fixedDeltaTime,
                0f).normalized;

            if (Vector3.Distance(transform.position, targetPoint) < 1.15f)
            {
                ResolveHit();
                return;
            }
        }

        body.linearVelocity = travelDirection * speed;
    }

    private void LateUpdate()
    {
        if (!resolved) UpdateWaterColumn();
    }

    private void UpdateWaterColumn()
    {
        if (streamCore == null || streamGlow == null) return;

        Vector3 tip = transform.position;
        Vector3 tail = tip - Vector3.ClampMagnitude(tip - streamOrigin, 2.4f);
        float ripple = 1f + Mathf.Sin(Time.time * 34f) * 0.08f;
        streamCore.startWidth = 0.46f * ripple;
        streamCore.endWidth = 0.34f * ripple;
        streamGlow.startWidth = 0.82f * ripple;
        streamGlow.endWidth = 0.58f * ripple;
        streamCore.SetPosition(0, tail);
        streamCore.SetPosition(1, tip);
        streamGlow.SetPosition(0, tail);
        streamGlow.SetPosition(1, tip);
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (resolved) return;

        if (target != null &&
            (collision.transform == target || collision.transform.IsChildOf(target)))
        {
            ResolveHit();
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private void ResolveHit()
    {
        if (resolved) return;
        resolved = true;

        Vector3 splashPoint = targetCollider != null
            ? targetCollider.ClosestPoint(transform.position)
            : transform.position;

        if (targetBehaviour is IOSCCombatant combatant && !combatant.IsRespawning)
            combatant.ReceiveCombatDamage(damage, attacker);

        SpawnSplash(splashPoint, -travelDirection);
        PopArtBurstSymbol.Spawn(splashPoint, -travelDirection);

        if (streamCore != null) streamCore.enabled = false;
        if (streamGlow != null) streamGlow.enabled = false;
        Destroy(gameObject);
    }

    private static void SpawnSplash(Vector3 position, Vector3 normal)
    {
        var splash = new GameObject("Milk Impact Splash");
        splash.SetActive(false);
        splash.transform.position = position;
        splash.transform.rotation = normal.sqrMagnitude > 0.01f
            ? Quaternion.LookRotation(normal)
            : Quaternion.identity;

        var particles = splash.AddComponent<ParticleSystem>();
        var main = particles.main;
        main.duration = 0.35f;
        main.loop = false;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.45f, 0.9f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(2.5f, 6.5f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.12f, 0.34f);
        main.startColor = new ParticleSystem.MinMaxGradient(
            new Color(0.08f, 2.1f, 3.4f, 0.95f),
            new Color(3.1f, 0.18f, 1.75f, 1f));
        main.gravityModifier = 1.15f;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = 48;

        var emission = particles.emission;
        emission.rateOverTime = 0f;
        emission.SetBursts(new[]
        {
            new ParticleSystem.Burst(0f, 34)
        });

        var shape = particles.shape;
        shape.shapeType = ParticleSystemShapeType.Cone;
        shape.angle = 42f;
        shape.radius = 0.22f;
        shape.length = 0.15f;

        var particleRenderer = particles.GetComponent<ParticleSystemRenderer>();
        particleRenderer.renderMode = ParticleSystemRenderMode.Billboard;
        Shader shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
        if (shader == null) shader = Shader.Find("Universal Render Pipeline/Unlit");
        var splashMaterial = new Material(shader);
        splashMaterial.SetColor(
            "_BaseColor",
            new Color(0.35f, 2.8f, 3.8f, 1f));
        particleRenderer.material = splashMaterial;

        splash.SetActive(true);
        particles.Play();
        Destroy(splash, 1.6f);
    }

    private void OnDestroy()
    {
        if (coreMaterial != null) Destroy(coreMaterial);
        if (glowMaterial != null) Destroy(glowMaterial);
    }
}
