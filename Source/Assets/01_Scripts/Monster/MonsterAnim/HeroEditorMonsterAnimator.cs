using Assets.HeroEditor4D.Common.Scripts.CharacterScripts;
using Assets.HeroEditor4D.Common.Scripts.Enums;
using UnityEngine;

/// <summary>
/// HeroEditor4D 기반 몬스터 애니메이션 제어
/// </summary>
public sealed class HeroEditorMonsterAnimator : MonsterAnimationController
{

    private enum AttackAnimation
    {
        ShotBow,
        SlashOneHanded,
        SlashTwoHanded
    }


    private const string ShotEvent = Constants.ANIM_MonsterShot;
    private const string HitEvent = Constants.ANIM_MonsterHit;

    [SerializeField] private Character4D _character;
    [SerializeField] private AnimationManager _animationManager;
    [SerializeField] private AnimationEvents _animationEvents;
    [SerializeField] private AttackAnimation _attackAnimation;

    protected override void ValidateReferences()
    {
        if (_character == null)
        {
            throw new MissingReferenceException($"{name}: Character4D 참조 필요");
        }

        if (_animationManager == null)
        {
            throw new MissingReferenceException($"{name}: AnimationManager 참조 필요");
        }

        if (_animationEvents == null)
        {
            throw new MissingReferenceException($"{name}: AnimationEvents 참조 필요");
        }
    }

    public override void PlayIdle()
    {
        _animationManager.SetState(CharacterState.Idle);
    }

    public override void PlayMove()
    {
        _animationManager.SetState(CharacterState.Run);
    }

    public override void PlayAttack()
    {
        switch (_attackAnimation)
        {
            case AttackAnimation.ShotBow:
                _animationManager.ShotBow();
                break;

            case AttackAnimation.SlashOneHanded:
                _animationManager.Slash1H();
                break;

            case AttackAnimation.SlashTwoHanded:
                _animationManager.Slash2H();
                break;

            default:
                throw new System.ArgumentOutOfRangeException();
        }
    }

    public override void PlayHit()
    {
        _animationManager.Hit();
    }

    public override void PlayDie()
    {
        _animationManager.Die();
    }

    public override void SetFacingDirection(float directionX)
    {
        Vector2 direction = directionX > 0f ? Vector2.right : Vector2.left;

        _character.SetDirection(direction);
    }

    private bool IsAttackExecutionEvent(string eventName)
    {
        return _attackAnimation switch
        {
            AttackAnimation.ShotBow =>
                eventName == ShotEvent,
            AttackAnimation.SlashOneHanded =>
                eventName == HitEvent,
            AttackAnimation.SlashTwoHanded =>
                eventName == HitEvent,
            _ => false
        };
    }

    protected override void OnInitialized()
    {
        _animationEvents.OnEvent -= HandleAnimationEvent;
        _animationEvents.OnEvent += HandleAnimationEvent;

        _animationManager.SetWeaponType(_character.WeaponType);
    }

    private void HandleAnimationEvent(string eventName)
    {
        if (IsAttackExecutionEvent(eventName))
        {
            NotifyAttackExecution();
        }
    }

    private void OnDestroy()
    {
        _animationEvents.OnEvent -= HandleAnimationEvent;
    }
}
