using UnityEngine;

/// <summary>
/// 플레이어 방향을 중심으로 여러 투사체를 부채꼴 발사
/// </summary>
public sealed class BossFanProjectileAttack : BossTelegraphedAttackBehaviour
{
    [SerializeField] private BossFanProjectileEmitter _projectileEmitter;
    [SerializeField] private Transform _firePoint;
    [SerializeField, Min(1)] private int _projectileCount = 5;
    [SerializeField, Range(0f, 180f)] private float _spreadAngle = 60f;
    [SerializeField, Min(0f)] private float _finalAimLeadTime = 0.12f;

    private readonly System.Collections.Generic.List<Vector2> _preparedDirections = new();

    private Vector2 _preparedFirePosition;
    private Vector2 _preparedAimDirection;
    private bool _aimLocked;

    protected override bool UsesDefaultTelegraph => false;

    protected override void UpdateTelegraph(Player target)
    {
        if (!_aimLocked)
        {
            ShowProjectilePaths(target, PreparationTimeRemaining <= _finalAimLeadTime);
        }
    }

    /// <summary>
    /// 확정 예고의 발사점과 탄별 방향으로 투사체 발사
    /// </summary>
    protected override bool TryExecuteAttack(Player target)
    {
        float damage = MonsterStats.Atk * AttackData.DamageMultiplier;
        _projectileEmitter.FireAlongPaths(_preparedFirePosition, _preparedDirections, damage, AttackData.ProjectileSpeed, AttackData.ProjectileLifetime, Monster);
        Telegraph.PlayMuzzleFlash(_preparedFirePosition, _preparedAimDirection);
        return true;
    }

    /// <summary>
    /// 현재 보스 시선을 반영한 발사점 계산
    /// </summary>
    private Vector2 GetFirePosition()
    {
        Vector2 monsterPosition = Monster.transform.position;
        Vector2 firePosition = _firePoint.position;
        float directionX = Monster.FacingDirection;
        float offsetX = Mathf.Abs(firePosition.x - monsterPosition.x);

        firePosition.x = monsterPosition.x + offsetX * directionX;
        return firePosition;
    }

    /// <summary>
    /// 예고 추적 중 발사점·조준 위치 갱신과 경로 표시
    /// 마지막 표시 위치를 실제 발사에 사용
    /// </summary>
    private void ShowProjectilePaths(Player target, bool isFinal)
    {
        if (target.transform.position.x != Monster.transform.position.x) { FaceTarget(target); }

        _preparedFirePosition = GetFirePosition();
        Vector2 targetPosition = target.transform.position;
        _preparedAimDirection = _projectileEmitter.CalculateDirections(_preparedFirePosition, targetPosition, _projectileCount, _spreadAngle, Monster.FacingDirection, _preparedDirections);
        _aimLocked = isFinal;
        Telegraph.ShowPaths(_preparedFirePosition, _preparedDirections, AttackData.ProjectileSpeed * AttackData.ProjectileLifetime, AttackData.PreparationDuration, isFinal);
    }

    protected override void OnInitializeBossAttack()
    {
        if (_projectileEmitter == null)
        {
            throw new MissingReferenceException($"{name}: BossFanProjectileEmitter 참조 필요");
        }

        if (_firePoint == null)
        {
            throw new MissingReferenceException($"{name}: 발사 위치 Transform 참조 필요");
        }

        _projectileEmitter.Initialize();
    }

    protected override void OnBeginBossAttack(Player target)
    {
        ShowProjectilePaths(target, !TracksTelegraphTarget || PreparationTimeRemaining <= _finalAimLeadTime);
    }
}
