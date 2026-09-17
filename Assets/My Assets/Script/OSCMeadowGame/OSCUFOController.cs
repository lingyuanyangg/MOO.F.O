using System.Collections;
using UnityEngine;
using UnityEngine.Rendering;
using extOSC;

[DisallowMultipleComponent]
[RequireComponent(typeof(Rigidbody), typeof(SphereCollider))]
public class OSCUFOController : MonoBehaviour, IOSCCombatant
{
    [Header("OSC")]
    public OSCReceiver receiver;
    public string moveXAddress = "/tabletX";
    public string moveYAddress = "/tabletY";
    public string pressureAddress = "/tablePressure";
    public string alternatePressureAddress = "/tabletPressure";

    [Header("Scene References")]
    public Transform visual;
    public Transform cowTarget;
    public Collider groundCollider;
    public LineRenderer beamLine;
    public OSCMeadowGameManager gameManager;

    [Header("Flight")]
    public float acceleration = 14f;
    public float maximumSpeed = 10f;
    public float hoverHeight = 6.2f;
    public float hoverSpring = 7.5f;
    public float hoverDamping = 4.5f;
    public float neutralDeadZone = 0.08f;
    public float groundEdgePadding = 2.25f;

    [Header("Beam / Heat")]
    public float maximumHealth = 100f;
    public float heatPerSecond = 38f;
    public float coolingPerSecond = 23f;
    public float restartHeat = 28f;
    public float beamDamagePerSecond = 18f;
    public float maximumBeamDistance = 45f;
    public float pressureThreshold = 0.5f;

    [Header("Poop Drop")]
    public GameObject poopPrefab;
    [Range(0f, 1f)] public float poopDropChance = 0.2f;
    public float poopVisualSize = 0.9f;
    public float poopDamage = 14f;
    public float poopBlastRadius = 2.25f;
    public float poopDropOffset = 1.1f;

    [Header("Hit Feedback")]
    public float hitKnockbackSpeed = 1.35f;
    public float hitKnockbackCooldown = 0.16f;

    public float Health { get; private set; } = 100f;
    public float Heat { get; private set; }
    public bool IsOverheated { get; private set; }
    public bool IsBeamActive { get; private set; }
    public bool IsRespawning { get; private set; }
    public Transform CombatTransform => transform;
    public Collider CombatCollider => sphereCollider;

    private Rigidbody body;
    private SphereCollider sphereCollider;
    private Collider cowCollider;
    private LineRenderer beamGlowLine;
    private Light beamImpactLight;
    private float inputX = 0.5f;
    private float inputY = 0.5f;
    private float pressure;
    private Vector3 spawnPosition;
    private Quaternion spawnRotation;
    private Vector3 visualBaseScale;
    private float hitPulse;
    private float nextHitKnockbackTime;
    private IOSCCombatant beamTarget;
    private bool beamWasFiring;
    private PopArtHitFlash hitFlash;
    private Material spectralBeamMaterial;
    private Material spectralBeamGlowMaterial;

    private OSCBind moveXBind;
    private OSCBind moveYBind;
    private OSCBind pressureBind;
    private OSCBind alternatePressureBind;

    private void Awake()
    {
        body = GetComponent<Rigidbody>();
        sphereCollider = GetComponent<SphereCollider>();
        hitFlash = GetComponent<PopArtHitFlash>();
        if (hitFlash == null) hitFlash = gameObject.AddComponent<PopArtHitFlash>();
        body.useGravity = false;
        body.mass = 2.4f;
        body.interpolation = RigidbodyInterpolation.Interpolate;
        body.collisionDetectionMode = CollisionDetectionMode.Continuous;
        body.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;
        sphereCollider.radius = 1.75f;
        sphereCollider.center = Vector3.zero;
        Health = maximumHealth;
        spawnPosition = transform.position;
        spawnRotation = transform.rotation;
    }

    private void Start()
    {
        if (visual != null) visualBaseScale = visual.localScale;
        if (receiver == null) receiver = FindFirstObjectByType<OSCReceiver>();
        if (gameManager == null) gameManager = FindFirstObjectByType<OSCMeadowGameManager>();
        if (cowTarget != null) cowCollider = cowTarget.GetComponentInChildren<Collider>();

        if (receiver == null)
        {
            Debug.LogError("OSCUFOController: OSCReceiver not found.");
            enabled = false;
            return;
        }

        moveXBind = receiver.Bind(moveXAddress, message => inputX = Read01(message, inputX));
        moveYBind = receiver.Bind(moveYAddress, message => inputY = Read01(message, inputY));
        pressureBind = receiver.Bind(pressureAddress, message => pressure = Read01(message, pressure));
        if (!string.IsNullOrEmpty(alternatePressureAddress) && alternatePressureAddress != pressureAddress)
            alternatePressureBind = receiver.Bind(alternatePressureAddress, message => pressure = Read01(message, pressure));

        SetupBeamVisuals();
        SetBeam(false, transform.position, 0f);
    }

    private void OnDestroy()
    {
        if (receiver != null)
        {
            receiver.Unbind(moveXBind);
            receiver.Unbind(moveYBind);
            receiver.Unbind(pressureBind);
            if (alternatePressureBind != null) receiver.Unbind(alternatePressureBind);
        }
        if (spectralBeamMaterial != null) Destroy(spectralBeamMaterial);
        if (spectralBeamGlowMaterial != null) Destroy(spectralBeamGlowMaterial);
    }

    private static float Read01(OSCMessage message, float fallback)
    {
        float value;
        if (message.ToFloat(out value)) return Mathf.Clamp01(value);
        int intValue;
        if (message.ToInt(out intValue)) return Mathf.Clamp01(intValue);
        return fallback;
    }

    private void FixedUpdate()
    {
        if (IsRespawning) return;

        float x = ApplyDeadZone((inputX - 0.5f) * 2f);
        float z = ApplyDeadZone((inputY - 0.5f) * 2f);
        if (MeadowTrafficLight.Block(this, x != 0f || z != 0f || pressure > pressureThreshold))
        {
            if (!IsRespawning) MeadowTrafficLight.Hold(body);
            return;
        }
        var planarInput = Vector3.ClampMagnitude(new Vector3(x, 0f, z), 1f);
        body.AddForce(planarInput * acceleration, ForceMode.Acceleration);

        var velocity = body.linearVelocity;
        var horizontal = new Vector3(velocity.x, 0f, velocity.z);
        horizontal = Vector3.MoveTowards(horizontal, Vector3.zero, 0.9f * Time.fixedDeltaTime);
        if (horizontal.magnitude > maximumSpeed) horizontal = horizontal.normalized * maximumSpeed;

        float desiredY = spawnPosition.y;
        if (groundCollider != null)
        {
            RaycastHit hit;
            var ray = new Ray(transform.position + Vector3.up * 20f, Vector3.down);
            if (groundCollider.Raycast(ray, out hit, 80f)) desiredY = hit.point.y + hoverHeight;
        }

        float altitudeError = desiredY - transform.position.y;
        float verticalAcceleration = altitudeError * hoverSpring - velocity.y * hoverDamping;
        body.AddForce(Vector3.up * verticalAcceleration, ForceMode.Acceleration);
        body.linearVelocity = new Vector3(horizontal.x, body.linearVelocity.y, horizontal.z);

        if (horizontal.sqrMagnitude > 0.2f)
        {
            var desired = Quaternion.LookRotation(horizontal.normalized, Vector3.up);
            body.MoveRotation(Quaternion.Slerp(body.rotation, desired, Time.fixedDeltaTime * 4f));
        }

        KeepInsideGround();
    }

    private void Update()
    {
        UpdateBeamAndHeat();
        AnimateVisual();
    }

    private void KeepInsideGround()
    {
        if (groundCollider == null) return;
        Bounds bounds = groundCollider.bounds;
        Vector3 position = body.position;
        float minX = bounds.min.x + groundEdgePadding;
        float maxX = bounds.max.x - groundEdgePadding;
        float minZ = bounds.min.z + groundEdgePadding;
        float maxZ = bounds.max.z - groundEdgePadding;
        float clampedX = Mathf.Clamp(position.x, minX, maxX);
        float clampedZ = Mathf.Clamp(position.z, minZ, maxZ);
        if (!Mathf.Approximately(position.x, clampedX) || !Mathf.Approximately(position.z, clampedZ))
        {
            body.position = new Vector3(clampedX, position.y, clampedZ);
            Vector3 velocity = body.linearVelocity;
            if ((position.x < minX && velocity.x < 0f) || (position.x > maxX && velocity.x > 0f)) velocity.x = 0f;
            if ((position.z < minZ && velocity.z < 0f) || (position.z > maxZ && velocity.z > 0f)) velocity.z = 0f;
            body.linearVelocity = velocity;
        }
    }

    private float ApplyDeadZone(float value)
    {
        if (Mathf.Abs(value) <= neutralDeadZone) return 0f;
        return Mathf.Sign(value) * Mathf.InverseLerp(neutralDeadZone, 1f, Mathf.Abs(value));
    }

    private void UpdateBeamAndHeat()
    {
        if (!IsRespawning && MeadowTrafficLight.Block(this, pressure > pressureThreshold))
        {
            beamTarget = null;
            beamWasFiring = false;
            SetBeam(false, transform.position, 0f);
            Heat = Mathf.MoveTowards(Heat, 0f, coolingPerSecond * Time.deltaTime);
            if (IsOverheated && Heat <= restartHeat) IsOverheated = false;
            return;
        }
        if (IsRespawning || IsOverheated)
        {
            beamTarget = null;
            beamWasFiring = false;
            Heat = Mathf.MoveTowards(Heat, 0f, coolingPerSecond * Time.deltaTime);
            if (IsOverheated && Heat <= restartHeat) IsOverheated = false;
            SetBeam(false, transform.position, 0f);
            return;
        }

        float beamStrength = Mathf.InverseLerp(pressureThreshold, 1f, pressure);
        bool pressureHeld = pressure > pressureThreshold;
        if (!pressureHeld)
            beamTarget = null;
        else if (!IsValidBeamTarget(beamTarget))
            beamTarget = OSCCombatTargeting.FindRandomEnemy(
                this, maximumBeamDistance);

        IOSCCombatant target = beamTarget;
        bool wantsBeam = pressureHeld && target != null;
        if (wantsBeam && !beamWasFiring && Random.value < poopDropChance)
            SpawnPoopBomb();
        beamWasFiring = wantsBeam;
        Vector3 origin = transform.position + Vector3.down * 0.75f;
        float distance = target != null
            ? OSCCombatTargeting.HorizontalDistance(
                transform.position, target.CombatTransform.position)
            : float.MaxValue;

        if (wantsBeam && distance <= maximumBeamDistance)
        {
            Collider targetCollider = target.CombatCollider;
            Vector3 targetPoint = targetCollider != null
                ? targetCollider.ClosestPoint(origin)
                : target.CombatTransform.position + Vector3.up * 0.75f;
            Vector3 direction = (targetPoint - origin).normalized;
            Vector3 endPoint = targetPoint;
            RaycastHit hit;
            if (targetCollider != null && targetCollider.Raycast(
                new Ray(origin, direction), out hit, maximumBeamDistance))
                endPoint = hit.point;

            Heat = Mathf.Min(100f, Heat + heatPerSecond * beamStrength * Time.deltaTime);
            SetBeam(true, endPoint, beamStrength);
            target.ReceiveCombatDamage(
                beamDamagePerSecond * beamStrength * Time.deltaTime,
                this);

            if (Heat >= 100f)
            {
                Heat = 100f;
                IsOverheated = true;
                beamTarget = null;
                SetBeam(false, endPoint, 0f);
            }
        }
        else
        {
            Heat = Mathf.MoveTowards(Heat, 0f, coolingPerSecond * Time.deltaTime);
            SetBeam(false, origin, 0f);
        }
    }

    public void SpawnPoopBomb()
    {
        if (poopPrefab == null || IsRespawning) return;

        var bombObject = new GameObject("UFO Poop Bomb");
        bombObject.transform.position =
            transform.position + Vector3.down * poopDropOffset;

        GameObject visualObject = Instantiate(
            poopPrefab,
            bombObject.transform);
        visualObject.name = "Poop Visual";
        visualObject.transform.localPosition = Vector3.zero;
        visualObject.transform.localRotation = Quaternion.Euler(
            Random.Range(-12f, 12f),
            Random.Range(0f, 360f),
            Random.Range(-12f, 12f));
        visualObject.transform.localScale = Vector3.one;
        PopArtSceneStyler.StylePoop(visualObject);

        foreach (Collider childCollider in
                 visualObject.GetComponentsInChildren<Collider>(true))
            childCollider.enabled = false;
        foreach (Rigidbody childBody in
                 visualObject.GetComponentsInChildren<Rigidbody>(true))
        {
            childBody.isKinematic = true;
            childBody.detectCollisions = false;
        }

        var normalizer =
            visualObject.AddComponent<OSCProjectileVisualNormalizer>();
        normalizer.Initialize(bombObject.transform, poopVisualSize);

        var bombCollider = bombObject.AddComponent<SphereCollider>();
        bombCollider.radius = poopVisualSize * 0.42f;
        if (sphereCollider != null)
            Physics.IgnoreCollision(bombCollider, sphereCollider, true);

        var bombBody = bombObject.AddComponent<Rigidbody>();
        bombBody.mass = 0.85f;
        bombBody.useGravity = true;
        bombBody.interpolation = RigidbodyInterpolation.Interpolate;
        bombBody.collisionDetectionMode =
            CollisionDetectionMode.ContinuousDynamic;
        Vector3 inheritedVelocity = body != null
            ? body.linearVelocity * 0.18f
            : Vector3.zero;
        inheritedVelocity.y = Mathf.Min(inheritedVelocity.y, -0.6f);
        bombBody.linearVelocity = inheritedVelocity;
        bombBody.angularVelocity = Random.insideUnitSphere * 2.5f;

        var bomb = bombObject.AddComponent<UFOPoopBomb>();
        bomb.Initialize(
            this,
            groundCollider,
            poopDamage,
            poopBlastRadius);
    }

    private bool IsValidBeamTarget(IOSCCombatant target)
    {
        MonoBehaviour targetBehaviour = target as MonoBehaviour;
        return targetBehaviour != null && targetBehaviour.isActiveAndEnabled &&
               !target.IsRespawning &&
               OSCCombatTargeting.HorizontalDistance(
                   transform.position,
                   target.CombatTransform.position) <= maximumBeamDistance;
    }

    private void SetupBeamVisuals()
    {
        if (beamLine == null) return;
        beamLine.positionCount = 2;
        beamLine.useWorldSpace = true;
        beamLine.numCapVertices = 8;

        var glowObject = new GameObject("Beam Outer Glow");
        glowObject.transform.SetParent(transform, false);
        beamGlowLine = glowObject.AddComponent<LineRenderer>();
        beamGlowLine.positionCount = 2;
        beamGlowLine.useWorldSpace = true;
        beamGlowLine.numCapVertices = 8;
        beamGlowLine.sortingOrder = beamLine.sortingOrder - 1;

        Shader shader = Shader.Find("Hand2/SpectralEnergyLine");
        if (shader == null) shader = Shader.Find("Universal Render Pipeline/Unlit");
        if (shader == null) shader = Shader.Find("Unlit/Color");
        spectralBeamMaterial = new Material(shader) { name = "Runtime UFO Contour Core" };
        spectralBeamMaterial.SetColor("_BaseColor", new Color(0.12f,3.4f,4.2f,1f));
        if (spectralBeamMaterial.HasProperty("_PulseSpeed")) spectralBeamMaterial.SetFloat("_PulseSpeed",9.5f);
        if (spectralBeamMaterial.HasProperty("_BandScale")) spectralBeamMaterial.SetFloat("_BandScale",34f);
        beamLine.material = spectralBeamMaterial;
        var glowMaterial = new Material(shader) { name = "Runtime UFO Beam Glow" };
        spectralBeamGlowMaterial = glowMaterial;
        Color glowColor = new Color(0.08f, 2.25f, 3.4f, 0.3f);
        glowMaterial.color = glowColor;
        if (glowMaterial.HasProperty("_BaseColor"))
            glowMaterial.SetColor("_BaseColor", glowColor);
        if (glowMaterial.HasProperty("_Surface")) glowMaterial.SetFloat("_Surface", 1f);
        if (glowMaterial.HasProperty("_Blend")) glowMaterial.SetFloat("_Blend", 1f);
        if (glowMaterial.HasProperty("_SrcBlend")) glowMaterial.SetFloat("_SrcBlend", 5f);
        if (glowMaterial.HasProperty("_DstBlend")) glowMaterial.SetFloat("_DstBlend", 1f);
        if (glowMaterial.HasProperty("_ZWrite")) glowMaterial.SetFloat("_ZWrite", 0f);
        glowMaterial.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        glowMaterial.renderQueue = 3000;
        beamGlowLine.material = glowMaterial;

        var lightObject = new GameObject("Beam Impact Glow");
        lightObject.transform.SetParent(transform, false);
        beamImpactLight = lightObject.AddComponent<Light>();
        beamImpactLight.type = LightType.Point;
        beamImpactLight.color = new Color(0.12f, 1.7f, 3f);
        beamImpactLight.range = 3.2f;
        beamImpactLight.intensity = 5.2f;
        beamImpactLight.shadows = LightShadows.None;
        beamImpactLight.enabled = false;
    }

    private void SetBeam(bool active, Vector3 endPoint, float strength)
    {
        IsBeamActive = active;
        if (beamLine == null) return;
        beamLine.enabled = active;
        if (beamGlowLine != null) beamGlowLine.enabled = active;
        if (beamImpactLight != null) beamImpactLight.enabled = active;
        if (!active) return;

        Vector3 origin = transform.position + Vector3.down * 0.75f;
        float heat01 = Heat / 100f;
        Color coldColor = new Color(0.06f, 2.4f, 3.4f, 1f);
        Color hotColor = Color.Lerp(
            coldColor,
            new Color(3.4f, 0.06f, 1.75f, 1f),
            heat01);
        beamLine.startWidth = Mathf.Lerp(0.34f, 0.58f, strength);
        beamLine.endWidth = Mathf.Lerp(0.28f, 0.46f, strength);
        beamLine.SetPosition(0, origin);
        beamLine.SetPosition(1, endPoint);
        beamLine.startColor = new Color(0.65f, 2.8f, 3.4f, 1f);
        beamLine.endColor = hotColor;

        if (beamGlowLine != null)
        {
            beamGlowLine.startWidth = Mathf.Lerp(0.82f, 1.28f, strength);
            beamGlowLine.endWidth = Mathf.Lerp(0.68f, 1.05f, strength);
            beamGlowLine.SetPosition(0, origin);
            beamGlowLine.SetPosition(1, endPoint);
            beamGlowLine.startColor = new Color(hotColor.r, hotColor.g, hotColor.b, 0.18f);
            beamGlowLine.endColor = new Color(hotColor.r, hotColor.g, hotColor.b, 0.5f);
        }

        if (beamImpactLight != null)
        {
            beamImpactLight.transform.position = endPoint;
            beamImpactLight.color = hotColor;
            beamImpactLight.intensity = Mathf.Lerp(3.5f, 6.5f, strength);
        }
    }

    private void AnimateVisual()
    {
        if (visual == null) return;
        visual.Rotate(0f, 42f * Time.deltaTime, 0f, Space.Self);
        float pulse = Mathf.Sin(Time.time * 2.8f) * 0.025f;
        hitPulse = Mathf.MoveTowards(hitPulse, 0f, Time.deltaTime * 2.8f);
        visual.localScale = visualBaseScale * (1f + pulse + hitPulse);
    }

    public void ResetControlInput()
    {
        inputX = 0.5f;
        inputY = 0.5f;
        pressure = 0f;
        beamTarget = null;
        beamWasFiring = false;
        if (body != null)
        {
            body.linearVelocity = Vector3.zero;
            body.angularVelocity = Vector3.zero;
        }
        SetBeam(false, transform.position, 0f);
    }

    public void ReceiveMilkHit(float damage)
    {
        ReceiveCombatDamage(damage, null);
    }

    public void ReceiveBeamDamage(float damage)
    {
        ReceiveCombatDamage(damage, null);
    }

    public void ReceiveCombatDamage(float damage, MonoBehaviour attacker)
    {
        if (IsRespawning) return;
        Health = Mathf.Max(0f, Health - damage);
        if (damage > 0f) hitFlash?.Trigger();
        Heat = Mathf.Max(0f, Heat - 8f);
        hitPulse = 0.16f;
        if (Health > 0f && attacker != null &&
            Time.time >= nextHitKnockbackTime)
        {
            OSCCombatFeedback.ApplyHorizontalKnockback(
                body,
                transform,
                attacker,
                hitKnockbackSpeed,
                maximumSpeed + hitKnockbackSpeed);
            nextHitKnockbackTime = Time.time + hitKnockbackCooldown;
        }
        if (Health <= 0f) StartCoroutine(DeathAndRespawn(attacker));
    }

    public void ResetForNewMatch()
    {
        StopAllCoroutines();
        ResetControlInput();
        IsRespawning = false;
        Health = maximumHealth; Heat = 0f; IsOverheated = false;
        body.linearVelocity = Vector3.zero;
        body.angularVelocity = Vector3.zero;
        body.position = spawnPosition;
        transform.position = spawnPosition;
        Quaternion upright = Quaternion.Euler(0f, spawnRotation.eulerAngles.y, 0f);
        body.rotation = upright;
        transform.rotation = upright;
        body.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;
        body.useGravity = false;
        sphereCollider.enabled = true;
        beamTarget = null; beamWasFiring = false; SetBeam(false, transform.position, 0f);
    }

    private IEnumerator DeathAndRespawn(MonoBehaviour attacker)
    {
        IsRespawning = true;
        PopArtDeathTrail.Begin(transform);
        beamTarget = null;
        beamWasFiring = false;
        SetBeam(false, transform.position, 0f);
        if (gameManager != null) gameManager.AwardPoint(attacker);

        sphereCollider.enabled = false;
        body.useGravity = true;
        body.constraints = RigidbodyConstraints.None;
        Vector3 launch = new Vector3(Random.Range(-1f, 1f), Random.Range(0.65f, 1f), Random.Range(-0.35f, 0.8f)).normalized;
        body.linearVelocity = launch * Random.Range(20f, 27f) + Vector3.up * 10f;
        body.angularVelocity = Random.insideUnitSphere * 8f;

        yield return new WaitForSeconds(1.15f);

        Health = maximumHealth;
        Heat = 0f;
        IsOverheated = false;
        transform.position = spawnPosition + Vector3.up * 12f;
        transform.rotation = Quaternion.Euler(0f, spawnRotation.eulerAngles.y, 0f);
        body.rotation = transform.rotation;
        body.linearVelocity = Vector3.down * 1.5f;
        body.angularVelocity = Vector3.zero;
        body.constraints = RigidbodyConstraints.FreezePositionX |
                           RigidbodyConstraints.FreezePositionZ |
                           RigidbodyConstraints.FreezeRotation;
        sphereCollider.enabled = true;

        while (transform.position.y > spawnPosition.y + 0.12f)
            yield return null;

        transform.position = spawnPosition;
        body.linearVelocity = Vector3.zero;
        body.angularVelocity = Vector3.zero;
        body.useGravity = false;
        body.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;
        IsRespawning = false;
    }
}
