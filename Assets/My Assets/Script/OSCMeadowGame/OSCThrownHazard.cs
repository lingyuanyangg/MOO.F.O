using System.Collections;
using UnityEngine;

[DisallowMultipleComponent]
public class OSCThrownHazard : MonoBehaviour
{
    public float damage = 24f;
    public float groundExplosionDelay = 2f;
    public float maximumLifetime = 8f;

    [Header("Post-impact motion")]
    public float maximumPlanarSpeed = 3.2f;
    public float maximumUpwardBounceSpeed = 1.25f;
    public float maximumAngularSpeed = 4f;
    public float postImpactLinearDamping = 0.7f;
    public float postImpactAngularDamping = 1.35f;

    public bool HasEnvironmentContact { get; private set; }
    public bool HasGroundContact { get; private set; }
    public float GroundContactTime { get; private set; } = -1f;

    private bool exploded;
    private Collider projectileCollider;
    private Collider groundCollider;
    private Rigidbody body;
    private Vector2 lockedPlanarVelocity;
    private Coroutine delayedExplosion;
    private PhysicsMaterial runtimePhysicsMaterial;

    public void Arm(
        Collider colliderToArm,
        Collider sceneGroundCollider,
        float delay)
    {
        projectileCollider = colliderToArm;
        groundCollider = sceneGroundCollider;
        body = GetComponent<Rigidbody>();

        if (body != null)
        {
            lockedPlanarVelocity = new Vector2(
                body.linearVelocity.x,
                body.linearVelocity.z);
        }

        runtimePhysicsMaterial =
            new PhysicsMaterial("Runtime OSC Hazard Bounce");
        runtimePhysicsMaterial.dynamicFriction = 0.62f;
        runtimePhysicsMaterial.staticFriction = 0.72f;
        runtimePhysicsMaterial.bounciness = 0.08f;
        runtimePhysicsMaterial.frictionCombine =
            PhysicsMaterialCombine.Average;
        runtimePhysicsMaterial.bounceCombine =
            PhysicsMaterialCombine.Minimum;
        if (projectileCollider != null)
            projectileCollider.sharedMaterial = runtimePhysicsMaterial;

        StartCoroutine(EnableCollisionAfter(delay));
        Destroy(gameObject, maximumLifetime);
    }

    private void FixedUpdate()
    {
        if (exploded || body == null) return;

        if (HasEnvironmentContact)
        {
            LimitPostImpactMotion();
            return;
        }

        Vector3 velocity = body.linearVelocity;
        velocity.x = lockedPlanarVelocity.x;
        velocity.z = lockedPlanarVelocity.y;
        body.linearVelocity = velocity;
    }

    private void LimitPostImpactMotion()
    {
        Vector3 velocity = body.linearVelocity;
        Vector2 planar = new Vector2(velocity.x, velocity.z);
        float planarLimit = Mathf.Max(0f, maximumPlanarSpeed);
        if (planar.sqrMagnitude > planarLimit * planarLimit)
        {
            planar = planar.normalized * planarLimit;
            velocity.x = planar.x;
            velocity.z = planar.y;
        }

        velocity.y = Mathf.Min(
            velocity.y,
            Mathf.Max(0f, maximumUpwardBounceSpeed));
        body.linearVelocity = velocity;

        Vector3 angularVelocity = body.angularVelocity;
        float angularLimit = Mathf.Max(0f, maximumAngularSpeed);
        if (angularVelocity.sqrMagnitude > angularLimit * angularLimit)
            body.angularVelocity = angularVelocity.normalized * angularLimit;
    }

    private IEnumerator EnableCollisionAfter(float delay)
    {
        yield return new WaitForSeconds(delay);
        if (projectileCollider != null)
            projectileCollider.isTrigger = false;
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (exploded) return;

        Vector3 point = collision.contactCount > 0
            ? collision.GetContact(0).point
            : transform.position;

        OSCCowController cow =
            collision.collider.GetComponentInParent<OSCCowController>();
        if (cow != null)
        {
            cow.ReceiveCombatDamage(damage, this);
            Explode(point);
            return;
        }

        OSCUFOController ufo =
            collision.collider.GetComponentInParent<OSCUFOController>();
        if (ufo != null)
        {
            ufo.ReceiveCombatDamage(damage, this);
            Explode(point);
            return;
        }

        OSCAlienController alien =
            collision.collider.GetComponentInParent<OSCAlienController>();
        if (alien != null)
        {
            alien.ReceiveCombatDamage(damage, this);
            Explode(point);
            return;
        }

        HasEnvironmentContact = true;
        if (body != null)
        {
            body.linearDamping = postImpactLinearDamping;
            body.angularDamping = postImpactAngularDamping;
            body.maxAngularVelocity = maximumAngularSpeed;
            LimitPostImpactMotion();
        }

        if (IsGroundCollider(collision.collider))
            BeginGroundExplosionCountdown();
    }

    private bool IsGroundCollider(Collider other)
    {
        if (groundCollider == null) return true;
        if (other == groundCollider) return true;
        return other.transform.IsChildOf(groundCollider.transform) ||
               groundCollider.transform.IsChildOf(other.transform);
    }

    private void BeginGroundExplosionCountdown()
    {
        if (HasGroundContact || exploded) return;
        HasGroundContact = true;
        GroundContactTime = Time.time;
        delayedExplosion = StartCoroutine(ExplodeAfterGroundDelay());
    }

    private IEnumerator ExplodeAfterGroundDelay()
    {
        yield return new WaitForSeconds(groundExplosionDelay);
        if (!exploded) Explode(transform.position);
    }

    public void ResolveContact(GameObject contactObject, Vector3 point)
    {
        if (exploded || contactObject == null) return;

        OSCCowController cow =
            contactObject.GetComponentInParent<OSCCowController>();
        if (cow != null)
        {
            cow.ReceiveCombatDamage(damage, this);
            Explode(point);
            return;
        }

        OSCUFOController ufo =
            contactObject.GetComponentInParent<OSCUFOController>();
        if (ufo != null)
        {
            ufo.ReceiveCombatDamage(damage, this);
            Explode(point);
            return;
        }


        OSCAlienController alien =
            contactObject.GetComponentInParent<OSCAlienController>();
        if (alien != null)
        {
            alien.ReceiveCombatDamage(damage, this);
            Explode(point);
            return;
        }

        HasEnvironmentContact = true;
        Collider contactCollider = contactObject.GetComponent<Collider>();
        if (contactCollider == null || IsGroundCollider(contactCollider))
            BeginGroundExplosionCountdown();
    }

    private void Explode(Vector3 position)
    {
        if (exploded) return;
        exploded = true;
        if (delayedExplosion != null) StopCoroutine(delayedExplosion);
        SpawnExplosion(position);
        Destroy(gameObject);
    }

    private static void SpawnExplosion(Vector3 position)
    {
        var explosion = new GameObject("OSC Projectile Explosion");
        explosion.SetActive(false);
        explosion.transform.position = position;

        var particles = explosion.AddComponent<ParticleSystem>();
        var main = particles.main;
        main.duration = 0.32f;
        main.loop = false;
        main.startLifetime =
            new ParticleSystem.MinMaxCurve(0.28f, 0.72f);
        main.startSpeed =
            new ParticleSystem.MinMaxCurve(3.5f, 8.5f);
        main.startSize =
            new ParticleSystem.MinMaxCurve(0.12f, 0.42f);
        main.startColor = new ParticleSystem.MinMaxGradient(
            new Color(1f, 0.92f, 0.25f, 1f),
            new Color(1f, 0.2f, 0.05f, 1f));
        main.gravityModifier = 0.65f;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = 64;

        var emission = particles.emission;
        emission.rateOverTime = 0f;
        emission.SetBursts(
            new[] { new ParticleSystem.Burst(0f, 42) });

        var shape = particles.shape;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = 0.18f;

        var renderer = particles.GetComponent<ParticleSystemRenderer>();
        Shader shader =
            Shader.Find("Universal Render Pipeline/Particles/Unlit");
        if (shader == null)
            shader = Shader.Find("Universal Render Pipeline/Unlit");
        var material = new Material(shader);
        if (material.HasProperty("_BaseColor"))
            material.SetColor(
                "_BaseColor",
                new Color(2.5f, 0.65f, 0.08f, 1f));
        renderer.material = material;

        var lightObject = new GameObject("Explosion Flash");
        lightObject.transform.SetParent(explosion.transform, false);
        var light = lightObject.AddComponent<Light>();
        light.type = LightType.Point;
        light.color = new Color(1f, 0.38f, 0.08f);
        light.intensity = 7f;
        light.range = 4f;
        light.shadows = LightShadows.None;

        explosion.SetActive(true);
        particles.Play();
        Destroy(explosion, 1.25f);
    }

    private void OnDestroy()
    {
        if (runtimePhysicsMaterial != null)
            Destroy(runtimePhysicsMaterial);
    }
}
