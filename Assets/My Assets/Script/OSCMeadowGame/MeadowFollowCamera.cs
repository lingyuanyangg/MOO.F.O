using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Camera))]
public class MeadowFollowCamera : MonoBehaviour
{
    [Header("Framing Targets")]
    public Transform target;
    public Transform secondaryTarget;
    [Tooltip("Fallback world-space direction from the framed midpoint toward the camera.")]
    public Vector3 localOffset = new Vector3(4.2f, 3.4f, -6.3f);
    public Vector3 lookOffset = new Vector3(0f, 0.82f, 0f);

    [Header("Automatic Framing")]
    public float minimumDistance = 11.5f;
    public float maximumDistance = 65f;
    public float framingRadiusPadding = 0.8f;
    [Range(0.45f, 0.95f)] public float framingFill = 0.9f;
    [Range(0.02f, 0.2f)] public float viewportSafetyMargin = 0.08f;

    [Header("Motion")]
    public float positionSmoothTime = 0.12f;
    public float rotationSharpness = 11f;
    public float minimumObstacleDistance = 0.45f;

    private Camera controlledCamera;
    private Vector3 positionVelocity;
    private MeshRenderer[] primaryRenderers;
    private MeshRenderer[] secondaryRenderers;

    private void Awake()
    {
        controlledCamera = GetComponent<Camera>();
    }

    private void OnEnable()
    {
        positionVelocity = Vector3.zero;
    }

    private void Start()
    {
        CacheRenderers();
        // Snap after every actor and Rigidbody has completed initialization.
        Snap();
    }

    private void LateUpdate()
    {
        FrameTargets(false);
    }

    private void CacheRenderers()
    {
        primaryRenderers = target != null
            ? target.GetComponentsInChildren<MeshRenderer>(true)
            : null;
        secondaryRenderers = secondaryTarget != null
            ? secondaryTarget.GetComponentsInChildren<MeshRenderer>(true)
            : null;
    }

    private void FrameTargets(bool instant)
    {
        if (target == null) return;
        if (controlledCamera == null) controlledCamera = GetComponent<Camera>();
        if (primaryRenderers == null) CacheRenderers();

        Bounds primaryBounds;
        bool hasPrimaryBounds = TryGetBounds(primaryRenderers, out primaryBounds);
        Vector3 primaryPoint = hasPrimaryBounds
            ? primaryBounds.center
            : target.position + lookOffset;

        Bounds secondaryBounds = new Bounds();
        bool hasSecondary = secondaryTarget != null &&
                            secondaryTarget.gameObject.activeInHierarchy;
        bool hasSecondaryBounds = hasSecondary &&
                                  TryGetBounds(secondaryRenderers, out secondaryBounds);
        Vector3 secondaryPoint = hasSecondaryBounds
            ? secondaryBounds.center
            : hasSecondary ? secondaryTarget.position : primaryPoint;

        Bounds combinedBounds = hasPrimaryBounds
            ? primaryBounds
            : new Bounds(primaryPoint, Vector3.one * 1.5f);

        if (hasSecondary)
        {
            if (hasSecondaryBounds) combinedBounds.Encapsulate(secondaryBounds);
            else combinedBounds.Encapsulate(secondaryPoint);
        }

        Vector3 lookPoint = combinedBounds.center;
        Vector3 offsetDirection = localOffset.sqrMagnitude > 0.001f
            ? localOffset.normalized
            : new Vector3(0.42f, 0.34f, -0.84f).normalized;

        if (hasSecondary)
        {
            Vector3 planarBattleDirection = Vector3.ProjectOnPlane(
                secondaryPoint - primaryPoint,
                Vector3.up);

            // Follow from behind the cow toward the UFO. This stays independent from
            // imported model axes and gives a stable opening view.
            if (planarBattleDirection.sqrMagnitude > 0.25f)
            {
                planarBattleDirection.Normalize();
                Vector3 side = Vector3.Cross(Vector3.up, planarBattleDirection) * 0.18f;
                offsetDirection = (-planarBattleDirection + side + Vector3.up * 0.38f).normalized;
            }
        }

        Quaternion framingRotation = Quaternion.LookRotation(-offsetDirection, Vector3.up);
        float distance = CalculateRequiredDistance(
            lookPoint,
            framingRotation,
            primaryBounds,
            hasPrimaryBounds,
            secondaryBounds,
            hasSecondaryBounds);

        Vector3 desired = lookPoint + offsetDirection * distance;
        Vector3 castDirection = desired - lookPoint;
        float castDistance = castDirection.magnitude;

        if (castDistance > 0.01f && Physics.SphereCast(
            lookPoint,
            0.22f,
            castDirection.normalized,
            out RaycastHit hit,
            castDistance,
            Physics.DefaultRaycastLayers,
            QueryTriggerInteraction.Ignore))
        {
            bool hitPrimary = hit.transform == target || hit.transform.IsChildOf(target);
            bool hitSecondary = secondaryTarget != null &&
                (hit.transform == secondaryTarget || hit.transform.IsChildOf(secondaryTarget));

            if (!hitPrimary && !hitSecondary)
                desired = hit.point + hit.normal * minimumObstacleDistance;
        }

        if (instant)
        {
            ApplyImmediate(desired, lookPoint);
            return;
        }

        transform.position = Vector3.SmoothDamp(
            transform.position,
            desired,
            ref positionVelocity,
            positionSmoothTime);

        Quaternion desiredRotation = Quaternion.LookRotation(
            lookPoint - transform.position,
            Vector3.up);
        transform.rotation = Quaternion.Slerp(
            transform.rotation,
            desiredRotation,
            1f - Mathf.Exp(-rotationSharpness * Time.deltaTime));

        // Fast OSC input can briefly outrun smoothing. Snap only at the safety
        // margin so the complete cow and UFO meshes never leave the frame.
        if (!IsBoundsSafelyVisible(primaryBounds, hasPrimaryBounds) ||
            (hasSecondary && !IsBoundsSafelyVisible(secondaryBounds, hasSecondaryBounds)))
        {
            ApplyImmediate(desired, lookPoint);
        }
    }

    private float CalculateRequiredDistance(
        Vector3 lookPoint,
        Quaternion cameraRotation,
        Bounds primaryBounds,
        bool hasPrimaryBounds,
        Bounds secondaryBounds,
        bool hasSecondaryBounds)
    {
        float verticalHalfFov = controlledCamera.fieldOfView * Mathf.Deg2Rad * 0.5f;
        float horizontalHalfFov = Mathf.Atan(
            Mathf.Tan(verticalHalfFov) * controlledCamera.aspect);
        float safeFill = Mathf.Clamp01(1f - viewportSafetyMargin * 2f) * framingFill;
        float verticalSlope = Mathf.Max(0.1f, Mathf.Tan(verticalHalfFov) * safeFill);
        float horizontalSlope = Mathf.Max(0.1f, Mathf.Tan(horizontalHalfFov) * safeFill);
        Quaternion inverseRotation = Quaternion.Inverse(cameraRotation);

        float required = minimumDistance;
        if (hasPrimaryBounds)
            required = AccumulateRequiredDistance(
                required,
                primaryBounds,
                lookPoint,
                inverseRotation,
                horizontalSlope,
                verticalSlope);
        if (hasSecondaryBounds)
            required = AccumulateRequiredDistance(
                required,
                secondaryBounds,
                lookPoint,
                inverseRotation,
                horizontalSlope,
                verticalSlope);

        return Mathf.Clamp(required + framingRadiusPadding, minimumDistance, maximumDistance);
    }

    private static float AccumulateRequiredDistance(
        float required,
        Bounds bounds,
        Vector3 lookPoint,
        Quaternion inverseRotation,
        float horizontalSlope,
        float verticalSlope)
    {
        Vector3 min = bounds.min;
        Vector3 max = bounds.max;

        for (int i = 0; i < 8; i++)
        {
            Vector3 corner = new Vector3(
                (i & 1) == 0 ? min.x : max.x,
                (i & 2) == 0 ? min.y : max.y,
                (i & 4) == 0 ? min.z : max.z);
            Vector3 local = inverseRotation * (corner - lookPoint);
            required = Mathf.Max(required, Mathf.Abs(local.x) / horizontalSlope - local.z);
            required = Mathf.Max(required, Mathf.Abs(local.y) / verticalSlope - local.z);
        }

        return required;
    }

    private static bool TryGetBounds(MeshRenderer[] renderers, out Bounds bounds)
    {
        bounds = new Bounds();
        bool found = false;
        if (renderers == null) return false;

        foreach (MeshRenderer meshRenderer in renderers)
        {
            if (meshRenderer == null || !meshRenderer.enabled ||
                !meshRenderer.gameObject.activeInHierarchy)
                continue;

            if (!found)
            {
                bounds = meshRenderer.bounds;
                found = true;
            }
            else
            {
                bounds.Encapsulate(meshRenderer.bounds);
            }
        }

        return found;
    }

    private bool IsBoundsSafelyVisible(Bounds bounds, bool hasBounds)
    {
        if (!hasBounds) return true;

        Vector3 min = bounds.min;
        Vector3 max = bounds.max;
        float margin = viewportSafetyMargin;

        for (int i = 0; i < 8; i++)
        {
            Vector3 corner = new Vector3(
                (i & 1) == 0 ? min.x : max.x,
                (i & 2) == 0 ? min.y : max.y,
                (i & 4) == 0 ? min.z : max.z);
            Vector3 viewport = controlledCamera.WorldToViewportPoint(corner);

            if (viewport.z <= controlledCamera.nearClipPlane ||
                viewport.x < margin || viewport.x > 1f - margin ||
                viewport.y < margin || viewport.y > 1f - margin)
                return false;
        }

        return true;
    }

    private void ApplyImmediate(Vector3 position, Vector3 lookPoint)
    {
        transform.position = position;
        positionVelocity = Vector3.zero;
        transform.rotation = Quaternion.LookRotation(
            lookPoint - transform.position,
            Vector3.up);
    }

    public void Snap()
    {
        FrameTargets(true);
    }
}
