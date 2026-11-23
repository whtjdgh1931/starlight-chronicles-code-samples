using UnityEngine;

/// <summary>
/// 범위 타격 시점에 맞춘 베기 메시와 휘두르기 효과음 연결
/// </summary>
public sealed class AreaSkillPresentation : MonoBehaviour, IObjectPoolReceiver
{
    [SerializeField] private PlayerAreaSkillExecutor _executor;
    [SerializeField] private AreaSlashEffect _slashPrefab;
    [SerializeField] private float[] _slashAngles = { -25f, 20f, -5f };
    [SerializeField] private float[] _hitIntensities = { 0.65f, 0.82f, 1f };

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
        _executor.AttackPerformed += PlayAttack;
    }

    /// <summary>
    /// 확정된 공격 중심과 범위의 베기 연출
    /// </summary>
    public void PlayAttack(Vector3 center, Vector2 range, int hitIndex, int directionX)
    {
        GameObject effect = _objectPool.Pop(_slashPrefab.gameObject);
        float angle = _slashAngles[hitIndex % _slashAngles.Length];
        float intensity = _hitIntensities[hitIndex % _hitIntensities.Length];
        effect.GetComponent<AreaSlashEffect>().Play(center, range, angle, directionX, _isLocalSound, intensity);
    }

    private void OnDisable()
    {
        _executor.AttackPerformed -= PlayAttack;
    }
}
