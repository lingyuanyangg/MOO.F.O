using UnityEngine;
using extOSC;

[DisallowMultipleComponent]
public class OSCCameraProjectileSpawner : MonoBehaviour
{
    [Header("OSC")]
    public OSCReceiver receiver;
    public string object1Address = "/o1";
    public string object2Address = "/o2";
    public string object3Address = "/o3";

    [Header("Scene References")]
    public Camera launchCamera;
    public Collider groundCollider;
    public GameObject object1Prefab;
    public GameObject object2Prefab;
    public GameObject object3Prefab;

    [Header("Throw")]
    public float visualSize = 1.35f;
    public float upwardLaunchSpeed = 2.6f;
    public float nearFenceZ = -10.9f;
    public float collisionSafetyDelay = 0.08f;
    public float damage = 24f;
    public float minimumZ = -8.8f;
    public float maximumZ = 7.7f;
    public float nearHalfWidth = 3.35f;
    public float farHalfWidth = 10.4f;

    private readonly bool[] signalHigh = new bool[3];
    private OSCBind object1Bind;
    private OSCBind object2Bind;
    private OSCBind object3Bind;

    private void Start()
    {
        if (receiver == null) receiver = FindFirstObjectByType<OSCReceiver>();
        if (launchCamera == null) launchCamera = Camera.main;

        if (receiver == null)
        {
            Debug.LogError("OSCCameraProjectileSpawner: OSCReceiver not found.");
            enabled = false;
            return;
        }

        object1Bind = receiver.Bind(object1Address, message => ReceiveSignal(0, message));
        object2Bind = receiver.Bind(object2Address, message => ReceiveSignal(1, message));
        object3Bind = receiver.Bind(object3Address, message => ReceiveSignal(2, message));
    }

    private void OnDestroy()
    {
        if (receiver == null) return;
        receiver.Unbind(object1Bind);
        receiver.Unbind(object2Bind);
        receiver.Unbind(object3Bind);
    }

    private void ReceiveSignal(int index, OSCMessage message)
    {
        float value = Read01(message);
        bool high = value > 0.5f;
        if (!isActiveAndEnabled)
        {
            signalHigh[index] = high;
            return;
        }
        if (high && !signalHigh[index]) Spawn(index);
        signalHigh[index] = high;
    }

    private static float Read01(OSCMessage message)
    {
        if (message.ToFloat(out float floatValue)) return Mathf.Clamp01(floatValue);
        if (message.ToInt(out int intValue)) return Mathf.Clamp01(intValue);
        return 0f;
    }

    public void Spawn(int index)
    {
        GameObject prefab = index == 0 ? object1Prefab : index == 1 ? object2Prefab : object3Prefab;
        if (prefab == null || launchCamera == null)
        {
            Debug.LogWarning("OSCCameraProjectileSpawner: missing prefab or camera.");
            return;
        }

        Vector3 origin = launchCamera.transform.position + launchCamera.transform.forward * 0.65f;
        Vector3 target = ChooseArenaTarget();
        Vector3 velocity = CalculateBallisticVelocity(
            origin,
            target,
            Mathf.Max(0f, upwardLaunchSpeed),
            out float flightDuration);

        var projectileObject = new GameObject(
            index == 0 ? "OSC Poop Projectile" :
            index == 1 ? "OSC Cat Box Projectile" :
            "OSC Win95 Projectile");
        projectileObject.transform.position = origin;

        GameObject visual = Instantiate(prefab, projectileObject.transform);
        visual.name = prefab.name + " Visual";
        visual.transform.localPosition = Vector3.zero;
        visual.transform.localRotation = Quaternion.identity;
        visual.transform.localScale = Vector3.one;
        NormalizeVisual(visual, projectileObject.transform, visualSize);
        visual.transform.localRotation = Quaternion.Euler(
            Random.Range(-18f, 18f),
            Random.Range(0f, 360f),
            Random.Range(-18f, 18f));
        var visualNormalizer = visual.AddComponent<OSCProjectileVisualNormalizer>();
        visualNormalizer.Initialize(projectileObject.transform, visualSize);

        foreach (Collider childCollider in visual.GetComponentsInChildren<Collider>(true))
            childCollider.enabled = false;
        foreach (Rigidbody childBody in visual.GetComponentsInChildren<Rigidbody>(true))
        {
            childBody.isKinematic = true;
            childBody.detectCollisions = false;
        }

        var sphere = projectileObject.AddComponent<SphereCollider>();
        sphere.radius = visualSize * 0.5f;
        sphere.isTrigger = true;

        var body = projectileObject.AddComponent<Rigidbody>();
        body.mass = 1.4f;
        body.useGravity = true;
        body.interpolation = RigidbodyInterpolation.Interpolate;
        body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        body.linearDamping = 0f;
        body.angularDamping = 0.65f;
        body.linearVelocity = velocity;
        body.angularVelocity = Vector3.up * Random.Range(-1.2f, 1.2f);

        float fenceFraction = Mathf.InverseLerp(origin.z, target.z, nearFenceZ);
        float collisionDelay = Mathf.Clamp(
            flightDuration * fenceFraction + collisionSafetyDelay,
            0.1f,
            flightDuration * 0.82f);

        var hazard = projectileObject.AddComponent<OSCThrownHazard>();
        hazard.damage = damage;
        hazard.Arm(sphere, groundCollider, collisionDelay);
    }

    private Vector3 ChooseArenaTarget()
    {
        float z = Random.Range(minimumZ, maximumZ);
        float t = Mathf.InverseLerp(minimumZ, maximumZ, z);
        float halfWidth = Mathf.Lerp(nearHalfWidth, farHalfWidth, t);
        float x = Random.Range(-halfWidth, halfWidth);
        float y = 0f;

        if (groundCollider != null)
        {
            Bounds bounds = groundCollider.bounds;
            Ray ray = new Ray(
                new Vector3(x, bounds.max.y + 15f, z),
                Vector3.down);
            if (groundCollider.Raycast(ray, out RaycastHit hit, bounds.size.y + 35f))
                y = hit.point.y + visualSize * 0.5f;
        }

        return new Vector3(x, y, z);
    }

    private static Vector3 CalculateBallisticVelocity(
        Vector3 origin,
        Vector3 target,
        float verticalSpeed,
        out float flightDuration)
    {
        float gravity = Mathf.Max(0.01f, -Physics.gravity.y);
        float verticalDisplacement = target.y - origin.y;
        float discriminant =
            verticalSpeed * verticalSpeed -
            2f * gravity * verticalDisplacement;
        flightDuration = (
            verticalSpeed + Mathf.Sqrt(Mathf.Max(0.01f, discriminant))) /
            gravity;
        flightDuration = Mathf.Clamp(flightDuration, 0.65f, 1.8f);

        Vector3 horizontalDisplacement = target - origin;
        horizontalDisplacement.y = 0f;
        return horizontalDisplacement / flightDuration +
               Vector3.up * verticalSpeed;
    }

    private static void NormalizeVisual(GameObject visual, Transform projectileRoot, float targetSize)
    {
        Bounds localBounds = new Bounds();
        bool hasBounds = false;

        foreach (MeshFilter filter in visual.GetComponentsInChildren<MeshFilter>(true))
        {
            if (filter.sharedMesh == null) continue;
            EncapsulateLocalBounds(
                filter.sharedMesh.bounds,
                filter.transform,
                visual.transform,
                ref localBounds,
                ref hasBounds);
        }

        foreach (SkinnedMeshRenderer skinned in visual.GetComponentsInChildren<SkinnedMeshRenderer>(true))
        {
            EncapsulateLocalBounds(
                skinned.localBounds,
                skinned.transform,
                visual.transform,
                ref localBounds,
                ref hasBounds);
        }

        if (!hasBounds) return;

        float largest = Mathf.Max(
            localBounds.size.x,
            localBounds.size.y,
            localBounds.size.z);
        if (largest <= 0.0001f) return;

        float uniformScale = targetSize / largest;
        visual.transform.localScale = Vector3.one * uniformScale;
        visual.transform.localPosition = -localBounds.center * uniformScale;
    }

    private static void EncapsulateLocalBounds(
        Bounds sourceBounds,
        Transform sourceTransform,
        Transform visualRoot,
        ref Bounds combined,
        ref bool hasBounds)
    {
        Vector3 min = sourceBounds.min;
        Vector3 max = sourceBounds.max;
        for (int i = 0; i < 8; i++)
        {
            Vector3 corner = new Vector3(
                (i & 1) == 0 ? min.x : max.x,
                (i & 2) == 0 ? min.y : max.y,
                (i & 4) == 0 ? min.z : max.z);
            Vector3 localPoint = visualRoot.InverseTransformPoint(
                sourceTransform.TransformPoint(corner));

            if (!hasBounds)
            {
                combined = new Bounds(localPoint, Vector3.zero);
                hasBounds = true;
            }
            else
            {
                combined.Encapsulate(localPoint);
            }
        }
    }
}
