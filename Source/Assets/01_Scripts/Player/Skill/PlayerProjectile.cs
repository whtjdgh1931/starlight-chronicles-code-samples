using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 몬스터 피해와 관통 횟수를 관리하는 플레이어 투사체
/// </summary>
[RequireComponent(typeof(ProjectileMotion))]
public sealed class PlayerProjectile : MonoBehaviour
{
    [SerializeField] private ProjectileMotion _motion;

    private readonly HashSet<MonsterBase> _hitMonsters = new();

    private Player _attacker;
    private float _damage;
    private int _maxTargets;
    private bool _isLaunched;

    /// <summary>
    /// 이동 방향과 수명 설정이 끝난 실제 발사 알림
    /// </summary>
    public event Action Launched;

    /// <summary>
    /// 투사체 피해와 이동 초기화
    /// </summary>
    public void Initialize(float damage, Vector2 direction, float speed, float lifetime, int maxTargets, Player attacker)
    {
        ValidateReferences();

        if (attacker == null)
        {
            throw new ArgumentNullException(nameof(attacker));
        }

        if (maxTargets <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maxTargets), maxTargets, "최대 타깃 수는 1 이상 필요");
        }

        _attacker = attacker;
        _damage = damage;
        _maxTargets = maxTargets;
        _hitMonsters.Clear();
        _isLaunched = true;

        _motion.Launch(direction, speed, lifetime);
        Launched?.Invoke();
    }

    /// <summary>
    /// 필수 참조 검증
    /// </summary>
    private void ValidateReferences()
    {
        if (_motion == null)
        {
            throw new MissingReferenceException($"{name}: ProjectileMotion 참조 필요");
        }
    }

    /// <summary>
    /// 몬스터별 피해 일회 적용
    /// </summary>
    private void TryApplyDamage(MonsterBase monster, Vector3 hitPosition)
    {
        if (!_hitMonsters.Add(monster))
        {
            return;
        }

        monster.TakeDamage(_damage, transform.position, _attacker, hitPosition);

        if (_hitMonsters.Count >= _maxTargets)
        {
            Release();
        }
    }

    /// <summary>
    /// 몬스터 또는 지형 충돌 처리
    /// </summary>
    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (!_isLaunched)
        {
            return;
        }

        if (collision.gameObject.layer == Constants.LAYER_Wall)
        {
            Release();
            return;
        }

        if (collision.TryGetComponent(out MonsterBase monster))
        {
            TryApplyDamage(monster, collision.ClosestPoint(transform.position));
        }
    }

    /// <summary>
    /// 투사체 풀 반환
    /// </summary>
    private void Release()
    {
        _isLaunched = false;
        _motion.Release();
    }

    private void OnDisable()
    {
        _attacker = null;
        _damage = 0f;
        _maxTargets = 0;
        _isLaunched = false;
        _hitMonsters.Clear();
    }
}
