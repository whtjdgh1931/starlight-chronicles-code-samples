using DG.Tweening;
using UnityEngine;

/// <summary>보스 베기와 발사 섬광의 렌더러 재사용</summary>
internal sealed class BossAttackBurst
{
    internal enum Kind
    {
        SlashLeft,
        SlashRight,
        Muzzle
    }

    private sealed class Slot
    {
        public LineRenderer Renderer;
        public Tween Tween;
    }

    private const int SlashPointCount = 17;
    private const int MuzzlePointCount = 9;
    private const float SlashDuration = 0.18f;
    private const float MuzzleDuration = 0.12f;

    private static readonly Color SlashCore = new Color(1f, 0.93f, 0.70f);
    private static readonly Color SlashTip = new Color(1f, 0.44f, 0.14f);
    private static readonly Color MuzzleCore = new Color(0.89f, 0.98f, 1f);
    private static readonly Color MuzzleTip = new Color(0.27f, 0.77f, 1f);
    private static readonly AnimationCurve MuzzleWidth = AnimationCurve.Constant(0f, 1f, 1f);
    private static readonly AnimationCurve SlashWidth = new AnimationCurve(
        new Keyframe(0f, 0.10f), new Keyframe(0.40f, 1f), new Keyframe(1f, 0.08f));

    private readonly Transform _parent;
    private readonly SpriteRenderer _sortingReference;
    private readonly Material _material;
    // 교차 탄막의 두 발사점을 동시에 표시
    private readonly Slot[] _slots = { new Slot(), new Slot() };
    private int _nextSlot;

    internal BossAttackBurst(Transform parent, SpriteRenderer sortingReference, Material material)
    {
        _parent = parent;
        _sortingReference = sortingReference;
        _material = material;
    }

    internal void Play(Kind kind, Vector2 position, Vector2 sizeOrDirection)
    {
        Slot slot = _slots[_nextSlot];
        _nextSlot = (_nextSlot + 1) % _slots.Length;
        slot.Tween?.Kill();
        if (slot.Renderer == null)
        {
            slot.Renderer = CreateRenderer();
        }

        bool isMuzzle = kind == Kind.Muzzle;
        LineRenderer renderer = slot.Renderer;
        renderer.positionCount = isMuzzle ? MuzzlePointCount : SlashPointCount;
        renderer.widthCurve = isMuzzle ? MuzzleWidth : SlashWidth;
        renderer.enabled = true;
        RenderFrame(renderer, kind, position, sizeOrDirection, 0f);
        slot.Tween = DOVirtual.Float(0f, 1f, isMuzzle ? MuzzleDuration : SlashDuration,
            progress => RenderFrame(renderer, kind, position, sizeOrDirection, progress))
            .SetEase(Ease.OutQuad)
            .OnComplete(() =>
            {
                renderer.enabled = false;
                slot.Tween = null;
            });
    }

    internal void Clear()
    {
        foreach (Slot slot in _slots)
        {
            slot.Tween?.Kill();
            slot.Tween = null;
            if (slot.Renderer != null)
            {
                slot.Renderer.enabled = false;
            }
        }

        _nextSlot = 0;
    }

    private LineRenderer CreateRenderer()
    {
        var burstObject = new GameObject("BossAttackBurst");
        burstObject.layer = _sortingReference.gameObject.layer;
        burstObject.transform.SetParent(_parent, false);

        LineRenderer renderer = burstObject.AddComponent<LineRenderer>();
        renderer.sharedMaterial = _material;
        renderer.useWorldSpace = true;
        renderer.numCornerVertices = 3;
        renderer.numCapVertices = 3;
        renderer.sortingLayerID = _sortingReference.sortingLayerID;
        renderer.sortingOrder = _sortingReference.sortingOrder + 10;
        return renderer;
    }

    private static void RenderFrame(LineRenderer renderer, Kind kind, Vector2 origin, Vector2 sizeOrDirection, float progress)
    {
        bool isMuzzle = kind == Kind.Muzzle;
        Color core = isMuzzle ? MuzzleCore : SlashCore;
        Color tip = isMuzzle ? MuzzleTip : SlashTip;
        core.a = 1f - progress * progress;
        tip.a = core.a * 0.85f;
        renderer.startColor = core;
        renderer.endColor = tip;

        if (isMuzzle)
        {
            DrawMuzzle(renderer, origin, sizeOrDirection, progress);
            return;
        }

        float facing = kind == Kind.SlashLeft ? -1f : 1f;
        DrawSlash(renderer, origin, sizeOrDirection, facing, progress);
    }

    private static void DrawSlash(LineRenderer renderer, Vector2 center, Vector2 size, float facing, float progress)
    {
        float halfHeight = Mathf.Min(size.y * 0.48f, 2.4f);
        float halfWidth = Mathf.Min(size.x * 0.48f, 3f);
        renderer.widthMultiplier = Mathf.Clamp(halfHeight * 0.32f, 0.14f, 0.65f) * Mathf.Lerp(1f, 0.45f, progress);
        float startAngle = Mathf.Lerp(-12f, -72f, progress);
        float endAngle = Mathf.Lerp(48f, 76f, progress);

        for (int i = 0; i < SlashPointCount; i++)
        {
            float fraction = i / (float)(SlashPointCount - 1);
            float angle = Mathf.Lerp(startAngle, endAngle, fraction) * Mathf.Deg2Rad;
            var offset = new Vector2(facing * halfWidth * (2f * Mathf.Cos(angle) - 1.1f), halfHeight * Mathf.Sin(angle));
            renderer.SetPosition(i, center + offset);
        }
    }

    private static void DrawMuzzle(LineRenderer renderer, Vector2 origin, Vector2 direction, float progress)
    {
        Vector2 forward = direction == Vector2.zero ? Vector2.right : direction.normalized;
        var up = new Vector2(-forward.y, forward.x);
        float radius = Mathf.Lerp(0.35f, 0.90f, progress);
        renderer.widthMultiplier = Mathf.Lerp(0.20f, 0.06f, progress);

        for (int i = 0; i < MuzzlePointCount; i++)
        {
            float angle = i * Mathf.PI * 0.25f;
            float pointRadius = i % 2 == 0 ? radius : radius * 0.28f;
            Vector2 offset = forward * (Mathf.Cos(angle) * pointRadius * 1.4f) + up * (Mathf.Sin(angle) * pointRadius);
            renderer.SetPosition(i, origin + offset);
        }
    }
}
