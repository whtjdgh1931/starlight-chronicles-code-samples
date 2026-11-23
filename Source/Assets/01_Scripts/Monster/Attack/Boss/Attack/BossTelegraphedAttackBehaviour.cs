using System;
using UnityEngine;

/// <summary>
/// 보스 공격 예고 시간과 타깃 추적 관리
/// </summary>
public abstract class BossTelegraphedAttackBehaviour : MonsterAttackBehaviour
{
    private enum TelegraphTargetMode
    {
        FollowTarget,
        LockedPosition
    }

    [SerializeField] private BossAttackTelegraph _telegraph;

    private TelegraphTargetMode _targetMode;

    protected virtual bool UsesDefaultTelegraph => true;
    protected BossAttackTelegraph Telegraph => _telegraph;
    protected bool TracksTelegraphTarget => _targetMode == TelegraphTargetMode.FollowTarget;


    protected virtual void UpdateTelegraph(Player target)
    {
        _telegraph.SetPosition(target.transform.position);
    }

    private void HideTelegraph()
    {
        _telegraph.Hide();
    }

    protected sealed override void OnInitialize()
    {
        if (_telegraph == null)
        {
            throw new MissingReferenceException($"{name}: BossAttackTelegraph 참조 필요");
        }

        if (AttackData.PreparationDuration < 0f)
        {
            throw new ArgumentOutOfRangeException(nameof(AttackData.PreparationDuration), $"{name}: 예고 시간은 0 이상 필요");
        }

        if (!Enum.TryParse(AttackData.TelegraphTargetMode, true, out _targetMode))
        {
            throw new InvalidOperationException($"{name}: 지원하지 않는 예고 추적 방식 {AttackData.TelegraphTargetMode}");
        }

        _telegraph.Initialize();
        OnInitializeBossAttack();
    }

    protected sealed override void OnBegin(Player target)
    {
        BeginPreparation(AttackData.PreparationDuration);

        if (UsesDefaultTelegraph)
        {
            _telegraph.Show(target.transform.position, new Vector2(AttackData.RangeX * 2f, AttackData.RangeY * 2f), AttackData.PreparationDuration);
        }

        OnBeginBossAttack(target);

        if (IsPrepared)
        {
            _telegraph.Hide();
        }
    }

    protected sealed override bool OnUpdateAttack()
    {
        if (!Monster.TryGetTrackableTarget(out Player trackedTarget) || trackedTarget != Target)
        {
            Cancel();
            return false;
        }

        if (IsPrepared)
        {
            return true;
        }

        if (TracksTelegraphTarget)
        {
            UpdateTelegraph(trackedTarget);
        }

        if (TryCompletePreparation())
        {
            _telegraph.Hide();
        }
        return true;
    }

    protected sealed override void OnComplete()
    {
        HideTelegraph();
        OnCompleteBossAttack();
    }

    protected sealed override void OnCancel(bool wasExecuted)
    {
        HideTelegraph();
        OnCancelBossAttack(wasExecuted);
    }

    protected virtual void OnInitializeBossAttack()
    {
    }

    protected virtual void OnBeginBossAttack(Player target)
    {
    }

    protected virtual void OnCompleteBossAttack()
    {
    }

    protected virtual void OnCancelBossAttack(bool wasExecuted)
    {
    }
}
