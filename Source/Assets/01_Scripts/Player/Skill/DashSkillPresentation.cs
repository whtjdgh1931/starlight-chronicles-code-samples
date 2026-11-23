using UnityEngine;

/// <summary>
/// 돌진 시작 섬광·이동 잔상·종료 정리 담당
/// </summary>
public sealed class DashSkillPresentation : MonoBehaviour, IObjectPoolReceiver
{
    [SerializeField] private PlayerDashSkillExecutor _executor;
    [SerializeField] private PlayerVisual _playerVisual;
    [SerializeField] private TrailRenderer[] _trails;
    [SerializeField] private ParticleSystem _particles;
    [SerializeField] private SwordWaveBurst _startPrefab;

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
        _executor.DashStarted += PlayStart;
        _executor.DashEnded += PlayEnd;
    }

    /// <summary>
    /// 서버 또는 로컬 실행기가 결정한 위치의 돌진 시작 연출
    /// </summary>
    public void PlayStart(Vector3 position, int directionX)
    {
        foreach (TrailRenderer trail in _trails)
        {
            trail.Clear();
            trail.emitting = true;
        }

        _particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        _particles.Play(true);
        GameObject effect = _objectPool.Pop(_startPrefab.gameObject);
        Quaternion rotation = Quaternion.Euler(0f, 0f, directionX < 0 ? 180f : 0f);
        effect.GetComponent<SwordWaveBurst>().PlayCombat(position, rotation, _isLocalSound, false);
    }

    /// <summary>
    /// 돌진 잔상 방출 종료와 자연 소멸
    /// </summary>
    public void PlayEnd()
    {
        _playerVisual.EndDashAction();
        foreach (TrailRenderer trail in _trails)
        {
            trail.emitting = false;
        }
        _particles.Stop(true, ParticleSystemStopBehavior.StopEmitting);
    }

    private void OnDisable()
    {
        _executor.DashStarted -= PlayStart;
        _executor.DashEnded -= PlayEnd;
        Clear();
    }

    /// <summary>
    /// 캐릭터 제거 시 남은 잔상과 입자 즉시 정리
    /// </summary>
    public void Clear()
    {
        foreach (TrailRenderer trail in _trails)
        {
            trail.emitting = false;
            trail.Clear();
        }
        _particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
    }
}
