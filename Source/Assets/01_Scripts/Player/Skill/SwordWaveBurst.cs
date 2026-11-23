using UnityEngine;

/// <summary>
/// 풀링되는 전투·착지·부활 이미지와 입자·단발 효과음 재생
/// 투사체 반환 이후에도 지정 수명까지 연출 유지
/// </summary>
public sealed class SwordWaveBurst : MonoBehaviour, ICombatSoundReceiver
{
    [SerializeField] private ParticleSystem _particles;
    [SerializeField] private AudioSource _audioSource;
    [SerializeField] private AudioClip _clip;
    [SerializeField] private PoolableObject _poolableObject;
    [SerializeField, Min(0.01f)] private float _lifetime = 0.65f;
    [SerializeField] private SpriteEffectPulse[] _spriteEffects = System.Array.Empty<SpriteEffectPulse>();

    private CombatSoundPlayer _soundPlayer;

    /// <summary>풀에서 생성할 때 공통 전투 사운드 연결</summary>
    public void SetCombatSoundPlayer(CombatSoundPlayer soundPlayer)
    {
        _soundPlayer = soundPlayer;
    }

    /// <summary>
    /// 입자·이미지 재생과 제한된 전투 효과음 요청
    /// 부활·착지 등 별도 효과는 기존 Play 사용
    /// </summary>
    public void PlayCombat(Vector3 position, Quaternion rotation, bool isLocalPlayer, bool isImpact)
    {
        PlayBurst(position, rotation, false, isImpact);
        _soundPlayer.Play(_clip, _audioSource.volume, _audioSource.pitch, isLocalPlayer, isImpact);
    }

    /// <summary>
    /// 풀에서 꺼낸 효과의 위치 설정 후 일회 재생
    /// 소리가 겹치는 타격은 입자만 재생
    /// </summary>
    public void Play(Vector3 position, Quaternion rotation, bool playSound)
    {
        PlayBurst(position, rotation, playSound, false);
    }

    private void PlayBurst(Vector3 position, Quaternion rotation, bool playSound, bool isImpact)
    {
        transform.SetPositionAndRotation(position, rotation);
        _particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        _audioSource.Stop();
        _particles.Play(true);
        if (playSound)
        {
            _audioSource.PlayOneShot(_clip);
        }

        float lifetime = Mathf.Max(_lifetime, _clip.length);
        foreach (SpriteEffectPulse effect in _spriteEffects)
        {
            effect.Play(isImpact);
            lifetime = Mathf.Max(lifetime, effect.Duration);
        }
        _poolableObject.ScheduleRelease(lifetime);
    }

    private void OnDisable()
    {
        _particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        _audioSource.Stop();
    }
}
