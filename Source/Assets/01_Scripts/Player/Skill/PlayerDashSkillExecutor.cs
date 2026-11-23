using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 플레이어 돌진 공격 실행
/// </summary>
public sealed class PlayerDashSkillExecutor : PlayerSkillExecutorBehaviour
{
    [SerializeField] private PlayerAreaHitResolver _hitResolver;
    [SerializeField, Min(0f)] private float _startupDuration = 0.1f;
    [SerializeField, Min(0f)] private float _recoveryDuration = 0.1f;
    [SerializeField] private AnimationCurve _speedProfile = new(
        new Keyframe(0f, 0.85f), new Keyframe(0.2f, 1f),
        new Keyframe(0.7f, 0.35f), new Keyframe(1f, 0f));

    /// <summary>
    /// 시작 위치와 고정된 돌진 방향 전달
    /// </summary>
    public event Action<Vector3, int> DashStarted;

    /// <summary>
    /// 정상 종료·벽 충돌·취소에 따른 돌진 종료 알림
    /// </summary>
    public event Action DashEnded;

    private IPlayerForcedMovement _currentMovement;

    private readonly HashSet<MonsterBase> _hitMonsters = new();
    private readonly WaitForFixedUpdate _waitForFixedUpdate = new();


    public override PlayerSkillExecutionType ExecutionType => PlayerSkillExecutionType.Dash;

    public override float GetActionDuration(SkillData skillData) => _startupDuration + skillData.MoveDuration + _recoveryDuration;


    /// <summary>
    /// 돌진 실행기 필수 참조 초기화
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
    /// 고정 방향 돌진과 단일 범위 타격
    /// </summary>
    public override IEnumerator Execute(SkillData skillData, Player attacker, IAttackPowerProvider attackPowerProvider)
    {
        const int hitIndex = 0;

        IPlayerForcedMovement movement = attacker.ForcedMovement;
        int directionX = movement.SightDirection;
        float damage = attackPowerProvider.EffectiveAttack * skillData.DamagePercents[hitIndex];

        if (!movement.TryBeginForcedMovement())
        {
            yield break;
        }

        _currentMovement = movement;
        try
        {
            _hitMonsters.Clear();
            movement.MoveAtSpeed(directionX, 0f);
            yield return new WaitForSeconds(_startupDuration);
            if (!movement.IsForcedMoving) { yield break; }
            DashStarted?.Invoke(attacker.transform.position, directionX);
            float startedAt = Time.time;
            while (movement.IsForcedMoving && Time.time - startedAt < skillData.MoveDuration && !movement.HasWallAhead(directionX))
            {
                float progress = (Time.time - startedAt) / skillData.MoveDuration;
                float speed = skillData.MoveSpeed * Mathf.Max(0f, _speedProfile.Evaluate(progress));
                movement.MoveAtSpeed(directionX, speed);
                yield return _waitForFixedUpdate;
                if (!movement.IsForcedMoving) { yield break; }
                _hitResolver.ApplyHit(skillData, hitIndex, damage, attacker, directionX, _hitMonsters);
            }
        }
        finally
        {
            StopDash();
        }
        yield return new WaitForSeconds(_recoveryDuration);
    }

    /// <summary>
    /// 진행 중인 돌진 취소
    /// </summary>
    public override void Cancel()
    {
        StopDash();
    }

    /// <summary>
    /// 돌진 상태와 강제 이동 종료
    /// </summary>
    private void StopDash()
    {
        IPlayerForcedMovement movement = _currentMovement;
        if (movement == null)
        {
            return;
        }

        _currentMovement = null;
        movement.EndForcedMovement();
        DashEnded?.Invoke();
    }
}
