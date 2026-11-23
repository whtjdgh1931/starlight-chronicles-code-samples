using UnityEngine;

/// <summary>
/// 몬스터 돌진 잔상과 먼지 효과 관리
/// </summary>
public sealed class MonsterChargeEffect : MonoBehaviour
{
    private const int LaunchDustCount = 12;
    private const float BossTrailDuration = 0.18f;

    [SerializeField] private ParticleSystem _dustEffect;
    [SerializeField] private TrailRenderer _trailRenderer;
    private bool _useBossStyle;

    /// <summary>
    /// 필수 참조 검증과 이펙트 초기 상태 설정
    /// </summary>
    public void Initialize(bool useBossStyle = false)
    {
        if (_trailRenderer == null)
        {
            throw new MissingReferenceException($"{name}: 돌진 TrailRenderer 참조 필요");
        }

        _trailRenderer.emitting = false;
        _trailRenderer.Clear();
        if (_dustEffect != null)
        {
            _dustEffect.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }

        if (useBossStyle && !_useBossStyle)
        {
            ConfigureBossStyle();
        }

        _useBossStyle = useBossStyle;
    }

    /// <summary>
    /// 돌진 방향에 맞춘 이펙트 재생
    /// </summary>
    public void Play(float directionX, bool emitLaunchDust = true)
    {
        float facingDirection = directionX <= 0f ? -1f : 1f;
        transform.localScale = new Vector3(facingDirection, 1f, 1f);
        _trailRenderer.Clear();
        _trailRenderer.emitting = true;
        if (_dustEffect != null)
        {
            if (_useBossStyle)
            {
                _dustEffect.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            }

            _dustEffect.Play();
            if (_useBossStyle && emitLaunchDust)
            {
                EmitLaunchDust(facingDirection);
            }
        }
    }

    private void ConfigureBossStyle()
    {
        _trailRenderer.time = BossTrailDuration;
        var gradient = new Gradient();
        gradient.SetKeys(
            new[]
            {
                new GradientColorKey(new Color(0.77f, 1f, 1f), 0f),
                new GradientColorKey(new Color(0.19f, 0.66f, 0.90f), 0.55f),
                new GradientColorKey(new Color(0.10f, 0.45f, 0.65f), 1f)
            },
            new[] { new GradientAlphaKey(0.72f, 0f), new GradientAlphaKey(0.16f, 0.48f), new GradientAlphaKey(0f, 1f) });
        _trailRenderer.colorGradient = gradient;

        if (_dustEffect != null)
        {
            ParticleSystem.MainModule main = _dustEffect.main;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
        }
    }

    private void EmitLaunchDust(float directionX)
    {
        Vector3 origin = _dustEffect.transform.position;
        for (int i = 0; i < LaunchDustCount; i++)
        {
            float spread = i / (float)(LaunchDustCount - 1);
            var dust = new ParticleSystem.EmitParams
            {
                position = origin + Vector3.right * (Mathf.Lerp(-0.35f, 0.35f, spread) * directionX),
                velocity = new Vector3(-directionX * Mathf.Lerp(2.5f, 6f, spread), Mathf.Lerp(1.9f, 0.4f, spread), 0f),
                startLifetime = Mathf.Lerp(0.32f, 0.20f, spread),
                startSize = Mathf.Lerp(0.55f, 0.28f, spread)
            };
            _dustEffect.Emit(dust, 1);
        }
    }

    /// <summary>
    /// 돌진 이펙트 정지
    /// </summary>
    public void Stop()
    {
        _trailRenderer.emitting = false;
        if (_dustEffect != null)
        {
            _dustEffect.Stop(true, ParticleSystemStopBehavior.StopEmitting);
        }
    }
}
