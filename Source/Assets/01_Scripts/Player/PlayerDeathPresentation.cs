using System;
using DG.Tweening;
using UnityEngine;

/// <summary>
/// 서버 공유 시각 기준 비석 낙하·착지·부활 연출
/// 캐릭터 표시와 서버 생명 상태 변경은 호출자 담당
/// </summary>
public sealed class PlayerDeathPresentation : MonoBehaviour, IObjectPoolReceiver
{
    [SerializeField] private GameObject _tombstonePrefab;
    [SerializeField] private SwordWaveBurst _landingEffectPrefab;
    [SerializeField] private SwordWaveBurst _reviveEffectPrefab;
    [SerializeField, Min(0f)] private float _fallHeight = 3f;
    [SerializeField] private Vector3 _tombstoneOffset;
    private ObjectPool _pool;
    private GameObject _tombstone;
    private Sequence _fallSequence;

    public event Action FallStarted;

    public void SetObjectPool(ObjectPool objectPool) => _pool = objectPool;

    public void ValidateConfiguration()
    {
        if (_tombstonePrefab == null || _landingEffectPrefab == null || _reviveEffectPrefab == null)
        {
            throw new MissingReferenceException($"{name}: 비석·착지·부활 프리팹 필요");
        }
    }

    /// <summary>
    /// 사망 상태 수신 시 비석 낙하 예약과 경과한 재생 위치 적용
    /// 이미 착지한 상태는 비석만 표시, 지난 착지 효과 제외
    /// </summary>
    public void BeginDeath(Vector3 position, double fallStartedAt, double readyAt, double now)
    {
        Clear();
        Vector3 end = position + _tombstoneOffset;
        Vector3 start = end + Vector3.up * _fallHeight;
        _tombstone = _pool.Pop(_tombstonePrefab);
        _tombstone.transform.position = start;
        _tombstone.SetActive(false);
        if (now >= readyAt)
        {
            _tombstone.transform.position = end;
            _tombstone.SetActive(true);
            FallStarted?.Invoke();
            return;
        }

        float delay = (float)Math.Max(0d, fallStartedAt - now);
        float elapsed = (float)Math.Max(0d, now - fallStartedAt);
        float duration = (float)(readyAt - fallStartedAt);
        _fallSequence = DOTween.Sequence().Pause().SetUpdate(true).SetEase(Ease.Linear);
        _fallSequence.AppendInterval(delay);
        _fallSequence.AppendCallback(() =>
        {
            _tombstone.SetActive(true);
            FallStarted?.Invoke();
        });
        _fallSequence.Append(_tombstone.transform.DOMove(end, duration).SetEase(Ease.InQuad));
        _fallSequence.OnComplete(() => PlayEffect(_landingEffectPrefab, end));
        _fallSequence.OnKill(() => _fallSequence = null);
        _fallSequence.Goto(elapsed, andPlay: true);
    }

    /// <summary>
    /// 실제 부활 승인 위치의 금빛 이미지·상승 입자 재생
    /// </summary>
    public void PlayRevive(Vector3 position)
    {
        PlayEffect(_reviveEffectPrefab, position + _tombstoneOffset);
    }

    private void PlayEffect(SwordWaveBurst prefab, Vector3 position)
    {
        GameObject effect = _pool.Pop(prefab.gameObject);
        effect.GetComponent<SwordWaveBurst>().Play(position, Quaternion.identity, true);
    }

    /// <summary>
    /// 부활·네트워크 종료·맵 이동 시 Tween 취소와 비석 반환
    /// 취소 시 착지 완료 효과 재생 제외
    /// </summary>
    public void Clear()
    {
        _fallSequence?.Kill(complete: false);
        if (_tombstone != null)
        {
            _pool.Push(_tombstone);
            _tombstone = null;
        }
    }

    private void OnDisable() => Clear();
}
