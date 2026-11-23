using UnityEngine;

/// <summary>
/// 검기의 발사·비행 연출 연결
/// 피해 계산과 독립적인 프리팹 표현 담당
/// </summary>
public sealed class SwordWavePresentation : MonoBehaviour, IObjectPoolReceiver
{
    [SerializeField] private PlayerProjectile _projectile;
    [SerializeField] private TrailRenderer _trail;
    [SerializeField] private ParticleSystem _flightParticles;
    [SerializeField] private SwordWaveBurst _launchPrefab;

    private ObjectPool _objectPool;
    private bool _isLocalSound = true;

    /// <summary>온라인 생성 시 실제 공격자의 화면 기준 소리 구분</summary>
    public void SetSoundOwnership(bool isLocalPlayer)
    {
        _isLocalSound = isLocalPlayer;
    }

    /// <summary>
    /// 생성 또는 맵 진입 시 사용할 풀 연결
    /// </summary>
    public void SetObjectPool(ObjectPool objectPool)
    {
        _objectPool = objectPool;
    }

    private void OnEnable()
    {
        // 싱글 프리팹은 로컬 이벤트, 네트워크 프리팹은 서버 생성 알림 사용
        if (_projectile != null)
        {
            _projectile.Launched += PlayLaunch;
        }
    }

    /// <summary>발사 섬광·비행 잔상 시작</summary>
    public void PlayLaunch()
    {
        _trail.Clear();
        _trail.emitting = true;
        _flightParticles.Play(true);
        GameObject effect = _objectPool.Pop(_launchPrefab.gameObject);
        effect.GetComponent<SwordWaveBurst>().PlayCombat(
            transform.position, transform.rotation, _isLocalSound, isImpact: false);
    }

    private void OnDisable()
    {
        if (_projectile != null)
        {
            _projectile.Launched -= PlayLaunch;
        }
        _trail.emitting = false;
        _trail.Clear();
        _flightParticles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        _isLocalSound = true;
    }
}
