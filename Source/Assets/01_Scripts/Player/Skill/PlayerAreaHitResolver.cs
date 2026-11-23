using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 플레이어 사각 범위 공격의 대상 탐색과 피해 적용
/// </summary>
public sealed class PlayerAreaHitResolver : MonoBehaviour
{
    [SerializeField] private LayerMask _monsterLayer;

    private readonly List<Collider2D> _hitColliders = new();
   
    private ContactFilter2D _monsterContactFilter;

    /// <summary>
    /// 공격자와 공격력 공급자 초기화
    /// </summary>
    public void Initialize()
    {
        _monsterContactFilter = new ContactFilter2D();
        _monsterContactFilter.SetLayerMask(_monsterLayer);
        _monsterContactFilter.useTriggers = false;
    }

    /// <summary>
    /// 공격 판정과 연출에서 공유하는 방향 보정 중심 계산
    /// </summary>
    public static Vector3 GetAttackCenter(SkillData skillData, int hitIndex, Player attacker, int directionX)
    {
        Vector2 offset = skillData.AttackOffsets[hitIndex];
        offset.x *= directionX;
        return attacker.transform.position + (Vector3)offset;
    }

    public int ApplyHit(SkillData skillData, int hitIndex, float damage, Player attacker, int directionX, ISet<MonsterBase> hitMonsters)
    {
        int remainingTargetCount = skillData.MaxTargets[hitIndex] - hitMonsters.Count;

        if (remainingTargetCount <= 0)
        {
            return 0;
        }

        Vector2 attackRange = skillData.AttackRanges[hitIndex];
        Vector2 attackCenter = GetAttackCenter(skillData, hitIndex, attacker, directionX);

        _hitColliders.Clear();
        Physics2D.OverlapBox(attackCenter, attackRange, 0f, _monsterContactFilter, _hitColliders);

        int appliedTargetCount = 0;

        foreach (Collider2D hitCollider in _hitColliders)
        {
            if (!hitCollider.TryGetComponent(out MonsterBase monster) || !hitMonsters.Add(monster))
            {
                continue;
            }

            Vector3 hitPosition = hitCollider.ClosestPoint(attackCenter);
            hitPosition.z = attacker.transform.position.z;
            monster.TakeDamage(damage, attacker.transform.position, attacker, hitPosition);
            appliedTargetCount++;

            if (appliedTargetCount >= remainingTargetCount)
            {
                break;
            }
        }

        return appliedTargetCount;
    }
}
