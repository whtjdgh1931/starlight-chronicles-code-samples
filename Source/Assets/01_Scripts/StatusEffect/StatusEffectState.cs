/// <summary>
/// 활성 상태이상 표시 정보
/// </summary>
public readonly struct StatusEffectState
{
    public StatusEffectState(
        StatusEffectId effectId,
        float value,
        float startTime,
        float duration)
    {
        EffectId = effectId;
        Value = value;
        StartTime = startTime;
        Duration = duration;
    }

    public StatusEffectId EffectId { get; }
    public float Value { get; }
    public float StartTime { get; }
    public float Duration { get; }
    public float EndTime => StartTime + Duration;
}