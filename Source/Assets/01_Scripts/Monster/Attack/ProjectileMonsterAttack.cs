using UnityEngine;

/// <summary>
/// 풀링 투사체를 발사하는 몬스터 공격
/// </summary>
public sealed class ProjectileMonsterAttack : MonsterAttackBehaviour, IObjectPoolReceiver
{
    private ObjectPool _objectPool;

    /// <summary>
    /// 생성 또는 맵 진입 시 사용할 풀 연결
    /// </summary>
    public void SetObjectPool(ObjectPool objectPool)
    {
        _objectPool = objectPool;
    }


    [SerializeField] private MonsterProjectile _projectilePrefab;
    [SerializeField] private Transform _firePoint;

    internal Transform FirePoint => _firePoint;

    protected override bool TryExecuteAttack(Player target)
    {
        FaceTarget(target);

        Vector2 firePosition = _firePoint.position;
        Vector2 targetPosition = target.transform.position;
        Vector2 direction = (targetPosition - firePosition).normalized;

        GameObject projectileObject = _objectPool.Pop(_projectilePrefab.gameObject);
        projectileObject.transform.position = firePosition;

        MonsterProjectile projectile = projectileObject.GetComponent<MonsterProjectile>();
        float damage = MonsterStats.Atk * AttackData.DamageMultiplier;

        projectile.Initialize(damage, direction, AttackData.ProjectileSpeed, AttackData.ProjectileLifetime, Monster);
        return true;
    }

    protected override void OnInitialize()
    {
        if (_projectilePrefab == null)
        {
            throw new MissingReferenceException($"{name}: 투사체 Prefab 참조 필요");
        }

        if (_firePoint == null)
        {
            throw new MissingReferenceException($"{name}: 발사 위치 Transform 참조 필요");
        }
    }

}
