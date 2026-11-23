using UnityEngine;

/// <summary>
/// 전방 범위에 단일 근접 피해를 적용하는 검 공격
/// </summary>
public sealed class SwordMonsterAttack : MonsterAttackBehaviour
{
    private const int PlayerLayerMask = 1 << Constants.LAYER_Player;

    private float _directionX;

    /// <summary>
    /// 전방 검 공격 판정 실행
    /// </summary>
    protected override bool TryExecuteAttack(Player target)
    {
        Vector2 attackSize = new Vector2(AttackData.RangeX, AttackData.RangeY * 2f);
        Vector2 attackCenter = (Vector2)Monster.transform.position + Vector2.right * (_directionX * AttackData.RangeX * 0.5f);
        Collider2D playerCollider = Physics2D.OverlapBox(attackCenter, attackSize, 0f, PlayerLayerMask);

        if (playerCollider != null && playerCollider.TryGetComponent(out Player player))
        {
            float damage = MonsterStats.Atk * AttackData.DamageMultiplier;
            KnockbackData knockbackData = CreateKnockbackData();
            player.TakeDamage(damage, Monster.transform.position, knockbackData);
        }

        return true;
    }

    /// <summary>
    /// 공격 시작 방향 고정
    /// </summary>
    protected override void OnBegin(Player target)
    {
        _directionX = FaceTarget(target);
    }
}
