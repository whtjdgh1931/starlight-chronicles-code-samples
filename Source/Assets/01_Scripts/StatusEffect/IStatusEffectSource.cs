using System;
using System.Collections.Generic;

/// <summary>
/// 상태이상 UI 데이터 제공 계약
/// </summary>
public interface IStatusEffectSource
{
    event Action<StatusEffectState> EffectApplied;
    event Action<StatusEffectId> EffectRemoved;

    IReadOnlyCollection<StatusEffectState> ActiveEffects { get; }
}