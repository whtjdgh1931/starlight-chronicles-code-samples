using UnityEngine;

/// <summary>
/// 본인·다른 플레이어의 전투 효과음 재생 수와 연속 타격음 제한
/// 보스 경고·결과·부활 효과음과 독립적인 재생 채널 사용
/// </summary>
public sealed class CombatSoundPlayer : MonoBehaviour
{
    [SerializeField] private AudioSource[] _localSources;
    [SerializeField] private AudioSource[] _remoteSources;
    [SerializeField, Range(0f, 1f)] private float _remoteVolume = 0.45f;
    [SerializeField, Min(0f)] private float _localImpactInterval = 0.06f;
    [SerializeField, Min(0f)] private float _remoteImpactInterval = 0.12f;

    private double _nextLocalImpactAt;
    private double _nextRemoteImpactAt;

    /// <summary>
    /// 영구 GameManager 초기화 시 필수 재생 채널 확인
    /// </summary>
    public void Initialize()
    {
        if (_localSources == null || _remoteSources == null || _localSources.Length == 0 || _remoteSources.Length == 0)
        {
            throw new MissingReferenceException($"{name}: 본인·다른 플레이어 재생 채널 필요");
        }
        StopAll();
    }

    /// <summary>
    /// 비어 있는 전용 채널에서 전투 효과음 재생
    /// 채널 포화 또는 타격음 간격 미달 시 소리만 생략
    /// </summary>
    public void Play(AudioClip clip, float volume, float pitch, bool isLocalPlayer, bool isImpact)
    {
        double now = Time.unscaledTimeAsDouble;
        double nextImpactAt = isLocalPlayer ? _nextLocalImpactAt : _nextRemoteImpactAt;
        if (isImpact && now < nextImpactAt)
        {
            return;
        }

        AudioSource[] sources = isLocalPlayer ? _localSources : _remoteSources;
        foreach (AudioSource source in sources)
        {
            if (source.isPlaying)
            {
                continue;
            }
            source.clip = clip;
            source.pitch = pitch;
            source.volume = volume * (isLocalPlayer ? 1f : _remoteVolume);
            source.Play();
            if (isImpact)
            {
                if (isLocalPlayer)
                {
                    _nextLocalImpactAt = now + _localImpactInterval;
                }
                else
                {
                    _nextRemoteImpactAt = now + _remoteImpactInterval;
                }
            }
            return;
        }
    }

    /// <summary>
    /// 맵 전환 시 이전 전투음과 연속 타격 제한 초기화
    /// </summary>
    public void StopAll()
    {
        foreach (AudioSource source in _localSources)
        {
            source.Stop();
            source.clip = null;
        }
        foreach (AudioSource source in _remoteSources)
        {
            source.Stop();
            source.clip = null;
        }
        _nextLocalImpactAt = 0d;
        _nextRemoteImpactAt = 0d;
    }
}
