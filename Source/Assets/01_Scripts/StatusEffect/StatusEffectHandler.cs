using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 상태이상 적용, 갱신과 만료 관리
/// </summary>
[DisallowMultipleComponent]
public sealed class StatusEffectHandler : MonoBehaviour, IStatusEffectSource
{
    private readonly Dictionary<StatusEffectId, StatusEffectState> _activeEffects = new();
    private readonly List<StatusEffectId> _expiredEffectIds = new();

    public event Action<StatusEffectState> EffectApplied;
    public event Action<StatusEffectId> EffectRemoved;

    public IReadOnlyCollection<StatusEffectState> ActiveEffects => _activeEffects.Values;

    /// <summary>
    /// 피격 피해 증가 디버프 적용
    /// </summary>
    public void ApplyVulnerability(float damageMultiplier, float duration)
    {
        if (damageMultiplier < 1f)
        {
            throw new ArgumentOutOfRangeException(nameof(damageMultiplier), "피격 피해 배율은 1 이상이어야 합니다.");
        }

        ApplyEffect(StatusEffectId.Vulnerability, damageMultiplier, duration);
    }

    /// <summary>
    /// 피격 피해 증가 디버프 제거
    /// </summary>
    public void RemoveVulnerability()
    {
        RemoveEffect(StatusEffectId.Vulnerability);
    }

    /// <summary>
    /// 피격 피해량 보정
    /// </summary>
    public float ModifyIncomingDamage(float damage)
    {
        if (_activeEffects.TryGetValue(
                StatusEffectId.Vulnerability,
                out StatusEffectState vulnerability))
        {
            return damage * vulnerability.Value;
        }

        return damage;
    }

    private void Update()
    {
        float currentTime = Time.time;

        _expiredEffectIds.Clear();

        foreach (StatusEffectState effect in _activeEffects.Values)
        {
            if (currentTime >= effect.EndTime)
            {
                _expiredEffectIds.Add(effect.EffectId);
            }
        }

        foreach (StatusEffectId effectId in _expiredEffectIds)
        {
            RemoveEffect(effectId);
        }
    }

    private void ApplyEffect(StatusEffectId effectId, float value, float duration)
    {
        if (duration <= 0f)
        {
            throw new ArgumentOutOfRangeException(nameof(duration), "지속시간은 0보다 커야 합니다.");
        }

        StatusEffectState effect = new StatusEffectState(effectId, value, Time.time, duration);

        _activeEffects[effectId] = effect;
        EffectApplied?.Invoke(effect);
    }

    private void RemoveEffect(StatusEffectId effectId)
    {
        if (!_activeEffects.Remove(effectId))
        {
            return;
        }

        EffectRemoved?.Invoke(effectId);
    }

    /// <summary>
    /// 모든 상태이상 제거
    /// </summary>
    public void Clear()
    {
        _expiredEffectIds.Clear();
        _expiredEffectIds.AddRange(_activeEffects.Keys);
        _activeEffects.Clear();

        foreach (StatusEffectId effectId in _expiredEffectIds)
        {
            EffectRemoved?.Invoke(effectId);
        }

        _expiredEffectIds.Clear();
    }
}
