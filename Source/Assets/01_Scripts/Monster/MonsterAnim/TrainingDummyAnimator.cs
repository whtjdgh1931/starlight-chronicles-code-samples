using System.Collections;
using UnityEngine;

/// <summary>
/// 기존 캐릭터 연출을 재사용하는 허수아비 피격과 복귀 연결
/// </summary>
public sealed class TrainingDummyAnimator : MonsterAnimationController
{
    [SerializeField] private HeroEditorMonsterAnimator _presentation;
    [SerializeField, Min(0.01f)] private float _hitDuration = 0.35f;
    private Coroutine _hitRoutine;

    protected override void ValidateReferences()
    {
        if (_presentation == null)
        {
            throw new MissingReferenceException($"{name}: 허수아비 캐릭터 연출 참조 필요");
        }
    }

    protected override void OnInitialized()
    {
        CancelHit();
        _presentation.Initialize(NotifyAnimationFinished, NotifyAttackExecution);
    }

    public override void PlayIdle()
    {
        CancelHit();
        _presentation.PlayIdle();
    }

    public override void PlayMove()
    {
        PlayIdle();
    }

    public override void PlayAttack()
    {
        CancelHit();
        _presentation.PlayAttack();
    }

    public override void PlayHit()
    {
        CancelHit();
        _presentation.PlayHit();
        _hitRoutine = StartCoroutine(FinishHit());
    }

    public override void PlayDie()
    {
        PlayIdle();
    }

    public override void SetFacingDirection(float directionX)
    {
        _presentation.SetFacingDirection(directionX);
    }

    private IEnumerator FinishHit()
    {
        yield return new WaitForSeconds(_hitDuration);
        _hitRoutine = null;
        NotifyAnimationFinished();
    }

    private void CancelHit()
    {
        if (_hitRoutine == null) { return; }
        StopCoroutine(_hitRoutine);
        _hitRoutine = null;
    }

    private void OnDisable()
    {
        CancelHit();
    }
}
