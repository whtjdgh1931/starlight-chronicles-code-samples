using UnityEngine;


/// <summary>
/// Sprite 기반 몬스터 애니메이션 제어
/// </summary>
public class MonsterAnimator : MonsterAnimationController
{
    private static readonly int IdleState = Animator.StringToHash(Constants.ANIM_MonsterIdle);
    private static readonly int MoveState = Animator.StringToHash(Constants.ANIM_MonsterMove);
    private static readonly int HitTrigger = Animator.StringToHash(Constants.ANIM_MonsterHit);
    private static readonly int DieTrigger = Animator.StringToHash(Constants.ANIM_MonsterDie);
    private static readonly int AttackTrigger = Animator.StringToHash(Constants.ANIM_MonsterAttack);


    [SerializeField] private Animator _animator;
    [SerializeField] private SpriteRenderer[] _spriteRenderers;
    [SerializeField] private bool _facesRightByDefault;

    protected override void ValidateReferences()
    {
        if (_animator == null)
        {
            throw new MissingReferenceException($"{name}: Animator 참조 필요");
        }

        if (_spriteRenderers == null ||_spriteRenderers.Length == 0)
        {
            throw new MissingReferenceException($"{name}: SpriteRenderer 참조 필요");
        }
    }

    public override void PlayIdle()
    {
        _animator.Play(IdleState);
    }

    public override void PlayMove()
    {
        _animator.Play(MoveState);
    }

    public override void PlayAttack()
    {
        _animator.SetTrigger(AttackTrigger);
    }

    public override void PlayHit()
    {
        _animator.SetTrigger(HitTrigger);
    }

    public override void PlayDie()
    {
        _animator.SetTrigger(DieTrigger);
    }

    public override void SetFacingDirection(float directionX)
    {
        bool shouldFlip =
            (directionX > 0f) != _facesRightByDefault;

        foreach (SpriteRenderer spriteRenderer in _spriteRenderers)
        {
            spriteRenderer.flipX = shouldFlip;
        }
    }
}
