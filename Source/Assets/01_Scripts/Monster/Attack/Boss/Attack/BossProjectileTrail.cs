using UnityEngine;

/// <summary>싱글·협동 보스 투사체의 짧은 잔상과 풀 반환 정리</summary>
internal static class BossProjectileTrail
{
    private const float Duration = 0.075f;
    private const float HeadWidth = 0.22f;

    internal static TrailRenderer Play(Transform projectile, TrailRenderer trail)
    {
        if (trail == null)
        {
            trail = Create(projectile);
        }

        trail.Clear();
        trail.emitting = true;
        return trail;
    }

    internal static void Stop(TrailRenderer trail)
    {
        if (trail != null)
        {
            trail.emitting = false;
            trail.Clear();
        }
    }

    private static TrailRenderer Create(Transform projectile)
    {
        SpriteRenderer visual = projectile.GetComponentInChildren<SpriteRenderer>(true);
        if (visual == null)
        {
            throw new MissingReferenceException($"{projectile.name}: 잔상 기준 SpriteRenderer 필요");
        }

        TrailRenderer trail = projectile.gameObject.AddComponent<TrailRenderer>();
        trail.sharedMaterial = visual.sharedMaterial;
        trail.sortingLayerID = visual.sortingLayerID;
        trail.sortingOrder = visual.sortingOrder - 1;
        trail.time = Duration;
        trail.minVertexDistance = 0.04f;
        trail.startWidth = HeadWidth;
        trail.endWidth = 0f;
        trail.startColor = new Color(0.66f, 0.93f, 1f, 0.8f);
        trail.endColor = new Color(0.26f, 0.62f, 1f, 0f);
        trail.autodestruct = false;
        return trail;
    }
}
