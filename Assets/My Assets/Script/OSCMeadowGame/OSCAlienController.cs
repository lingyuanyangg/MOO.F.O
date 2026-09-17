using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using extOSC;

[DisallowMultipleComponent]
[RequireComponent(typeof(Rigidbody), typeof(CapsuleCollider))]
public class OSCAlienController : MonoBehaviour, IOSCCombatant
{
    [Header("OSC rubber attacks")]
    public OSCReceiver receiver;
    public string leftHandAddress = "/R1";
    public string rightHandAddress = "/R2";
    public string leftFootAddress = "/R3";
    public string rightFootAddress = "/R4";
    public string headAddress = "/R5";

    [Header("Scene references")]
    public Transform visual;
    public Collider groundCollider;
    public OSCMeadowGameManager gameManager;

    [Header("Keyboard movement")]
    public float walkSpeed = 2.65f;
    public float acceleration = 8f;
    public float turnSpeed = 420f;
    public float groundEdgePadding = 2.2f;

    [Header("Walk animation")]
    public float walkCycleSpeed = 7.5f;
    public float legSwingAngle = 28f;
    public float armSwingAngle = 22f;
    public float walkBobHeight = 0.045f;
    public float walkAnimationSmoothing = 10f;

    [Header("Health / stamina")]
    public float maximumHealth = 100f;
    public float maximumStamina = 100f;
    public float stamina = 100f;
    public float staminaPerAttack = 22f;
    public float staminaRecoveryPerSecond = 18f;

    [Header("Hit Feedback")]
    public float hitKnockbackSpeed = 1.55f;
    public float hitKnockbackCooldown = 0.16f;

    [Header("Rubber attack")]
    public float attackDamage = 18f;
    public float maximumAttackDistance = 18f;
    public float extendDuration = 0.16f;
    public float holdDuration = 0.07f;
    public float retractDuration = 0.2f;
    public float limbWidth = 0.18f;

    public float Health { get; private set; } = 100f;
    public float Stamina => stamina;
    public bool IsRespawning { get; private set; }
    public bool IsAttacking => activeAttackCount > 0;
    public Vector2 MoveInput { get; private set; }
    public Transform CombatTransform => transform;
    public Collider CombatCollider => capsule;

    private static readonly string[] DefaultBoneNames =
    {
        "mixamorig:LeftArm",
        "mixamorig:RightArm",
        "mixamorig:LeftUpLeg",
        "mixamorig:RightUpLeg",
        "mixamorig:Neck"
    };

    private readonly string[] addresses = new string[5];
    private readonly bool[] signalHigh = new bool[5];
    private readonly OSCBind[] binds = new OSCBind[5];
    private readonly Transform[] attackBones = new Transform[5];
    private readonly Vector3[] restingLocalPositions = new Vector3[5];
    private readonly Quaternion[] restingLocalRotations = new Quaternion[5];
    private readonly Vector3[] restingLocalScales = new Vector3[5];
    private readonly bool[] boneOverrides = new bool[5];
    private readonly Vector3[] attackOrigins = new Vector3[5];
    private readonly Vector3[] attackTips = new Vector3[5];
    private readonly LineRenderer[] rubberLines = new LineRenderer[5];

    private Rigidbody body;
    private CapsuleCollider capsule;
    private int activeAttackCount;
    private Vector3 spawnPosition;
    private Quaternion spawnRotation;
    private readonly Material[] rubberMaterials = new Material[5];
    private Vector3 visualRestLocalPosition;
    private Quaternion visualRestLocalRotation;
    private float walkBlend;
    private float nextHitKnockbackTime;
    private PopArtHitFlash hitFlash;

    private void Awake()
    {
        body = GetComponent<Rigidbody>();
        capsule = GetComponent<CapsuleCollider>();
        hitFlash = GetComponent<PopArtHitFlash>();
        if (hitFlash == null) hitFlash = gameObject.AddComponent<PopArtHitFlash>();
        body.mass = 1.35f;
        body.useGravity = true;
        body.interpolation = RigidbodyInterpolation.Interpolate;
        body.collisionDetectionMode = CollisionDetectionMode.Continuous;
        body.constraints = RigidbodyConstraints.FreezeRotationX |
                           RigidbodyConstraints.FreezeRotationZ;
        capsule.radius = 0.42f;
        capsule.height = 1.8f;
        capsule.center = new Vector3(0f, 0.9f, 0f);
        Health = maximumHealth;
        stamina = maximumStamina;
        spawnPosition = transform.position;
        spawnRotation = transform.rotation;
    }

    private void Start()
    {
        if (visual == null) visual = transform;
        if (receiver == null) receiver = FindAnyObjectByType<OSCReceiver>();
        if (groundCollider == null)
        {
            OSCCowController cow = FindAnyObjectByType<OSCCowController>();
            if (cow != null) groundCollider = cow.groundCollider;
        }
        if (gameManager == null)
            gameManager = FindAnyObjectByType<OSCMeadowGameManager>();

        addresses[0] = leftHandAddress;
        addresses[1] = rightHandAddress;
        addresses[2] = leftFootAddress;
        addresses[3] = rightFootAddress;
        addresses[4] = headAddress;
        ResolveBones();
        visualRestLocalPosition = visual.localPosition;
        visualRestLocalRotation = visual.localRotation;
        SetupRubberMaterial();

        if (receiver == null)
        {
            Debug.LogError("OSCAlienController: OSCReceiver not found.");
            enabled = false;
            return;
        }

        for (int i = 0; i < addresses.Length; i++)
        {
            int attackIndex = i;
            binds[i] = receiver.Bind(
                addresses[i],
                message => ReceiveAttackSignal(attackIndex, message));
        }
    }

    private void OnDestroy()
    {
        if (receiver != null)
        {
            foreach (OSCBind bind in binds)
                if (bind != null) receiver.Unbind(bind);
        }
        foreach (Material material in rubberMaterials)
            if (material != null) Destroy(material);
    }

    private static float Read01(OSCMessage message)
    {
        if (message.ToFloat(out float value)) return Mathf.Clamp01(value);
        if (message.ToInt(out int intValue)) return Mathf.Clamp01(intValue);
        return 0f;
    }

    private void ReceiveAttackSignal(int index, OSCMessage message)
    {
        bool high = Read01(message) > 0.5f;
        if (MeadowTrafficLight.Block(this, high))
        {
            signalHigh[index] = high;
            return;
        }
        if (high && !signalHigh[index]) TryAttack(index);
        signalHigh[index] = high;
    }

    private void FixedUpdate()
    {
        if (IsRespawning) return;

        Vector3 velocity = body.linearVelocity;
        Vector3 horizontal = new Vector3(velocity.x, 0f, velocity.z);

        Keyboard keyboard = Keyboard.current;
        float inputX = keyboard == null ? 0f :
            (keyboard.dKey.isPressed ? 1f : 0f) -
            (keyboard.aKey.isPressed ? 1f : 0f);
        float inputY = keyboard == null ? 0f :
            (keyboard.wKey.isPressed ? 1f : 0f) -
            (keyboard.sKey.isPressed ? 1f : 0f);
        MoveInput = Vector2.ClampMagnitude(new Vector2(inputX, inputY), 1f);
        bool attackHeld = false;
        for (int i = 0; i < signalHigh.Length; i++) attackHeld |= signalHigh[i];
        if (MeadowTrafficLight.Block(this, attackHeld || (keyboard != null &&
            (keyboard.wKey.isPressed || keyboard.aKey.isPressed || keyboard.sKey.isPressed || keyboard.dKey.isPressed))))
        {
            MoveInput = Vector2.zero;
            walkBlend = 0f;
            if (!IsRespawning) MeadowTrafficLight.Hold(body);
            return;
        }

        if (IsAttacking)
        {
            horizontal = Vector3.MoveTowards(
                horizontal, Vector3.zero, acceleration * 2f * Time.fixedDeltaTime);
        }
        else
        {
            Vector3 direction = GetCameraRelativeDirection(MoveInput);
            Vector3 desired = direction * walkSpeed;
            horizontal = Vector3.MoveTowards(
                horizontal, desired, acceleration * Time.fixedDeltaTime);

            if (direction.sqrMagnitude > 0.01f)
            {
                Quaternion desiredRotation = Quaternion.LookRotation(
                    direction.normalized, Vector3.up);
                body.MoveRotation(Quaternion.RotateTowards(
                    body.rotation,
                    desiredRotation,
                    turnSpeed * Time.fixedDeltaTime));
            }
        }

        body.linearVelocity = new Vector3(horizontal.x, velocity.y, horizontal.z);
        float speed01 = Mathf.Clamp01(horizontal.magnitude / Mathf.Max(0.01f, walkSpeed));
        walkBlend = Mathf.MoveTowards(
            walkBlend,
            IsAttacking ? 0f : speed01,
            walkAnimationSmoothing * Time.fixedDeltaTime);
        KeepInsideGround();
    }

    private void Update()
    {
        if (!IsRespawning && !IsAttacking)
        {
            stamina = Mathf.MoveTowards(
                stamina,
                maximumStamina,
                staminaRecoveryPerSecond * Time.deltaTime);
        }
    }

    private void LateUpdate()
    {
        ApplyWalkAnimation();

        for (int i = 0; i < boneOverrides.Length; i++)
        {
            if (!boneOverrides[i] || attackBones[i] == null) continue;
            attackBones[i].position = attackTips[i];
            UpdateRubberLine(i);
        }
    }

    private Vector3 GetCameraRelativeDirection(Vector2 input)
    {
        if (input.sqrMagnitude < 0.001f) return Vector3.zero;

        Camera camera = Camera.main;
        Vector3 forward = camera != null ? camera.transform.forward : Vector3.forward;
        Vector3 right = camera != null ? camera.transform.right : Vector3.right;
        forward.y = 0f;
        right.y = 0f;
        forward = forward.sqrMagnitude > 0.001f ? forward.normalized : Vector3.forward;
        right = right.sqrMagnitude > 0.001f ? right.normalized : Vector3.right;
        return Vector3.ClampMagnitude(
            right * input.x + forward * input.y,
            1f);
    }

    private void ApplyWalkAnimation()
    {
        if (visual == null) return;

        float cycle = Mathf.Sin(Time.time * walkCycleSpeed);
        float bob = Mathf.Abs(Mathf.Sin(Time.time * walkCycleSpeed)) *
                    walkBobHeight * walkBlend;
        visual.localPosition = Vector3.Lerp(
            visual.localPosition,
            visualRestLocalPosition + Vector3.up * bob,
            1f - Mathf.Exp(-walkAnimationSmoothing * Time.deltaTime));
        visual.localRotation = Quaternion.Slerp(
            visual.localRotation,
            visualRestLocalRotation,
            1f - Mathf.Exp(-walkAnimationSmoothing * Time.deltaTime));

        ApplyWalkBoneRotation(0, -cycle * armSwingAngle * walkBlend);
        ApplyWalkBoneRotation(1, cycle * armSwingAngle * walkBlend);
        ApplyWalkBoneRotation(2, cycle * legSwingAngle * walkBlend);
        ApplyWalkBoneRotation(3, -cycle * legSwingAngle * walkBlend);
    }

    private void ApplyWalkBoneRotation(int index, float angle)
    {
        if (index < 0 || index >= attackBones.Length ||
            attackBones[index] == null || boneOverrides[index])
            return;

        attackBones[index].localRotation =
            restingLocalRotations[index] * Quaternion.AngleAxis(angle, Vector3.right);
    }

    private void TryAttack(int index)
    {
        if (!isActiveAndEnabled || MeadowTrafficLight.Block(this, true)) return;
        if (IsRespawning || index < 0 || index >= attackBones.Length ||
            boneOverrides[index] || stamina < staminaPerAttack ||
            attackBones[index] == null)
            return;

        IOSCCombatant target = OSCCombatTargeting.FindRandomEnemy(
            this, maximumAttackDistance);
        if (target == null) return;

        stamina -= staminaPerAttack;
        StartCoroutine(RubberAttack(index, target));
    }

    private IEnumerator RubberAttack(int index, IOSCCombatant target)
    {
        Transform bone = attackBones[index];
        Vector3 origin = bone.position;
        Collider targetCollider = target.CombatCollider;
        Vector3 targetPoint = targetCollider != null
            ? targetCollider.bounds.center
            : target.CombatTransform.position + Vector3.up * 0.9f;

        attackOrigins[index] = origin;
        attackTips[index] = origin;
        boneOverrides[index] = true;
        activeAttackCount++;
        LineRenderer line = CreateRubberLine(index);
        line.enabled = true;

        yield return AnimateAttack(index, origin, targetPoint, extendDuration, true);
        if (target != null && !target.IsRespawning && !MeadowTrafficLight.IsRed)
            target.ReceiveCombatDamage(attackDamage, this);

        if (holdDuration > 0f)
            yield return new WaitForSeconds(holdDuration);

        yield return AnimateAttack(index, targetPoint, origin, retractDuration, false);

        boneOverrides[index] = false;
        bone.localPosition = restingLocalPositions[index];
        bone.localRotation = restingLocalRotations[index];
        bone.localScale = restingLocalScales[index];
        line.enabled = false;
        activeAttackCount = Mathf.Max(0, activeAttackCount - 1);
    }

    private IEnumerator AnimateAttack(
        int index,
        Vector3 from,
        Vector3 to,
        float duration,
        bool elasticOut)
    {
        float elapsed = 0f;
        duration = Mathf.Max(0.02f, duration);
        while (elapsed < duration && !IsRespawning)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            float shaped = elasticOut
                ? 1f - Mathf.Pow(1f - t, 3f)
                : t * t * (3f - 2f * t);
            attackTips[index] = Vector3.LerpUnclamped(from, to, shaped);
            yield return null;
        }
        attackTips[index] = to;
    }

    private void ResolveBones()
    {
        Transform[] children = visual.GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < DefaultBoneNames.Length; i++)
        {
            foreach (Transform child in children)
            {
                if (child.name != DefaultBoneNames[i] &&
                    !child.name.EndsWith(DefaultBoneNames[i]))
                    continue;
                attackBones[i] = child;
                restingLocalPositions[i] = child.localPosition;
                restingLocalRotations[i] = child.localRotation;
                restingLocalScales[i] = child.localScale;
                break;
            }

            if (attackBones[i] == null)
                Debug.LogWarning("OSCAlienController: bone not found: " + DefaultBoneNames[i]);
        }
    }

    private void SetupRubberMaterial()
    {
        Shader shader = Shader.Find("Hand2/SpectralEnergyLine");
        if (shader == null) shader = Shader.Find("Universal Render Pipeline/Unlit");
        if (shader == null) shader = Shader.Find("Unlit/Color");
        Color[] limbColors =
        {
            new Color(3.2f, 0.04f, 1.55f, 1f),
            new Color(0.02f, 2.8f, 3.4f, 1f),
            new Color(2.0f, 3.1f, 0.02f, 1f),
            new Color(3.2f, 1.7f, 0.02f, 1f),
            new Color(1.25f, 0.08f, 3.1f, 1f)
        };
        for (int i = 0; i < rubberMaterials.Length; i++)
        {
            Material material = new Material(shader)
            {
                name = "Runtime Alien Rubber Limb " + (i + 1)
            };
            material.color = limbColors[i];
            if (material.HasProperty("_BaseColor"))
                material.SetColor("_BaseColor", limbColors[i]);
            if (material.HasProperty("_PulseSpeed")) material.SetFloat("_PulseSpeed", 7f + i);
            if (material.HasProperty("_BandScale")) material.SetFloat("_BandScale", 20f + i * 3f);
            rubberMaterials[i] = material;
        }
    }

    private LineRenderer CreateRubberLine(int index)
    {
        if (rubberLines[index] != null) return rubberLines[index];

        var lineObject = new GameObject("Rubber Limb " + (index + 1));
        lineObject.transform.SetParent(transform, false);
        LineRenderer line = lineObject.AddComponent<LineRenderer>();
        line.positionCount = 2;
        line.useWorldSpace = true;
        line.alignment = LineAlignment.View;
        line.numCapVertices = 8;
        line.numCornerVertices = 4;
        line.startWidth = limbWidth;
        line.endWidth = limbWidth * 0.72f;
        line.material = rubberMaterials[index];
        line.shadowCastingMode = ShadowCastingMode.Off;
        line.receiveShadows = false;
        rubberLines[index] = line;
        return line;
    }

    private void UpdateRubberLine(int index)
    {
        LineRenderer line = rubberLines[index];
        if (line == null) return;
        float pulse = 1f + Mathf.Sin(Time.time * 30f + index) * 0.08f;
        line.startWidth = limbWidth * pulse;
        line.endWidth = limbWidth * 0.72f * pulse;
        line.SetPosition(0, attackOrigins[index]);
        line.SetPosition(1, attackTips[index]);
    }

    private void KeepInsideGround()
    {
        if (groundCollider == null) return;
        Bounds bounds = groundCollider.bounds;
        Vector3 position = body.position;
        float x = Mathf.Clamp(position.x,
            bounds.min.x + groundEdgePadding,
            bounds.max.x - groundEdgePadding);
        float z = Mathf.Clamp(position.z,
            bounds.min.z + groundEdgePadding,
            bounds.max.z - groundEdgePadding);
        if (Mathf.Approximately(position.x, x) &&
            Mathf.Approximately(position.z, z)) return;

        body.position = new Vector3(x, position.y, z);
        Vector3 velocity = body.linearVelocity;
        if ((position.x < x && velocity.x < 0f) ||
            (position.x > x && velocity.x > 0f)) velocity.x = 0f;
        if ((position.z < z && velocity.z < 0f) ||
            (position.z > z && velocity.z > 0f)) velocity.z = 0f;
        body.linearVelocity = velocity;
    }

    public void ResetControlInput()
    {
        for (int i = 0; i < signalHigh.Length; i++) signalHigh[i] = false;
        MoveInput = Vector2.zero;
        walkBlend = 0f;
        if (body != null)
        {
            body.linearVelocity = Vector3.zero;
            body.angularVelocity = Vector3.zero;
        }
    }

    public void CancelTrafficAttack()
    {
        if (IsRespawning) return;
        StopAllCoroutines();
        activeAttackCount = 0;
        walkBlend = 0f;
        for (int i = 0; i < attackBones.Length; i++)
        {
            boneOverrides[i] = false;
            if (attackBones[i] != null)
            {
                attackBones[i].localPosition = restingLocalPositions[i];
                attackBones[i].localRotation = restingLocalRotations[i];
                attackBones[i].localScale = restingLocalScales[i];
            }
            if (rubberLines[i] != null) rubberLines[i].enabled = false;
        }
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
                walkSpeed + hitKnockbackSpeed);
            nextHitKnockbackTime = Time.time + hitKnockbackCooldown;
        }
        if (Health <= 0f) StartCoroutine(DeathAndRespawn(attacker));
    }

    public void ReceiveBeamDamage(float damage)
    {
        ReceiveCombatDamage(damage, null);
    }

    public void ReceiveMilkHit(float damage)
    {
        ReceiveCombatDamage(damage, null);
    }

    public void ResetForNewMatch()
    {
        StopAllCoroutines();
        ResetControlInput();
        IsRespawning = false;
        Health = maximumHealth; stamina = maximumStamina;
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
        activeAttackCount = 0;
        for (int i = 0; i < attackBones.Length; i++)
        {
            boneOverrides[i] = false;
            if (attackBones[i] != null) {
                attackBones[i].localPosition = restingLocalPositions[i];
                attackBones[i].localRotation = restingLocalRotations[i];
                attackBones[i].localScale = restingLocalScales[i];
            }
            if (rubberLines[i] != null) rubberLines[i].enabled = false;
        }
    }

    private IEnumerator DeathAndRespawn(MonoBehaviour attacker)
    {
        IsRespawning = true;
        PopArtDeathTrail.Begin(transform);
        if (gameManager != null) gameManager.AwardPoint(attacker);
        capsule.enabled = false;
        body.constraints = RigidbodyConstraints.None;
        Vector3 launch = new Vector3(
            Random.Range(-1f, 1f),
            Random.Range(0.7f, 1.15f),
            Random.Range(-0.6f, 0.9f)).normalized;
        body.linearVelocity = launch * Random.Range(17f, 23f);
        body.angularVelocity = Random.insideUnitSphere * 8f;

        yield return new WaitForSeconds(1.15f);

        Health = maximumHealth;
        stamina = maximumStamina;
        transform.position = spawnPosition + Vector3.up * 11f;
        transform.rotation = Quaternion.Euler(0f, spawnRotation.eulerAngles.y, 0f);
        body.rotation = transform.rotation;
        body.linearVelocity = Vector3.down * 2f;
        body.angularVelocity = Vector3.zero;
        body.constraints = RigidbodyConstraints.FreezePositionX |
                           RigidbodyConstraints.FreezePositionZ |
                           RigidbodyConstraints.FreezeRotation;
        capsule.enabled = true;

        float landingTimeout = Time.time + 4.5f;
        while (Time.time < landingTimeout && !IsGroundedForRespawn())
            yield return null;

        body.linearVelocity = Vector3.zero;
        body.angularVelocity = Vector3.zero;
        body.constraints = RigidbodyConstraints.FreezeRotationX |
                           RigidbodyConstraints.FreezeRotationZ;
        KeepInsideGround();
        IsRespawning = false;
    }

    private bool IsGroundedForRespawn()
    {
        if (groundCollider != null)
        {
            Ray ray = new Ray(transform.position + Vector3.up * 0.35f, Vector3.down);
            return groundCollider.Raycast(ray, out RaycastHit hit, 0.75f);
        }
        return Physics.Raycast(transform.position + Vector3.up * 0.2f,
            Vector3.down, 0.65f, Physics.DefaultRaycastLayers,
            QueryTriggerInteraction.Ignore);
    }
}
