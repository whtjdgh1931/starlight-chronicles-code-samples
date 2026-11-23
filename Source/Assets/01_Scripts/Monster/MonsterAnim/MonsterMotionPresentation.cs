using DG.Tweening;
using UnityEngine;

/// <summary>일반 몬스터의 이동·공격·피격·사망 시각 반응</summary>
internal sealed class MonsterMotionPresentation
{
    private static readonly Vector3 StepScale = new Vector3(1.04f, 0.96f, 1f);
    private static readonly Vector3 WindupScale = new Vector3(1.09f, 0.91f, 1f);
    private static readonly Vector3 ReleaseScale = new Vector3(0.95f, 1.08f, 1f);
    private static readonly Vector3 HitScale = new Vector3(1.13f, 0.88f, 1f);
    private static readonly Vector3 DeathScale = new Vector3(1.12f, 0.88f, 1f);
    private static readonly Color HitTint = new Color(1f, 0.62f, 0.46f);
    private static readonly Color ReleaseTint = new Color(1f, 0.86f, 0.48f);
    private const float FlinchDistance = 0.055f;

    private readonly Transform _poseRoot;
    private readonly SpriteRenderer[] _sprites;
    private readonly Color[] _flashBaseColors;
    private Sequence _pose;
    private Tween _flash;
    private bool _isWindingUp;

    internal MonsterMotionPresentation(Transform visualRoot, bool canOffsetVisual)
    {
        _sprites = visualRoot.GetComponentsInChildren<SpriteRenderer>(true);
        _flashBaseColors = new Color[_sprites.Length];
        if (canOffsetVisual)
        {
            _poseRoot = CreatePoseRoot(visualRoot);
        }
    }

    internal void PlayStep()
    {
        PlayPose(StepScale, 0f, 0.045f, 0.12f);
    }

    internal void PlayWindup(float direction)
    {
        if (_poseRoot == null) { return; }

        PlayPose(WindupScale, -direction * 0.045f, 0.10f);
        _isWindingUp = true;
    }

    internal void PlayAttackRelease(float direction, bool isRanged)
    {
        if (_poseRoot == null)
        {
            if (_flash == null) { PlayFlash(ReleaseTint, 0.42f, 0.12f); }
            return;
        }

        float offset = direction * (isRanged ? -0.065f : 0.085f);
        PlayPose(ReleaseScale, offset, 0.035f, 0.14f);
    }

    internal void PlayHit(float hitDirection)
    {
        PlayPose(HitScale, hitDirection * FlinchDistance, 0.035f, 0.12f);
    }

    internal void PlayDeath()
    {
        PlayPose(DeathScale, 0f, 0.16f);
    }

    internal void PlayDamageFlash(bool isFinalHit)
    {
        PlayFlash(HitTint, isFinalHit ? 0.86f : 0.62f, isFinalHit ? 0.14f : 0.10f);
    }

    private void PlayFlash(Color tint, float strength, float duration)
    {
        if (_sprites.Length == 0) { return; }
        if (_flash == null)
        {
            for (int i = 0; i < _sprites.Length; i++)
            {
                SpriteRenderer sprite = _sprites[i];
                if (sprite != null) { _flashBaseColors[i] = sprite.color; }
            }
        }
        else
        {
            _flash.Kill();
        }

        ApplyFlash(tint, strength);
        _flash = DOVirtual.Float(strength, 0f, duration, progress => ApplyFlash(tint, progress))
            .SetEase(Ease.OutQuad)
            .OnComplete(() => _flash = null);
    }

    internal void CancelWindup()
    {
        if (_isWindingUp)
        {
            ResetPose();
        }
    }

    internal void Clear()
    {
        ResetPose();
        if (_flash != null)
        {
            _flash.Kill();
            _flash = null;
            ApplyFlash(default, 0f);
        }
    }

    private static Transform CreatePoseRoot(Transform visualRoot)
    {
        var container = new GameObject("MonsterMotion");
        container.layer = visualRoot.gameObject.layer;
        Transform poseRoot = container.transform;
        poseRoot.SetParent(visualRoot.parent, false);
        poseRoot.SetSiblingIndex(visualRoot.GetSiblingIndex());
        visualRoot.SetParent(poseRoot, false);
        return poseRoot;
    }

    private void PlayPose(Vector3 scale, float offsetX, float duration, float returnDuration = 0f)
    {
        if (_poseRoot == null) { return; }

        _pose?.Kill();
        _isWindingUp = false;
        _pose = DOTween.Sequence();
        _pose.Append(_poseRoot.DOScale(scale, duration).SetEase(Ease.OutQuad));
        _pose.Join(_poseRoot.DOLocalMoveX(offsetX, duration).SetEase(Ease.OutQuad));
        if (returnDuration > 0f)
        {
            _pose.Append(_poseRoot.DOScale(Vector3.one, returnDuration).SetEase(Ease.OutCubic));
            _pose.Join(_poseRoot.DOLocalMoveX(0f, returnDuration).SetEase(Ease.OutCubic));
        }

        _pose.OnComplete(() => _pose = null);
    }

    private void ResetPose()
    {
        _pose?.Kill();
        _pose = null;
        _isWindingUp = false;
        if (_poseRoot != null)
        {
            _poseRoot.localScale = Vector3.one;
            _poseRoot.localPosition = Vector3.zero;
        }
    }

    private void ApplyFlash(Color tint, float strength)
    {
        for (int i = 0; i < _sprites.Length; i++)
        {
            SpriteRenderer sprite = _sprites[i];
            if (sprite == null) { continue; }
            Color color = Color.Lerp(_flashBaseColors[i], tint, strength);
            // 기존 사망 애니메이션의 투명도 유지
            color.a = sprite.color.a;
            sprite.color = color;
        }
    }
}
