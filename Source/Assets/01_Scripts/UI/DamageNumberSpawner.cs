using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 데미지 숫자 표시 요청과 중앙 오브젝트 풀 연결
/// </summary>
[DisallowMultipleComponent]
public sealed class DamageNumberSpawner : MonoBehaviour
{
    [SerializeField] private DamageNumberView _damageNumberPrefab;
    [SerializeField, Min(0.01f)] private float _lineSpacing = 0.15f;

    private readonly List<DamageNumberView> _activeViews = new();
    private ObjectPool _objectPool;
    private bool _isInitialized;

    /// <summary>
    /// 중앙 오브젝트 풀 설정과 표시 요청 구독
    /// </summary>
    public void Initialize(ObjectPool objectPool)
    {
        if (_isInitialized)
        {
            return;
        }

        _objectPool = objectPool != null ? objectPool : throw new ArgumentNullException(nameof(objectPool));

        if (_damageNumberPrefab == null)
        {
            throw new MissingReferenceException($"{name}: DamageNumber 프리팹이 할당되지 않았습니다");
        }

        _damageNumberPrefab.ValidateReferences();
        EventBus.OnDamageNumberRequested += Show;
        _isInitialized = true;
    }

    private void Show(DamageNumberRequest request)
    {
        for (int i = _activeViews.Count - 1; i >= 0; i--)
        {
            DamageNumberView activeView = _activeViews[i];
            if (activeView == null || !activeView.IsPlaying)
            {
                _activeViews.RemoveAt(i);
            }
        }

        float minimumY = request.Position.y + _lineSpacing;
        for (int i = _activeViews.Count - 1; i >= 0; i--)
        {
            DamageNumberView activeView = _activeViews[i];
            activeView.SetDisplayOrder(i);
            if (activeView.EntityId.Equals(request.EntityId))
            {
                activeView.RaiseTo(minimumY);
                minimumY = activeView.transform.position.y + _lineSpacing;
            }
        }

        DamageNumberView view = _objectPool.Pop(_damageNumberPrefab.gameObject, transform).GetComponent<DamageNumberView>();
        Color color = request.Style == DamageNumberStyle.Dealt ? Color.white : Color.red;
        view.SetDisplayOrder(_activeViews.Count);
        view.Show(request, color);
        _activeViews.Add(view);
    }

    private void OnDestroy()
    {
        if (!_isInitialized)
        {
            return;
        }

        EventBus.OnDamageNumberRequested -= Show;
        _activeViews.Clear();
        _isInitialized = false;
    }
}
