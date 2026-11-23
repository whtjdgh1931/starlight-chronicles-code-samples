using System;
using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;

/// <summary>
/// 보스 공격 사각형 예고 표시
/// </summary>
public sealed class BossAttackTelegraph : MonoBehaviour
{
    /// <summary>범위 예고의 이미지와 베기 방향</summary>
    public enum WarningStyle
    {
        Area,
        SlashLeft,
        SlashRight
    }

    private const float WarningBrightenStart = 0.72f;
    private const float WarningBrightenDuration = 1f - WarningBrightenStart;

    [SerializeField] private SpriteRenderer _spriteRenderer;
    [SerializeField] private Sprite _slashWarningSprite;
    [SerializeField] private Material _pathMaterial;
    [SerializeField, Min(0.01f)] private float _pathWidth = 0.08f;
    [SerializeField] private Color _pathColor = new Color(1f, 0f, 0f, 0.6f);

    private readonly List<LineRenderer> _pathRenderers = new();
    private BossAttackBurst _burst;
    private Tween _warningTween;
    private Color _areaColor;
    private Color _currentPathColor;
    private bool _showingPaths;
    private Sprite _defaultWarningSprite;

    // 서버 예고 표시 결과의 네트워크 전달용 알림
    public event Action<Vector2, Vector2, float, WarningStyle> Shown;
    public event Action<Vector2> PositionChanged;
    public event Action<Vector2, IReadOnlyList<Vector2>, float, float, bool> PathsShown;
    public event Action Hidden;
    internal event Action<BossAttackBurst.Kind, Vector2, Vector2> BurstPlayed;

    /// <summary>
    /// 필수 참조 검증과 초기 상태 설정
    /// </summary>
    public void Initialize()
    {
        if (_spriteRenderer == null || _pathMaterial == null)
        {
            throw new MissingReferenceException($"{name}: 예고 SpriteRenderer·경로 재질 참조 필요");
        }

        if (_burst == null)
        {
            _defaultWarningSprite = _spriteRenderer.sprite;
            _areaColor = _spriteRenderer.color;
            _burst = new BossAttackBurst(transform, _spriteRenderer, _pathMaterial);
        }

        Hide();
        ClearBursts();
    }

    /// <summary>
    /// 지정 위치와 크기로 예고 표시
    /// </summary>
    /// <param name="style">사각 범위 또는 좌우 베기 예고</param>
    public void Show(Vector2 position, Vector2 size, float duration = 0f, WarningStyle style = WarningStyle.Area)
    {
        HidePaths();
        transform.position = position;
        Sprite warning = style == WarningStyle.Area ? _defaultWarningSprite : _slashWarningSprite;
        if (warning == null) { throw new MissingReferenceException($"{name}: {style} 예고 Sprite 참조 필요"); }
        _spriteRenderer.sprite = warning;
        _spriteRenderer.flipX = style == WarningStyle.SlashLeft;

        Vector3 worldScale = _spriteRenderer.transform.lossyScale;
        if (_spriteRenderer.drawMode == SpriteDrawMode.Simple)
        {
            // 이미지 비율과 부모 배율을 반영한 실제 예고 범위
            Vector3 localScale = _spriteRenderer.transform.localScale;
            Vector2 spriteSize = _spriteRenderer.sprite.bounds.size;
            _spriteRenderer.transform.localScale = new Vector3(
                localScale.x * size.x / (spriteSize.x * Mathf.Abs(worldScale.x)),
                localScale.y * size.y / (spriteSize.y * Mathf.Abs(worldScale.y)), localScale.z);
        }
        else
        {
            _spriteRenderer.size = new Vector2(size.x / Mathf.Abs(worldScale.x), size.y / Mathf.Abs(worldScale.y));
        }
        _spriteRenderer.enabled = true;
        StartWarning(duration);
        Shown?.Invoke(position, size, duration, style);
    }

    /// <summary>
    /// 발사점에서 시작하는 다중 투사체 경로 표시
    /// </summary>
    public void ShowPaths(Vector2 origin, IReadOnlyList<Vector2> directions, float length, float duration = 0f, bool isFinal = false)
    {
        _spriteRenderer.enabled = false;
        if (!_showingPaths)
        {
            _showingPaths = true;
            StartWarning(duration);
        }

        EnsurePathRendererCount(directions.Count);

        for (int index = 0; index < directions.Count; index++)
        {
            LineRenderer pathRenderer = _pathRenderers[index];
            pathRenderer.SetPosition(0, origin);
            pathRenderer.SetPosition(1, origin + directions[index] * length);
            pathRenderer.startColor = _currentPathColor;
            pathRenderer.endColor = _currentPathColor;
            pathRenderer.enabled = true;
        }

        for (int index = directions.Count; index < _pathRenderers.Count; index++)
        {
            _pathRenderers[index].enabled = false;
        }
        PathsShown?.Invoke(origin, directions, length, duration, isFinal);
    }

    /// <summary>
    /// 예고 위치 갱신
    /// </summary>
    public void SetPosition(Vector2 position)
    {
        transform.position = position;
        PositionChanged?.Invoke(position);
    }

    /// <summary>
    /// 예고 표시 제거
    /// </summary>
    public void Hide()
    {
        StopWarning();
        _spriteRenderer.enabled = false;
        HidePaths();
        Hidden?.Invoke();
    }

    /// <summary>실행 시점의 근접 베기 표시와 협동전 전달</summary>
    public void PlaySlash(Vector2 center, Vector2 size, float directionX)
    {
        PlayBurst(directionX < 0f ? BossAttackBurst.Kind.SlashLeft : BossAttackBurst.Kind.SlashRight, center, size);
    }

    /// <summary>발사점과 조준 방향에 맞춘 섬광 표시와 협동전 전달</summary>
    public void PlayMuzzleFlash(Vector2 origin, Vector2 direction)
    {
        PlayBurst(BossAttackBurst.Kind.Muzzle, origin, direction);
    }

    internal void PlayBurst(BossAttackBurst.Kind kind, Vector2 position, Vector2 sizeOrDirection)
    {
        _burst.Play(kind, position, sizeOrDirection);
        BurstPlayed?.Invoke(kind, position, sizeOrDirection);
    }

    internal void ClearBursts()
    {
        _burst?.Clear();
    }

    private void StartWarning(float duration)
    {
        StopWarning();
        _spriteRenderer.color = _areaColor;
        _currentPathColor = _pathColor;
        if (duration <= 0f)
        {
            return;
        }

        _warningTween = DOVirtual.Float(0f, 1f, duration * WarningBrightenDuration, ApplyWarningBrightness)
            .SetDelay(duration * WarningBrightenStart)
            .SetEase(Ease.InQuad)
            .OnComplete(() => _warningTween = null);
    }

    private void ApplyWarningBrightness(float progress)
    {
        var brightArea = new Color(1f, 1f, 1f, Mathf.Max(_areaColor.a, 0.9f));
        var brightPath = new Color(1f, 0.38f, 0.16f, Mathf.Max(_pathColor.a, 0.95f));
        _spriteRenderer.color = Color.Lerp(_areaColor, brightArea, progress);
        _currentPathColor = Color.Lerp(_pathColor, brightPath, progress);
        foreach (LineRenderer pathRenderer in _pathRenderers)
        {
            pathRenderer.startColor = _currentPathColor;
            pathRenderer.endColor = _currentPathColor;
        }
    }

    private void StopWarning()
    {
        _warningTween?.Kill();
        _warningTween = null;
    }

    private void OnDisable()
    {
        StopWarning();
        _spriteRenderer.enabled = false;
        HidePaths();
        ClearBursts();
    }

    private void EnsurePathRendererCount(int count)
    {
        while (_pathRenderers.Count < count)
        {
            GameObject pathObject = new GameObject($"ProjectilePath{_pathRenderers.Count + 1}");
            pathObject.transform.SetParent(transform, false);

            LineRenderer pathRenderer = pathObject.AddComponent<LineRenderer>();
            pathRenderer.sharedMaterial = _pathMaterial;
            pathRenderer.positionCount = 2;
            pathRenderer.useWorldSpace = true;
            pathRenderer.startWidth = _pathWidth;
            pathRenderer.endWidth = _pathWidth;
            pathRenderer.sortingLayerID = _spriteRenderer.sortingLayerID;
            pathRenderer.sortingOrder = _spriteRenderer.sortingOrder;
            pathRenderer.enabled = false;
            _pathRenderers.Add(pathRenderer);
        }
    }

    private void HidePaths()
    {
        _showingPaths = false;
        foreach (LineRenderer pathRenderer in _pathRenderers)
        {
            pathRenderer.enabled = false;
        }
    }
}
