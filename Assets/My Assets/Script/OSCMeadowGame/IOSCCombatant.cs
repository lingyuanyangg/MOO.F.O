using System.Collections.Generic;
using UnityEngine;

public interface IOSCCombatant
{
    Transform CombatTransform { get; }
    Collider CombatCollider { get; }
    float Health { get; }
    bool IsRespawning { get; }
    void ReceiveCombatDamage(float damage, MonoBehaviour attacker);
}

public static class OSCCombatFeedback
{
    public static void ApplyHorizontalKnockback(
        Rigidbody body,
        Transform victim,
        MonoBehaviour attacker,
        float knockbackSpeed,
        float maximumHorizontalSpeed)
    {
        if (body == null || victim == null || attacker == null ||
            knockbackSpeed <= 0f)
            return;

        Vector3 direction = victim.position - attacker.transform.position;
        direction.y = 0f;
        if (direction.sqrMagnitude < 0.0025f &&
            attacker.TryGetComponent(out Rigidbody attackerBody))
        {
            direction = attackerBody.linearVelocity;
            direction.y = 0f;
        }
        if (direction.sqrMagnitude < 0.0025f)
            direction = -victim.forward;

        direction.Normalize();
        Vector3 velocity = body.linearVelocity;
        Vector3 horizontal = new Vector3(velocity.x, 0f, velocity.z) +
                             direction * knockbackSpeed;
        horizontal = Vector3.ClampMagnitude(
            horizontal,
            Mathf.Max(knockbackSpeed, maximumHorizontalSpeed));
        body.linearVelocity = new Vector3(
            horizontal.x,
            velocity.y,
            horizontal.z);
    }
}

public static class OSCCombatTargeting
{
    public static IOSCCombatant FindRandomEnemy(
        MonoBehaviour attacker,
        float maximumHorizontalDistance = float.PositiveInfinity)
    {
        if (attacker == null) return null;

        var validTargets = new List<IOSCCombatant>(3);
        float maximumSqrDistance = maximumHorizontalDistance * maximumHorizontalDistance;
        Vector3 origin = attacker.transform.position;

        Consider(Object.FindObjectsByType<OSCCowController>(FindObjectsSortMode.None));
        Consider(Object.FindObjectsByType<OSCUFOController>(FindObjectsSortMode.None));
        Consider(Object.FindObjectsByType<OSCAlienController>(FindObjectsSortMode.None));
        return validTargets.Count > 0
            ? validTargets[Random.Range(0, validTargets.Count)]
            : null;

        void Consider<T>(T[] candidates) where T : MonoBehaviour, IOSCCombatant
        {
            foreach (T candidate in candidates)
            {
                if (candidate == null || candidate == attacker ||
                    !candidate.isActiveAndEnabled || candidate.IsRespawning)
                    continue;

                Vector3 offset = candidate.transform.position - origin;
                float sqrDistance = offset.x * offset.x + offset.z * offset.z;
                if (sqrDistance > maximumSqrDistance) continue;
                validTargets.Add(candidate);
            }
        }
    }

    public static float HorizontalDistance(Vector3 a, Vector3 b)
    {
        float x = a.x - b.x;
        float z = a.z - b.z;
        return Mathf.Sqrt(x * x + z * z);
    }
}
