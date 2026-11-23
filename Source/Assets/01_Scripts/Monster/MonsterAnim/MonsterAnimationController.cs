using System;
using UnityEngine;

/// <summary>
/// 몬스터 애니메이션 제어 계약
/// </summary>
public abstract class MonsterAnimationController : MonoBehaviour
{
    private Action _onAnimationFinished;
    private Action _onAttackExecution;
    private bool _referencesValidated;


    /// <summary>
    /// 애니메이션 콜백 초기화
    /// </summary>
    public void Initialize(Action onAnimationFinished, Action onAttackExecution)
    {
        if (!_referencesValidated)
        {
            ValidateReferences();
            _referencesValidated = true;
        }

        _onAnimationFinished = onAnimationFinished;
        _onAttackExecution = onAttackExecution;

        OnInitialized();
    }

    /// <summary>
    /// 필수 참조 검증
    /// </summary>
    protected abstract void ValidateReferences();

    /// <summary>
    /// Idle 애니메이션 재생
    /// </summary>
    public abstract void PlayIdle();

    /// <summary>
    /// 이동 애니메이션 재생
    /// </summary>
    public abstract void PlayMove();

    /// <summary>
    /// 공격 애니메이션 재생
    /// </summary>
    public abstract void PlayAttack();

    /// <summary>
    /// 피격 애니메이션 재생
    /// </summary>
    public abstract void PlayHit();

    /// <summary>
    /// 사망 애니메이션 재생
    /// </summary>
    public abstract void PlayDie();

    /// <summary>
    /// 캐릭터 방향 설정
    /// </summary>
    public abstract void SetFacingDirection(float directionX);

    /// <summary>
    /// Animation Event 공격 판정 전달
    /// </summary>
    public void NotifyAttackExecution()
    {
        _onAttackExecution();
    }

    /// <summary>
    /// Animation Event 종료 전달
    /// </summary>
    public void NotifyAnimationFinished()
    {
        _onAnimationFinished();
    }

    /// <summary>
    /// 구현별 초기화
    /// </summary>
    protected virtual void OnInitialized()
    {
    }
}

