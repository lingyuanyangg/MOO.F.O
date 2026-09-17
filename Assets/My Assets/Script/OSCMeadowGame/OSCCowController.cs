using System.Collections;
using UnityEngine;
using extOSC;

[DisallowMultipleComponent]
[RequireComponent(typeof(Rigidbody), typeof(CapsuleCollider))]
public class OSCCowController : MonoBehaviour, IOSCCombatant
{
    [Header("OSC")]
    public OSCReceiver receiver;
    public string moveXAddress = "/GameControlLX";
    public string moveYAddress = "/GameControlLY";
    public string jumpAddress = "/Jump";
    public string eatAddress = "/Eat";
    public string shootAddress = "/Shoot";

    [Header("Scene References")]
    public Transform visual;
    public Transform muzzle;
    public Transform ufoTarget;
    public Collider groundCollider;
    public OSCMeadowGameManager gameManager;

    [Header("Movement")]
    public float acceleration = 18f;
    public float braking = 26f;
    public float forwardAccelerationMultiplier = 1.18f;
    public float maximumSpeed = 7f;
    public float jumpVelocity = 6.2f;
    public float steeringSharpness = 8f;
    public float joystickSmoothing = 14f;
    public float neutralDeadZone = 0.08f;
    public bool invertY;
    public float groundEdgePadding = 1.25f;
    public float fenceBodySafetyRadius = 2.15f;

    [Header("Cow Resources")]
    public float maximumHealth = 100f;
    public float maximumMilk = 100f;
    public float milk = 62f;
    public float eatingMilkPerSecond = 19f;
    public float milkPerShot = 12f;
    public float shotCooldown = 0.48f;

    [Header("Hit Feedback")]
    public float hitKnockbackSpeed = 1.7f;
    public float hitKnockbackCooldown = 0.16f;

    public float Health { get; private set; } = 100f;
    public float Milk => milk;
    public bool IsEating { get; private set; }
    public bool IsRespawning { get; private set; }
    public Vector2 MoveInput => smoothedMoveInput;
    public Transform CombatTransform => transform;
    public Collider CombatCollider => capsule;

    private Rigidbody body;
    private CapsuleCollider capsule;
    private float inputX = 0.5f;
    private float inputY = 0.5f;
    private Vector2 smoothedMoveInput;
    private bool eatHeld;
    private bool jumpRequested;
    private bool shootRequested;
    private float lastJumpSignal = -10f;
    private float lastShootSignal = -10f;
    private float lastShotTime = -10f;
    private float nextHitKnockbackTime;
    private Vector3 spawnPosition;
    private Quaternion spawnRotation;
    private Quaternion visualBaseRotation;
    private Vector3 visualBasePosition;
    private Vector3 visualBaseScale;
    private PopArtHitFlash hitFlash;
    private Bounds arenaFenceBounds;
    private bool hasArenaFenceBounds;

    private OSCBind moveXBind;
    private OSCBind moveYBind;
    private OSCBind jumpBind;
    private OSCBind eatBind;
    private OSCBind shootBind;

    private void Awake()
    {
        body = GetComponent<Rigidbody>();
        capsule = GetComponent<CapsuleCollider>();
        hitFlash = GetComponent<PopArtHitFlash>();
        if (hitFlash == null) hitFlash = gameObject.AddComponent<PopArtHitFlash>();

        body.mass = 1.5f;
        body.useGravity = true;
        body.interpolation = RigidbodyInterpolation.Interpolate;
        body.collisionDetectionMode = CollisionDetectionMode.Continuous;
        body.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;

        capsule.radius = 0.48f;
        capsule.height = 1.55f;
        capsule.center = new Vector3(0f, 0.78f, 0f);

        Health = maximumHealth;
        spawnPosition = transform.position;
        spawnRotation = transform.rotation;
    }

    private void Start()
    {
        if (visual != null)
        {
            visualBaseRotation = visual.localRotation;
            visualBasePosition = visual.localPosition;
            visualBaseScale = visual.localScale;
        }

        if (gameManager == null) gameManager = FindFirstObjectByType<OSCMeadowGameManager>();
        if (receiver == null) receiver = FindFirstObjectByType<OSCReceiver>();
        ResolveArenaFenceBounds();

        if (receiver == null)
        {
            Debug.LogError("OSCCowController: OSCReceiver not found.");
            enabled = false;
            return;
        }

        moveXBind = receiver.Bind(moveXAddress, message => inputX = Read01(message, inputX));
        moveYBind = receiver.Bind(moveYAddress, message => inputY = Read01(message, inputY));
        jumpBind = receiver.Bind(jumpAddress, ReceiveJump);
        eatBind = receiver.Bind(eatAddress, message => eatHeld = Read01(message, 0f) > 0.5f);
        shootBind = receiver.Bind(shootAddress, ReceiveShoot);
    }

    private void OnDestroy()
    {
        if (receiver == null) return;
        receiver.Unbind(moveXBind);
        receiver.Unbind(moveYBind);
        receiver.Unbind(jumpBind);
        receiver.Unbind(eatBind);
        receiver.Unbind(shootBind);
    }

    private static float Read01(OSCMessage message, float fallback)
    {
        if (message.ToFloat(out float value)) return Mathf.Clamp01(value);
        if (message.ToInt(out int intValue)) return Mathf.Clamp01(intValue);
        return fallback;
    }

    private void ReceiveJump(OSCMessage message)
    {
        if (MeadowTrafficLight.Block(this, Read01(message, 0f) > 0.5f)) return;
        if (Read01(message, 0f) > 0.5f && Time.unscaledTime - lastJumpSignal > 0.16f)
        {
            jumpRequested = true;
            lastJumpSignal = Time.unscaledTime;
        }
    }

    private void ReceiveShoot(OSCMessage message)
    {
        if (MeadowTrafficLight.Block(this, Read01(message, 0f) > 0.5f)) return;
        if (Read01(message, 0f) > 0.5f && Time.unscaledTime - lastShootSignal > 0.12f)
        {
            shootRequested = true;
            lastShootSignal = Time.unscaledTime;
        }
    }

    private void FixedUpdate()
    {
        if (IsRespawning)
        {
            IsEating = false;
            smoothedMoveInput = Vector2.zero;
            return;
        }

        float x = ApplyDeadZone((inputX - 0.5f) * 2f);
        float z = ApplyDeadZone((inputY - 0.5f) * 2f);
        if (MeadowTrafficLight.Block(this, x != 0f || z != 0f || eatHeld || jumpRequested))
        {
            IsEating = false;
            jumpRequested = false;
            smoothedMoveInput = Vector2.zero;
            if (!IsRespawning) MeadowTrafficLight.Hold(body);
            return;
        }
        if (invertY) z = -z;

        Vector2 targetInput = Vector2.ClampMagnitude(
            new Vector2(x, z * forwardAccelerationMultiplier),
            1f);
        smoothedMoveInput = Vector2.MoveTowards(
            smoothedMoveInput,
            targetInput,
            joystickSmoothing * Time.fixedDeltaTime);

        IsEating = eatHeld && IsGrounded();
        Vector3 velocity = body.linearVelocity;
        Vector3 horizontal = new Vector3(velocity.x, 0f, velocity.z);

        if (IsEating)
        {
            smoothedMoveInput = Vector2.zero;
            horizontal = Vector3.zero;
        }
        else
        {
            Vector3 desiredVelocity = new Vector3(
                smoothedMoveInput.x,
                0f,
                smoothedMoveInput.y) * maximumSpeed;
            float rate = desiredVelocity.sqrMagnitude > horizontal.sqrMagnitude
                ? acceleration
                : braking;
            horizontal = Vector3.MoveTowards(
                horizontal,
                desiredVelocity,
                rate * Time.fixedDeltaTime);
        }

        body.linearVelocity = new Vector3(horizontal.x, velocity.y, horizontal.z);
        body.angularVelocity = Vector3.zero;

        if (!IsEating && targetInput.sqrMagnitude > 0.015f)
        {
            Vector3 facingDirection = new Vector3(targetInput.x, 0f, targetInput.y).normalized;
            Quaternion desiredRotation =
                Quaternion.LookRotation(facingDirection, Vector3.up) *
                Quaternion.Euler(0f, -90f, 0f);
            body.MoveRotation(Quaternion.RotateTowards(
                body.rotation,
                desiredRotation,
                steeringSharpness * 45f * Time.fixedDeltaTime));
        }

        if (jumpRequested)
        {
            if (IsGrounded() && !IsEating)
            {
                Vector3 jumpVector = body.linearVelocity;
                jumpVector.y = 0f;
                body.linearVelocity = jumpVector;
                body.AddForce(Vector3.up * jumpVelocity, ForceMode.VelocityChange);
            }
            jumpRequested = false;
        }

        KeepInsideGround();
    }

    private void Update()
    {
        if (IsRespawning) return;
        if (MeadowTrafficLight.Block(this, shootRequested || eatHeld))
        {
            shootRequested = false;
            IsEating = false;
            return;
        }

        if (IsEating)
            milk = Mathf.MoveTowards(milk, maximumMilk, eatingMilkPerSecond * Time.deltaTime);

        if (shootRequested)
        {
            TryShoot();
            shootRequested = false;
        }

        AnimateVisual();
    }

    private void TryShoot()
    {
        IOSCCombatant target = OSCCombatTargeting.FindRandomEnemy(this);
        if (target == null || milk < milkPerShot ||
            Time.time - lastShotTime < shotCooldown)
            return;

        milk -= milkPerShot;
        lastShotTime = Time.time;

        Vector3 origin = muzzle != null
            ? muzzle.position
            : transform.position + Vector3.up * 0.9f + transform.forward * 1.2f;

        var projectileObject = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        projectileObject.name = "Homing Milk Water Column";
        projectileObject.transform.position = origin;
        projectileObject.transform.localScale = Vector3.one * 0.42f;

        var renderer = projectileObject.GetComponent<Renderer>();
        if (renderer != null)
        {
            var material = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
            material.SetColor("_BaseColor", new Color(2.5f, 2.7f, 3f, 1f));
            renderer.material = material;
        }

        var projectileBody = projectileObject.AddComponent<Rigidbody>();
        projectileBody.useGravity = false;
        projectileBody.mass = 0.18f;

        var projectile = projectileObject.AddComponent<MilkProjectile>();
        projectile.Initialize(target, this, 23f, 14f, origin);
    }

    public void ResetControlInput()
    {
        inputX = 0.5f;
        inputY = 0.5f;
        smoothedMoveInput = Vector2.zero;
        eatHeld = false;
        jumpRequested = false;
        shootRequested = false;
        IsEating = false;
        if (body != null)
        {
            body.linearVelocity = Vector3.zero;
            body.angularVelocity = Vector3.zero;
        }
    }

    public void ReceiveBeamDamage(float damage)
    {
        ReceiveCombatDamage(damage, null);
    }

    public void ReceiveMilkHit(float damage)
    {
        ReceiveCombatDamage(damage, null);
    }

    public void ReceiveCombatDamage(float damage, MonoBehaviour attacker)
    {
        if (IsRespawning) return;

        Health = Mathf.Max(0f, Health - damage);
        if (damage > 0f) hitFlash?.Trigger();
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
        if (Health <= 0f)
        {
            if (gameManager != null) gameManager.AwardPoint(attacker);
            StartCoroutine(DeathAndRespawn());
        }
    }

    public void ResetForNewMatch()
    {
        StopAllCoroutines();
        ResetControlInput();
        IsRespawning = false;
        Health = maximumHealth; milk = maximumMilk; IsEating = false;
        body.linearVelocity = Vector3.zero;
        body.angularVelocity = Vector3.zero;
        body.position = spawnPosition;
        transform.position = spawnPosition;
        Quaternion upright = Quaternion.Euler(0f, spawnRotation.eulerAngles.y, 0f);
        body.rotation = upright;
        transform.rotation = upright;
        body.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;
        body.useGravity = true;
        capsule.enabled = true;
        
    }

    private IEnumerator DeathAndRespawn()
    {
        IsRespawning = true;
        PopArtDeathTrail.Begin(transform);
        IsEating = false;
        eatHeld = false;
        jumpRequested = false;
        shootRequested = false;

        capsule.enabled = false;
        body.constraints = RigidbodyConstraints.None;
        body.useGravity = true;

        float side = Random.value < 0.5f ? -1f : 1f;
        Vector3 launch = new Vector3(
            side * Random.Range(12f, 17f),
            Random.Range(8f, 12f),
            Random.Range(-5f, 7f));
        body.linearVelocity = launch;
        body.angularVelocity = Random.onUnitSphere * 9f;

        yield return new WaitForSeconds(1.15f);

        Health = maximumHealth;
        milk = Mathf.Max(35f, milk);
        transform.position = spawnPosition + new Vector3(
            Random.Range(-1.2f, 1.2f),
            11f,
            Random.Range(-0.8f, 0.8f));
        transform.position = ClampToArena(transform.position);
        transform.rotation = Quaternion.Euler(0f, spawnRotation.eulerAngles.y, 0f);
        body.rotation = transform.rotation;
        body.linearVelocity = Vector3.down * 2f;
        body.angularVelocity = Vector3.zero;
        body.constraints = RigidbodyConstraints.FreezePositionX |
                           RigidbodyConstraints.FreezePositionZ |
                           RigidbodyConstraints.FreezeRotation;
        capsule.enabled = true;

        float timeout = Time.time + 4.5f;
        while (Time.time < timeout && !IsGrounded())
            yield return null;

        body.linearVelocity = Vector3.zero;
        body.angularVelocity = Vector3.zero;
        body.constraints = RigidbodyConstraints.FreezeRotationX |
                           RigidbodyConstraints.FreezeRotationZ;
        KeepInsideGround();
        IsRespawning = false;
    }

    private void KeepInsideGround()
    {
        if (groundCollider == null) return;

        Bounds bounds = hasArenaFenceBounds ? arenaFenceBounds : groundCollider.bounds;
        Vector3 position = body.position;
        float safeInset = hasArenaFenceBounds
            ? Mathf.Max(0.5f, fenceBodySafetyRadius)
            : groundEdgePadding;
        float minX = bounds.min.x + safeInset;
        float maxX = bounds.max.x - safeInset;
        float minZ = bounds.min.z + safeInset;
        float maxZ = bounds.max.z - safeInset;
        float clampedX = Mathf.Clamp(position.x, minX, maxX);
        float clampedZ = Mathf.Clamp(position.z, minZ, maxZ);

        bool crossedEdge = !Mathf.Approximately(position.x, clampedX) ||
                           !Mathf.Approximately(position.z, clampedZ);
        Vector3 correctedPosition = new Vector3(clampedX, position.y, clampedZ);
        Vector3 velocity = body.linearVelocity;

        if (crossedEdge)
        {
            if ((position.x < minX && velocity.x < 0f) ||
                (position.x > maxX && velocity.x > 0f)) velocity.x = 0f;
            if ((position.z < minZ && velocity.z < 0f) ||
                (position.z > maxZ && velocity.z > 0f)) velocity.z = 0f;
        }

        Ray ray = new Ray(
            new Vector3(clampedX, groundCollider.bounds.max.y + 12f, clampedZ),
            Vector3.down);
        if (groundCollider.Raycast(ray, out RaycastHit hit, groundCollider.bounds.size.y + 30f) &&
            correctedPosition.y < hit.point.y - 1.5f)
        {
            correctedPosition.y = hit.point.y + 0.12f;
            velocity.y = 0f;
            crossedEdge = true;
        }

        if (crossedEdge)
        {
            body.position = correctedPosition;
            body.linearVelocity = velocity;
        }
    }

    private Vector3 ClampToArena(Vector3 position)
    {
        if (!hasArenaFenceBounds) return position;
        float inset = Mathf.Max(0.5f, fenceBodySafetyRadius);
        position.x = Mathf.Clamp(position.x, arenaFenceBounds.min.x + inset, arenaFenceBounds.max.x - inset);
        position.z = Mathf.Clamp(position.z, arenaFenceBounds.min.z + inset, arenaFenceBounds.max.z - inset);
        return position;
    }

    private void ResolveArenaFenceBounds()
    {
        bool initialized = false;
        foreach (Renderer renderer in FindObjectsByType<Renderer>(FindObjectsInactive.Include))
        {
            Transform current = renderer.transform;
            bool belongsToFence = false;
            while (current != null)
            {
                if (current.name == "Wooden Arena Fence") { belongsToFence = true; break; }
                current = current.parent;
            }
            if (!belongsToFence) continue;
            if (!initialized) { arenaFenceBounds = renderer.bounds; initialized = true; }
            else arenaFenceBounds.Encapsulate(renderer.bounds);
        }
        hasArenaFenceBounds = initialized;
    }

    private float ApplyDeadZone(float value)
    {
        if (Mathf.Abs(value) <= neutralDeadZone) return 0f;
        return Mathf.Sign(value) *
               Mathf.InverseLerp(neutralDeadZone, 1f, Mathf.Abs(value));
    }

    private bool IsGrounded()
    {
        if (groundCollider != null)
        {
            Vector3 probe = transform.position + Vector3.up * 0.12f;
            Vector3 closest = groundCollider.ClosestPoint(probe);
            return Vector3.Distance(probe, closest) < 0.42f;
        }

        return Physics.Raycast(
            transform.position + Vector3.up * 0.2f,
            Vector3.down,
            0.55f,
            Physics.DefaultRaycastLayers,
            QueryTriggerInteraction.Ignore);
    }

    private void AnimateVisual()
    {
        if (visual == null) return;

        float horizontalSpeed = new Vector2(
            body.linearVelocity.x,
            body.linearVelocity.z).magnitude;
        float walk = Mathf.Clamp01(horizontalSpeed / maximumSpeed);
        float step = Mathf.Sin(Time.time * Mathf.Lerp(3f, 10f, walk));
        Vector3 targetPosition =
            visualBasePosition + Vector3.up * (step * 0.045f * walk);
        Quaternion targetRotation = visualBaseRotation;

        if (IsEating)
        {
            float chew = Mathf.Sin(Time.time * 5.5f);
            targetRotation =
                visualBaseRotation * Quaternion.Euler(24f + chew * 4f, 0f, 0f);
            targetPosition += Vector3.down * 0.16f + Vector3.forward * 0.08f;
        }
        else if (walk > 0.05f)
        {
            targetRotation =
                visualBaseRotation * Quaternion.Euler(step * 2.5f, 0f, -step * 1.5f);
        }

        visual.localPosition = Vector3.Lerp(
            visual.localPosition,
            targetPosition,
            Time.deltaTime * 8f);
        visual.localRotation = Quaternion.Slerp(
            visual.localRotation,
            targetRotation,
            Time.deltaTime * 7f);
        visual.localScale = Vector3.Lerp(
            visual.localScale,
            visualBaseScale,
            Time.deltaTime * 8f);
    }
}
