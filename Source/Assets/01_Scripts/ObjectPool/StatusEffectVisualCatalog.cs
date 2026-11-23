using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 상태이상 시각 설정 모음
/// </summary>
[CreateAssetMenu(
    fileName = "StatusEffectVisualCatalog",
    menuName = "Config/Status Effect Visual Catalog")]
public sealed class StatusEffectVisualCatalog : ScriptableObject
{
    [SerializeField] private List<StatusEffectVisual> _visuals = new();

    private readonly Dictionary<StatusEffectId, StatusEffectVisual> _visualTable = new();

    public void Initialize()
    {
        _visualTable.Clear();

        foreach (StatusEffectVisual visual in _visuals)
        {
            if (!_visualTable.TryAdd(visual.EffectId, visual))
            {
                throw new InvalidOperationException($"보스 상태이상 시각 설정 ID 중복: {visual.EffectId}");
            }
        }
    }

    public StatusEffectVisual GetVisual(StatusEffectId effectId)
    {
        if (!_visualTable.TryGetValue(effectId, out StatusEffectVisual visual))
        {
            throw new KeyNotFoundException($"상태이상 시각 설정 누락 ID: {effectId}");
        }

        return visual;
    }
}
