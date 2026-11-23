using DG.Tweening;
using UnityEngine;

/// <summary>
/// 포탈 시각 자식의 완만한 상하 왕복, 진입 판정과 도착 위치 유지
/// </summary>
[DisallowMultipleComponent]
public sealed class PortalVisualMotion : MonoBehaviour
{
    [SerializeField] private Transform _visualRoot;
    [SerializeField, Min(0f)] private float _riseHeight = 0.14f;
    [SerializeField, Min(0.1f)] private float _halfCycleDuration = 1.4f;

    private Tween _motion;
    private Vector3 _basePosition;
    private bool _hasBasePosition;

    private void OnEnable()
    {
        if (Application.isPlaying) { Initialize(); }
    }

    /// <summary>
    /// 직계 시각 자식의 기준 위치에서 왕복 시작, 재호출 시 기존 연출 교체
    /// </summary>
    public void Initialize()
    {
        if (_visualRoot == null || _visualRoot.parent != transform)
        {
            throw new MissingReferenceException($"{name}: 포탈의 직계 시각 자식 참조 필요");
        }

        StopMotion();
        _basePosition = _visualRoot.localPosition;
        _hasBasePosition = true;
        if (!isActiveAndEnabled) { return; }

        _motion = _visualRoot.DOLocalMoveY(_basePosition.y + _riseHeight, _halfCycleDuration)
            .SetEase(Ease.InOutSine)
            .SetLoops(-1, LoopType.Yoyo);
    }

    private void OnDisable()
    {
        StopMotion();
    }

    private void OnDestroy()
    {
        StopMotion();
    }

    /// <summary>
    /// 중복 실행·비활성화·파괴 시 Tween 해제와 시각 위치 복원
    /// </summary>
    private void StopMotion()
    {
        _motion?.Kill();
        _motion = null;
        if (_hasBasePosition && _visualRoot != null) { _visualRoot.localPosition = _basePosition; }
        _hasBasePosition = false;
    }
}
