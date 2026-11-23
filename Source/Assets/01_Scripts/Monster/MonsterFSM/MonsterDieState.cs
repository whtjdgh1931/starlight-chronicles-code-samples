public class MonsterDieState : MonsterState
{
    public MonsterDieState(MonsterBase monster, MonsterFSM monsterFSM) : base(monster, monsterFSM)
    {
    }

    public override void Enter()
    {
        Monster.OnDie();
        Monster.PlayDieAnimation();
    }

    public override void Exit()
    {

    }

    public override void Update()
    {

    }

    public override void OnAnimationFinished()
    {
        Monster.CompleteDeath();
    }
}
