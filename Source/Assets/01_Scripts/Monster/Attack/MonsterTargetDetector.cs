using System;
using UnityEngine;

/// <summary>
/// 신규 타깃 탐색 및 현재 타깃 추적 범위 판정
/// </summary>
[RequireComponent(typeof(Collider2D))]
public sealed class MonsterTargetDetector : MonoBehaviour
{
    private const int DetectionBufferCapacity = 16;
    private const float DetectionInterval = 0.2f;

    [SerializeField] private Collider2D _detectionCollider;
    private readonly Collider2D[] _detectionBuffer = new Collider2D[DetectionBufferCapacity];

    private MonsterData _data;
    private ContactFilter2D _playerFilter;
    private float _nextDetectionTime;
    private bool _bufferWarningLogged;

    /// <summary>
    /// 탐지 데이터 및 플레이어 레이어 필터 초기화
    /// </summary>
    public void Initialize(MonsterData data)
    {
        ValidateReferences(data);

        _data = data;

        _playerFilter = new ContactFilter2D();
        _playerFilter.SetLayerMask(1 << Constants.LAYER_Player);
        _playerFilter.useTriggers = true;

        _nextDetectionTime = 0f;
        _bufferWarningLogged = false;
    }

    /// <summary>
    /// 상태 변경 없이 현재 탐지와 추적 범위 조회
    /// 초기화 전에는 false 반환
    /// </summary>
    public bool TryGetDetectionBounds(out Bounds detectionBounds, out Bounds trackingBounds)
    {
        detectionBounds = default;
        trackingBounds = default;

        if (_data == null)
        {
            return false;
        }

        Vector3 center = _detectionCollider.bounds.center;
        detectionBounds = new Bounds(center, new Vector3(_data.DetectionRangeX * 2f, _data.DetectionRangeY * 2f, 0f));
        trackingBounds = new Bounds(center, new Vector3(_data.LoseTargetRangeX * 2f, _data.LoseTargetRangeY * 2f, 0f));
        return true;
    }

    private void ValidateReferences(MonsterData data)
    {
        if (_detectionCollider == null)
        {
            throw new MissingReferenceException(
                $"{name}: 탐지 기준 Collider2D 참조 필요");
        }

        if (data == null)
        {
            throw new ArgumentNullException(
                nameof(data),
                $"{name}: MonsterData 필요");
        }
    }

    /// <summary>
    /// 감지 범위 안에서 가장 가까운 활성 플레이어 탐색
    /// Idle과 Patrol 상태에서만 호출(선공몹)
    /// </summary>
    public bool TryAcquireTarget(out Player target)
    {
        target = null;

        if (Time.time < _nextDetectionTime)
        {
            return false;
        }

        _nextDetectionTime = Time.time + DetectionInterval;

        Bounds bounds = _detectionCollider.bounds;
        Vector2 detectionSize = new Vector2(
     _data.DetectionRangeX * 2f,
     _data.DetectionRangeY * 2f);

        int hitCount = Physics2D.OverlapBox(
            bounds.center,
            detectionSize,
            0f,
            _playerFilter,
            _detectionBuffer);

        LogBufferOverflowOnce(hitCount);

        float closestDistanceSqr = float.MaxValue;

        for (int i = 0; i < hitCount; i++)
        {
            Player candidate = GetPlayer(_detectionBuffer[i]);

            if (!IsAvailable(candidate) || IsDuplicate(candidate, i))
            {
                continue;
            }

            float distanceSqr = (candidate.transform.position - transform.position).sqrMagnitude;

            if (distanceSqr >= closestDistanceSqr)
            {
                continue;
            }

            target = candidate;
            closestDistanceSqr = distanceSqr;
        }

        return target != null;
    }

    /// <summary>
    /// 지정된 타깃의 추적 가능 여부 확인
    /// </summary>
    public bool IsTrackable(Player target)
    {
        if (!IsAvailable(target))
        {
            return false;
        }

        Vector2 offset = target.transform.position - _detectionCollider.bounds.center;

        return Mathf.Abs(offset.x) <= _data.LoseTargetRangeX &&
            Mathf.Abs(offset.y) <= _data.LoseTargetRangeY;
    }

    private Player GetPlayer(Collider2D candidateCollider)
    {
        if (candidateCollider.attachedRigidbody != null &&
            candidateCollider.attachedRigidbody.TryGetComponent(
                out Player rigidbodyPlayer))
        {
            return rigidbodyPlayer;
        }

        return candidateCollider.GetComponentInParent<Player>();
    }

    private bool IsAvailable(Player candidate)
    {
        return candidate != null && candidate.CanBeMonsterTarget;
    }

    /// <summary>
    /// 탐색한 플레이어 중복 검사
    /// </summary>
    /// <param name="candidate"></param>
    /// <param name="currentIndex"></param>
    /// <returns></returns>
    private bool IsDuplicate(Player candidate, int currentIndex)
    {
        for (int i = 0; i < currentIndex; i++)
        {
            Player previous = GetPlayer(_detectionBuffer[i]);

            if (previous == candidate)
            {
                return true;
            }
        }

        return false;
    }

    private void LogBufferOverflowOnce(int hitCount)
    {
        if (hitCount < _detectionBuffer.Length || _bufferWarningLogged)
        {
            return;
        }

        Debug.LogWarning(
            $"{name}: 타깃 탐지 결과가 버퍼 용량 " +
            $"{_detectionBuffer.Length}개를 초과했을 수 있습니다.",
            this);

        _bufferWarningLogged = true;
    }
}




