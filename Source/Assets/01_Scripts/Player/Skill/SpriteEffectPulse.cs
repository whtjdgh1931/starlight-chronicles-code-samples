using System.Collections;
using UnityEngine;

/// <summary>
/// 단발 이미지의 크기 변화와 투명도 소멸
/// 풀 반환 시 진행 연출과 표시 상태 초기화
/// </summary>
public sealed class SpriteEffectPulse : MonoBehaviour
{
    private const float ImpactExpansionBoost = 1.35f;
    private const float ImpactFadeStart = 0.12f;

    [SerializeField] private SpriteRenderer _renderer;
    [SerializeField] private Vector2 _startScale = Vector2.one;
    [SerializeField] private Vector2 _endScale = Vector2.one;
    [SerializeField] private Color _tint = Color.white;
    [SerializeField, Min(0.01f)] private float _duration = 0.3f;
    [SerializeField] private AnimationCurve _scaleProgress = AnimationCurve.Linear(0f, 0f, 1f, 1f);
    [SerializeField] private AnimationCurve _opacity = AnimationCurve.Linear(0f, 1f, 1f, 0f);

    public float Duration => _duration;

    /// <summary>
    /// 이전 재생 상태를 초기화한 뒤 단발 이미지 연출 시작
    /// </summary>
    public void Play(bool isImpact = false)
    {
        StopAllCoroutines();
        _renderer.enabled = true;
        Apply(0f, isImpact);
        StartCoroutine(Animate(isImpact));
    }

    private IEnumerator Animate(bool isImpact)
    {
        float elapsed = 0f;
        while (elapsed < _duration)
        {
            yield return null;
            elapsed += Time.deltaTime;
            Apply(Mathf.Clamp01(elapsed / _duration), isImpact);
        }
        _renderer.enabled = false;
    }

    private void Apply(float progress, bool isImpact)
    {
        float scaleTime = isImpact ? 1f - (1f - progress) * (1f - progress) : progress;
        float scaleProgress = _scaleProgress.Evaluate(scaleTime);
        Vector2 scale = Vector2.Lerp(_startScale, _endScale, scaleProgress);
        if (isImpact) { scale *= Mathf.Lerp(1f, ImpactExpansionBoost, scaleTime); }
        transform.localScale = new Vector3(scale.x, scale.y, 1f);
        Color color = _tint;
        float fadeTime = isImpact ? Mathf.InverseLerp(ImpactFadeStart, 1f, progress) : progress;
        color.a *= _opacity.Evaluate(fadeTime);
        _renderer.color = color;
    }

    private void OnDisable()
    {
        StopAllCoroutines();
        _renderer.enabled = false;
    }
}
