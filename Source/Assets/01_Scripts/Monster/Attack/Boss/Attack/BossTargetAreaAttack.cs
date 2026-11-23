using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 공격 시작 시 저장한 범위 안의 모든 플레이어에게 피해 적용
/// </summary>
public sealed class BossTargetAreaAttack : BossTelegraphedAttackBehaviour
{
    private const int PlayerLayerMask = 1 << Constants.LAYER_Player;

    private readonly List<Collider2D> _hitColliders = new();
    private readonly HashSet<Player> _hitPlayers = new();

    private Vector2 _attackPosition;

    /// <summary>
    /// 저장된 사각 범위의 플레이어별 단일 피해 적용
    /// </summary>
    protected override bool TryExecuteAttack(Player target)
    {
        _hitColliders.Clear();
        _hitPlayers.Clear();

        var contactFilter = new ContactFilter2D();
        contactFilter.SetLayerMask(PlayerLayerMask);
        contactFilter.useTriggers = false;

        Vector2 attackSize = new Vector2(AttackData.RangeX * 2f, AttackData.RangeY * 2f);
        Physics2D.OverlapBox(_attackPosition, attackSize, 0f, contactFilter, _hitColliders);

        float damage = MonsterStats.Atk * AttackData.DamageMultiplier;
        KnockbackData knockbackData = CreateKnockbackData();

        foreach (Collider2D hitCollider in _hitColliders)
        {
            if (!hitCollider.TryGetComponent(out Player player) || !_hitPlayers.Add(player))
            {
                continue;
            }

            player.TakeDamage(damage, _attackPosition, knockbackData);
        }

        return true;
    }

    /// <summary>
    /// 공격 기준 위치와 보스 방향 저장
    /// </summary>
    protected override void OnBeginBossAttack(Player target)
    {
        _attackPosition = target.transform.position;
        FaceTarget(target);
    }
}
