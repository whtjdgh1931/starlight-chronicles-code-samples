using UnityEngine;


/// <summary>
/// 직선 이동과 충돌 피해를 처리하는 몬스터 투사체
/// </summary>
[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(PoolableObject))]
public sealed class MonsterProjectile : MonoBehaviour
{
    [SerializeField] private ProjectileMotion _motion;

    private MonsterBase _owner;
    private TrailRenderer _bossTrail;
    private float _damage;
    private bool _hasAppliedDamage;

    /// <summary>
    /// 투사체 피해량, 이동, 수명과 소유자 초기화
    /// </summary>
    public void Initialize(float damage, Vector2 direction, float speed, float lifetime, MonsterBase owner)
    {
        if (_motion == null)
        {
            throw new MissingReferenceException($"{name}: ProjectileMotion 참조 필요");
        }

        _damage = damage;
        _owner = owner;
        _hasAppliedDamage = false;

        _owner.OnKilledEvent += HandleOwnerUnavailable;
        _owner.OnReturnedToPool += HandleOwnerUnavailable;

        _motion.Launch(direction, speed, lifetime);
        if (owner is BossMonster)
        {
            _bossTrail = BossProjectileTrail.Play(transform, _bossTrail);
        }
    }

    /// <summary>
    /// 플레이어 피해 일회 적용
    /// </summary>
    private void TryApplyDamage(Collider2D collision)
    {
        if (_hasAppliedDamage || !collision.TryGetComponent(out Player player))
        {
            return;
        }

        _hasAppliedDamage = true;
        player.TakeDamage(_damage, transform.position);
        _motion.Release();
    }

    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (collision.CompareTag(Constants.TAG_Player))
        {
            TryApplyDamage(collision);
            return;
        }

        if (collision.CompareTag(Constants.TAG_Ground))
        {
            _motion.Release();
        }
    }

    /// <summary>
    /// 발사자 비활성화에 따른 투사체 반환
    /// </summary>
    private void HandleOwnerUnavailable(MonsterBase owner)
    {
        _motion.Release();
    }

    private void OnDisable()
    {
        BossProjectileTrail.Stop(_bossTrail);
        if (_owner != null)
        {
            _owner.OnKilledEvent -= HandleOwnerUnavailable;
            _owner.OnReturnedToPool -= HandleOwnerUnavailable;
            _owner = null;
        }

        _damage = 0f;
        _hasAppliedDamage = false;
    }
}
