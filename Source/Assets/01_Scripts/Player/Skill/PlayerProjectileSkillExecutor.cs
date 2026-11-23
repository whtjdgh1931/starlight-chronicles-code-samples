using System.Collections;
using UnityEngine;

/// <summary>
/// 플레이어 투사체 스킬 발사
/// </summary>
public sealed class PlayerProjectileSkillExecutor : PlayerSkillExecutorBehaviour, IObjectPoolReceiver
{
    private ObjectPool _objectPool;
    private NetworkObjectPool _networkPool;

    /// <summary>
    /// 생성 또는 맵 진입 시 사용할 풀 연결
    /// </summary>
    public void SetObjectPool(ObjectPool objectPool)
    {
        _objectPool = objectPool;
    }

    /// <summary>
    /// 네트워크 생성 시 세션의 투사체 풀 연결
    /// </summary>
    public void SetNetworkPool(NetworkObjectPool networkPool)
    {
        _networkPool = networkPool;
    }

    
    [SerializeField] private PlayerProjectile _projectilePrefab;
    [SerializeField] private NetworkPlayerProjectile _networkProjectilePrefab;
    [SerializeField, Min(0f)] private float _startupDuration = 0.3f;
    [SerializeField, Min(0f)] private float _recoveryDuration = 0.12f;

    public override PlayerSkillExecutionType ExecutionType => PlayerSkillExecutionType.Projectile;

    /// <summary>
    /// 검기 준비와 발사 후 복귀를 포함한 행동 시간
    /// </summary>
    public override float GetActionDuration(SkillData skillData)
    {
        return _startupDuration + _recoveryDuration;
    }

    /// <summary>
    /// 투사체 실행기 필수 참조 초기화
    /// </summary>
    public override void Initialize()
    {
        if (_projectilePrefab == null)
        {
            throw new MissingReferenceException($"{name}: PlayerProjectile Prefab 참조 필요");
        }
    }

    /// <summary>
    /// 베기 준비 이후 현재 플레이어 위치에서 검기 발사
    /// 마무리 시간까지 스킬 실행 상태 유지
    /// </summary>
    public override IEnumerator Execute(SkillData skillData, Player attacker, IAttackPowerProvider attackPowerProvider)
    {
        yield return new WaitForSeconds(_startupDuration);
        LaunchProjectile(skillData, attacker, attackPowerProvider);
        yield return new WaitForSeconds(_recoveryDuration);
    }

    /// <summary>
    /// 발사 시점의 위치·방향으로 로컬 또는 서버 투사체 생성
    /// 기존 오브젝트 풀과 서버 피해 권한 유지
    /// </summary>
    private void LaunchProjectile(SkillData skillData, Player attacker, IAttackPowerProvider attackPowerProvider)
    {
        const int hitIndex = 0;

        int directionX = attacker.ForcedMovement.SightDirection;
        Vector2 direction = Vector2.right * directionX;
        Vector2 fireOffset = skillData.AttackOffsets[hitIndex];
        Vector2 firePosition = (Vector2)attacker.transform.position + new Vector2(fireOffset.x * directionX, fireOffset.y);
        float damage = attackPowerProvider.EffectiveAttack * skillData.DamagePercents[hitIndex];
        int maxTargets = skillData.MaxTargets[hitIndex];

        if (attacker.IsNetworkControlled)
        {
            if (_networkProjectilePrefab == null)
            {
                throw new MissingReferenceException($"{name}: 네트워크 검기 프리팹 필요");
            }
            Quaternion rotation = Quaternion.Euler(0f, 0f, directionX > 0 ? 0f : 180f);
            var networkObject = _networkPool.Pop(_networkProjectilePrefab.gameObject, firePosition, rotation);
            NetworkPlayerProjectile networkProjectile = networkObject.GetComponent<NetworkPlayerProjectile>();
            networkProjectile.PrepareAttacker(attacker);
            networkProjectile.NetworkObject.Spawn(destroyWithScene: true);
            networkProjectile.Launch(damage, direction, skillData.ProjectileSpeed,
                skillData.ProjectileLifetime, maxTargets);
            return;
        }

        GameObject projectileObject = _objectPool.Pop(_projectilePrefab.gameObject);
        projectileObject.transform.position = firePosition;

        PlayerProjectile projectile = projectileObject.GetComponent<PlayerProjectile>();
        projectile.Initialize(damage, direction, skillData.ProjectileSpeed, skillData.ProjectileLifetime, maxTargets, attacker);

    }
}
