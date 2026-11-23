using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 로컬 플레이어의 상인 접근 범위와 상호작용 안내
/// 퀘스트 처리와 대화 화면 제어 제외
/// </summary>
public sealed class MerchantNpc : MonoBehaviour
{
    [SerializeField] private Collider2D _interactionArea;
    [SerializeField] private GameObject _interactionHint;
    private readonly HashSet<Collider2D> _contacts = new();
    private readonly List<Collider2D> _overlaps = new();
    private Player _player;

    public bool IsInRange => _contacts.Count > 0;
    public event Action<bool> RangeChanged;

    /// <summary>
    /// 로컬 캐릭터 연결과 이미 범위 안에 있는 콜라이더 확인
    /// </summary>
    public void Bind(Player player)
    {
        Unbind();
        _player = player;
        Physics2D.SyncTransforms();
        var filter = new ContactFilter2D();
        filter.SetLayerMask(1 << Constants.LAYER_Player);
        filter.useTriggers = true;
        _overlaps.Clear();
        _interactionArea.Overlap(filter, _overlaps);
        foreach (Collider2D other in _overlaps)
        {
            TrackContact(other);
        }
        _overlaps.Clear();
    }

    /// <summary>
    /// 캐릭터 교체와 맵 종료 시 접근 정보 정리
    /// </summary>
    public void Unbind()
    {
        bool wasInRange = IsInRange;
        _contacts.Clear();
        _interactionHint.SetActive(false);
        _player = null;
        if (wasInRange)
        {
            RangeChanged?.Invoke(false);
        }
    }

    private void TrackContact(Collider2D other)
    {
        // 연결 이전 접촉과 다른 캐릭터의 콜라이더 제외
        if (_player == null || other.GetComponentInParent<Player>() != _player)
        {
            return;
        }
        bool wasInRange = IsInRange;
        if (_contacts.Add(other) && !wasInRange)
        {
            _interactionHint.SetActive(true);
            RangeChanged?.Invoke(true);
        }
    }

    private void OnTriggerEnter2D(Collider2D other) => TrackContact(other);

    private void OnTriggerExit2D(Collider2D other)
    {
        if (_contacts.Remove(other) && !IsInRange)
        {
            _interactionHint.SetActive(false);
            RangeChanged?.Invoke(false);
        }
    }

    private void OnDisable()
    {
        // 씬 종료 또는 초기 연결 이전 비활성 경계
        if (_interactionHint != null)
        {
            Unbind();
        }
    }
}
