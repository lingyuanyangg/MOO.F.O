using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Rigidbody), typeof(SphereCollider))]
public class UFOPoopBomb : MonoBehaviour
{
    public float damage = 14f;
    public float blastRadius = 2.25f;
    public float maximumLifetime = 8f;

    private OSCUFOController owner;
    private Collider groundCollider;
    private bool exploded;

    public void Initialize(
        OSCUFOController source,
        Collider sceneGroundCollider,
        float explosionDamage,
        float explosionRadius)
    {
        owner = source;
        groundCollider = sceneGroundCollider;
        damage = Mathf.Max(0f, explosionDamage);
        blastRadius = Mathf.Max(0.25f, explosionRadius);
        Destroy(gameObject, maximumLifetime);
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (exploded || !IsGround(collision.collider)) return;

        Vector3 point = collision.contactCount > 0
            ? collision.GetContact(0).point
            : transform.position;
        Explode(point);
    }

    private bool IsGround(Collider other)
    {
        if (other == null) return false;
        if (groundCollider == null) return true;
        if (other == groundCollider) return true;
        return other.transform.IsChildOf(groundCollider.transform) ||
               groundCollider.transform.IsChildOf(other.transform);
    }

    private void Explode(Vector3 point)
    {
        if (exploded) return;
        exploded = true;
        ApplyAreaDamage(point);
        SpawnExplosion(point);
        PopArtShockwave.Spawn(point, blastRadius);
        PopArtDotMatrixBurst.Spawn(point, blastRadius);
        Destroy(gameObject);
    }

    private void ApplyAreaDamage(Vector3 point)
    {
        Collider[] hits = Physics.OverlapSphere(
            point,
            blastRadius,
            Physics.AllLayers,
            QueryTriggerInteraction.Ignore);
        var damaged = new HashSet<MonoBehaviour>();

        foreach (Collider hit in hits)
        {
            MonoBehaviour target =
                hit.GetComponentInParent<OSCCowController>();
            if (target == null)
                target = hit.GetComponentInParent<OSCAlienController>();
            if (target == null || !damaged.Add(target)) continue;

            if (target is IOSCCombatant combatant &&
                !combatant.IsRespawning)
                combatant.ReceiveCombatDamage(damage, owner);
        }
    }

    private void SpawnExplosion(Vector3 point)
    {
        var explosion = new GameObject("Poop Ground Explosion");
        explosion.SetActive(false);
        explosion.transform.position = point + Vector3.up * 0.08f;

        var particles = explosion.AddComponent<ParticleSystem>();
        var main = particles.main;
        main.duration = 0.28f;
        main.loop = false;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.25f, 0.58f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(2.2f, 5.2f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.12f, 0.34f);
        main.startColor = new ParticleSystem.MinMaxGradient(
            new Color(0.32f, 0.055f, 0.012f, 1f),
            new Color(1.55f, 3.2f, 0.025f, 1f));
        main.gravityModifier = 0.9f;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = 36;

        var emission = particles.emission;
        emission.rateOverTime = 0f;
        emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 28) });

        var shape = particles.shape;
        shape.shapeType = ParticleSystemShapeType.Hemisphere;
        shape.radius = 0.14f;

        var renderer = particles.GetComponent<ParticleSystemRenderer>();
        Shader shader =
            Shader.Find("Universal Render Pipeline/Particles/Unlit");
        if (shader == null)
            shader = Shader.Find("Universal Render Pipeline/Unlit");
        var material = new Material(shader);
        if (material.HasProperty("_BaseColor"))
            material.SetColor(
                "_BaseColor",
                Color.white);
        renderer.material = material;

        var lightObject = new GameObject("Poop Explosion Flash");
        lightObject.transform.SetParent(explosion.transform, false);
        var light = lightObject.AddComponent<Light>();
        light.type = LightType.Point;
        light.color = new Color(1f, 0.05f, 0.68f);
        light.intensity = 4.2f;
        light.range = blastRadius * 1.25f;
        light.shadows = LightShadows.None;

        explosion.SetActive(true);
        particles.Play();
        Destroy(explosion, 1.1f);
    }
}
