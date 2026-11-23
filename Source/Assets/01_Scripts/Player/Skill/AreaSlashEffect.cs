using System.Collections;
using UnityEngine;

/// <summary>
/// 풀링되는 범위 베기의 크기·방향 설정과 메시 소멸
/// 입자·효과음 수명은 독립적인 단발 재생 담당자에서 관리
/// </summary>
public sealed class AreaSlashEffect : MonoBehaviour
{
    private static readonly int TintId = Shader.PropertyToID("_Tint");

    [SerializeField] private SwordWaveBurst _burst;
    [SerializeField] private Transform _visual;
    [SerializeField] private MeshRenderer[] _renderers;
    [SerializeField] private Vector2 _meshSize;
    [SerializeField, Min(0.01f)] private float _fadeDuration = 0.2f;
    [SerializeField, Range(0.1f, 1f)] private float _rangeCoverage = 0.94f;

    private MaterialPropertyBlock _properties;
    private Coroutine _fade;

    private void OnEnable()
    {
        _properties ??= new MaterialPropertyBlock();
    }

    /// <summary>
    /// 실제 범위를 기준으로 베기 방향·크기와 단발 연출 적용
    /// </summary>
    public void Play(Vector3 position, Vector2 range, float angle, int directionX, bool isLocalPlayer, float intensity)
    {
        if (_fade != null)
        {
            StopCoroutine(_fade);
        }

        _burst.PlayCombat(position, Quaternion.identity, isLocalPlayer, false);
        // 회전 이후 이펙트가 차지하는 사각 영역 계산
        float radians = angle * Mathf.Deg2Rad;
        float cosine = Mathf.Abs(Mathf.Cos(radians));
        float sine = Mathf.Abs(Mathf.Sin(radians));
        float rotatedWidth = range.x * cosine + range.y * sine;
        float rotatedHeight = range.x * sine + range.y * cosine;

        // 실제 공격 범위 안에 들어오는 공통 배율 적용
        float fitScale = Mathf.Min(range.x / rotatedWidth, range.y / rotatedHeight);
        float visualScale = fitScale * _rangeCoverage;
        _visual.localRotation = Quaternion.Euler(0f, 0f, angle * directionX);
        _visual.localScale = new Vector3(
            range.x / _meshSize.x * directionX * visualScale,
            range.y / _meshSize.y * visualScale,
            1f);
        SetOpacity(intensity);
        _fade = StartCoroutine(Fade(intensity));
    }

    private IEnumerator Fade(float intensity)
    {
        float elapsed = 0f;
        while (elapsed < _fadeDuration)
        {
            elapsed += Time.deltaTime;
            SetOpacity(intensity * (1f - Mathf.Clamp01(elapsed / _fadeDuration)));
            yield return null;
        }

        SetOpacity(0f);
        _fade = null;
    }

    private void SetOpacity(float opacity)
    {
        _properties.SetColor(TintId, new Color(1f, 1f, 1f, opacity));
        foreach (MeshRenderer renderer in _renderers)
        {
            renderer.SetPropertyBlock(_properties);
        }
    }

    private void OnDisable()
    {
        StopAllCoroutines();
        _fade = null;
    }
}
