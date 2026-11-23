using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 다단 사각 범위 스킬 실행
/// </summary>
public sealed class PlayerAreaSkillExecutor : PlayerSkillExecutorBehaviour
{
    [SerializeField] private PlayerAreaHitResolver _hitResolver;
    [SerializeField, Min(0f)] private float _startupDuration = 0.12f;
    [SerializeField, Min(0f)] private float _recoveryDuration = 0.19f;

    /// <summary>
    /// 공격 중심·범위·타격 순번·방향을 전달하는 베기 연출 알림
    /// </summary>
    public event Action<Vector3, Vector2, int, int> AttackPerformed;

    private readonly HashSet<MonsterBase> _hitMonsters = new();

    public override PlayerSkillExecutionType ExecutionType => PlayerSkillExecutionType.Area;

    /// <summary>
    /// 첫 타격 준비·타격 간격·마무리를 포함한 행동 시간
    /// </summary>
    public override float GetActionDuration(SkillData skillData)
    {
        int intervalCount = Mathf.Max(0, skillData.DamagePercents.Length - 1);
        return _startupDuration + intervalCount * skillData.HitInterval + _recoveryDuration;
    }


    /// <summary>
    /// 범위 타격 처리기 초기화
    /// </summary>
    public override void Initialize()
    {
        if (_hitResolver == null)
        {
            throw new MissingReferenceException($"{name}: PlayerAreaHitResolver 참조 필요");
        }

        _hitResolver.Initialize();
    }

    /// <summary>
    /// 준비 이후 다단 타격과 마무리 시간 유지
    /// </summary>
    public override IEnumerator Execute(SkillData skillData, Player attacker, IAttackPowerProvider attackPowerProvider)
    {
        yield return new WaitForSeconds(_startupDuration);

        int hitCount = skillData.DamagePercents.Length;

        for (int hitIndex = 0; hitIndex < hitCount; hitIndex++)
        {
            _hitMonsters.Clear();

            float damage = attackPowerProvider.EffectiveAttack * skillData.DamagePercents[hitIndex];

            int directionX = attacker.GetPlayerSightDir();
            Vector3 center = PlayerAreaHitResolver.GetAttackCenter(skillData, hitIndex, attacker, directionX);
            AttackPerformed?.Invoke(center, skillData.AttackRanges[hitIndex], hitIndex, directionX);
            _hitResolver.ApplyHit(skillData, hitIndex, damage, attacker, directionX, _hitMonsters);

            if (hitIndex < hitCount - 1 && skillData.HitInterval > 0f)
            {
                yield return new WaitForSeconds(skillData.HitInterval);
            }
        }

        yield return new WaitForSeconds(_recoveryDuration);
    }
}
