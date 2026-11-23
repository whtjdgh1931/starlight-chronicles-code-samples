using System;
using UnityEngine;

/// <summary>
/// 상태이상과 상태 아이콘 Grid 연결
/// </summary>
public sealed class StatusEffectPresenter : MonoBehaviour
{
    [SerializeField] private StatusEffectGridView _gridView;

    private IStatusEffectSource _source;
    private bool _isInitialized;

    /// <summary>
    /// 상태 아이콘 Grid 초기화
    /// </summary>
    public void Initialize()
    {
        if (_isInitialized)
        {
            return;
        }

        if (_gridView == null)
        {
            throw new MissingReferenceException($"{name}: StatusEffectGridView 참조 필요");
        }

        _gridView.Initialize();
        _isInitialized = true;
    }

    /// <summary>
    /// 상태이상 데이터 소스 연결
    /// </summary>
    public void Bind(IStatusEffectSource source)
    {
        EnsureInitialized();

        if (source == null)
        {
            throw new ArgumentNullException(nameof(source));
        }

        Unbind();

        _source = source;
        _source.EffectApplied += HandleEffectApplied;
        _source.EffectRemoved += HandleEffectRemoved;

        foreach (StatusEffectState effect in _source.ActiveEffects)
        {
            _gridView.Show(effect);
        }
    }

    /// <summary>
    /// 상태이상 데이터 소스 연결 해제
    /// </summary>
    public void Unbind()
    {
        if (_source != null)
        {
            _source.EffectApplied -= HandleEffectApplied;
            _source.EffectRemoved -= HandleEffectRemoved;
            _source = null;
        }

        if (_isInitialized)
        {
            _gridView.Clear();
        }
    }

    private void EnsureInitialized()
    {
        if (!_isInitialized)
        {
            throw new InvalidOperationException($"{name}: StatusEffectPresenter 초기화 필요");
        }
    }

    private void HandleEffectApplied(StatusEffectState effect)
    {
        _gridView.Show(effect);
    }

    private void HandleEffectRemoved(StatusEffectId effectId)
    {
        _gridView.Hide(effectId);
    }

    private void OnDestroy()
    {
        Unbind();
    }
}
