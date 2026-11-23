using UnityEngine;

/// <summary>
/// 좌우 발사점에서 플레이어 방향으로 교차 탄막 발사
/// </summary>
public sealed class BossCrossFireProjectileAttack : BossTelegraphedAttackBehaviour
{
    [SerializeField] private BossFanProjectileEmitter _projectileEmitter;
    [SerializeField] private Transform _leftFirePoint;
    [SerializeField] private Transform _rightFirePoint;
    [SerializeField, Min(1)] private int _projectileCount = 3;
    [SerializeField, Range(0f, 180f)] private float _spreadAngle = 40f;

    protected override bool TryExecuteAttack(Player target)
    {
        if (target.transform.position.x != Monster.transform.position.x) { FaceTarget(target); }

        float damage = MonsterStats.Atk * AttackData.DamageMultiplier;
        Vector2 targetPosition = target.transform.position;

        Vector2 leftOrigin = _leftFirePoint.position;
        Vector2 rightOrigin = _rightFirePoint.position;
        Vector2 leftAim = _projectileEmitter.Fire(leftOrigin, targetPosition, _projectileCount, _spreadAngle, damage, AttackData.ProjectileSpeed, AttackData.ProjectileLifetime, Monster);
        Vector2 rightAim = _projectileEmitter.Fire(rightOrigin, targetPosition, _projectileCount, _spreadAngle, damage, AttackData.ProjectileSpeed, AttackData.ProjectileLifetime, Monster);
        Telegraph.PlayMuzzleFlash(leftOrigin, leftAim);
        Telegraph.PlayMuzzleFlash(rightOrigin, rightAim);
        return true;
    }

    protected override void OnInitializeBossAttack()
    {
        if (_projectileEmitter == null)
        {
            throw new MissingReferenceException($"{name}: BossFanProjectileEmitter 참조 필요");
        }

        if (_leftFirePoint == null || _rightFirePoint == null)
        {
            throw new MissingReferenceException($"{name}: 좌우 발사 위치 참조 필요");
        }

        _projectileEmitter.Initialize();
    }
}
