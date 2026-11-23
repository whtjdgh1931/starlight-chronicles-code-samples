using System;
using System.Collections.Generic;
using UnityEngine;


/// <summary>
/// 상태이상 아이콘 Grid 관리
/// </summary>
public sealed class StatusEffectGridView : MonoBehaviour
{

    [SerializeField]
    private StatusEffectIconView _iconTemplate;

    [SerializeField]
    private Transform _iconParent;

    [SerializeField]
    private StatusEffectVisualCatalog _visualCatalog;


    private readonly Dictionary<StatusEffectId, StatusEffectIconView> _activeIcons = new();

    private readonly Stack<StatusEffectIconView> _inactiveIcons = new();

    private bool _isInitialized;

    /// <summary>
    /// 시각 설정과 아이콘 풀 초기화
    /// </summary>
    public void Initialize()
    {
        ValidateReferences();
        Clear();

        _visualCatalog.Initialize();
        _iconTemplate.gameObject.SetActive(false);

        _isInitialized = true;
        enabled = false;
    }

    private void ValidateReferences()
    {
        if (_iconTemplate == null)
        {
            throw new MissingReferenceException(
                $"{name}: 상태이상 아이콘 Template 참조 필요");
        }

        if (_iconParent == null)
        {
            throw new MissingReferenceException(
                $"{name}: 상태이상 아이콘 Parent 참조 필요");
        }

        if (_visualCatalog == null)
        {
            throw new MissingReferenceException(
                $"{name}: StatusEffectVisualCatalog 참조 필요");
        }
    }

    /// <summary>
    /// 상태이상 아이콘 표시 또는 지속시간 갱신
    /// </summary>
    public void Show(StatusEffectState effect)
    {
        EnsureInitialized();

        StatusEffectVisual visual = _visualCatalog.GetVisual(effect.EffectId);

        if (_activeIcons.TryGetValue( effect.EffectId, out StatusEffectIconView activeIcon))
        {
            activeIcon.Initialize(visual.Icon, effect.StartTime, effect.Duration);

            return;
        }

        StatusEffectIconView icon = GetIcon();

        icon.Initialize(visual.Icon, effect.StartTime, effect.Duration);

        icon.gameObject.SetActive(true);
        _activeIcons.Add(effect.EffectId, icon);
        enabled = true;
    }

    /// <summary>
    /// 지정 상태이상 아이콘 숨김
    /// </summary>
    public void Hide(StatusEffectId effectId)
    {
        if (!_activeIcons.Remove(effectId, out StatusEffectIconView icon))
        {
            return;
        }

        ReleaseIcon(icon);

        if (_activeIcons.Count == 0)
        {
            enabled = false;
        }
    }

    private void Update()
    {
        float currentTime = Time.time;

        foreach (StatusEffectIconView icon in _activeIcons.Values)
        {
            icon.Tick(currentTime);
        }
    }

    private StatusEffectIconView GetIcon()
    {
        if (_inactiveIcons.Count > 0)
        {
            return _inactiveIcons.Pop();
        }

        return Instantiate(_iconTemplate, _iconParent);
    }

    private void EnsureInitialized()
    {
        if (!_isInitialized)
        {
            throw new InvalidOperationException($"{name}: StatusEffectGridView  초기화 필요");
        }
    }

    /// <summary>
    /// 모든 상태이상 아이콘 정리
    /// </summary>
    public void Clear()
    {
        foreach (StatusEffectIconView icon in _activeIcons.Values)
        {
            ReleaseIcon(icon);
        }

        _activeIcons.Clear();
        enabled = false;
    }

    private void ReleaseIcon(StatusEffectIconView icon)
    {
        icon.gameObject.SetActive(false);
        icon.transform.SetParent(_iconParent, false);
        _inactiveIcons.Push(icon);
    }
}
