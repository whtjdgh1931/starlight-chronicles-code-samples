using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 고정된 전방 범위를 예고하고 플레이어별 단일 피해 적용
/// </summary>
public sealed class BossMeleeAttack : BossTelegraphedAttackBehaviour
{
    private const int PlayerLayerMask = 1 << Constants.LAYER_Player;

    private readonly List<Collider2D> _hitColliders = new();
    private readonly HashSet<Player> _hitPlayers = new();

    private float _directionX;

    protected override bool UsesDefaultTelegraph => false;

    /// <summary>
    /// 전방 범위 안의 모든 플레이어에게 한 번씩 피해 적용
    /// </summary>
    protected override bool TryExecuteAttack(Player target)
    {
        _hitColliders.Clear();
        _hitPlayers.Clear();

        var contactFilter = new ContactFilter2D();
        contactFilter.SetLayerMask(PlayerLayerMask);
        contactFilter.useTriggers = false;

        Vector2 center = GetAttackCenter();
        Vector2 size = GetAttackSize();
        Physics2D.OverlapBox(center, size, 0f, contactFilter, _hitColliders);
        Telegraph.PlaySlash(center, size, _directionX);

        float damage = MonsterStats.Atk * AttackData.DamageMultiplier;
        KnockbackData knockbackData = CreateKnockbackData();

        foreach (Collider2D hitCollider in _hitColliders)
        {
            if (!hitCollider.TryGetComponent(out Player player) || !_hitPlayers.Add(player))
            {
                continue;
            }

            player.TakeDamage(damage, Monster.transform.position, knockbackData);
        }

        return true;
    }

    private Vector2 GetAttackCenter() => (Vector2)Monster.transform.position + Vector2.right * (_directionX * AttackData.RangeX * 0.5f);

    private Vector2 GetAttackSize() => new Vector2(AttackData.RangeX, AttackData.RangeY * 2f);

    protected override void OnBeginBossAttack(Player target)
    {
        _directionX = FaceTarget(target);
        var style = _directionX < 0f ? BossAttackTelegraph.WarningStyle.SlashLeft : BossAttackTelegraph.WarningStyle.SlashRight;
        Telegraph.Show(GetAttackCenter(), GetAttackSize(), AttackData.PreparationDuration, style);
    }
}
