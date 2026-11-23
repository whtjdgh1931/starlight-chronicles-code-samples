/// <summary>
/// 몬스터 FSM의 개별 상태 클래스의 부모 추상클래스
/// </summary>
public abstract class MonsterState
{
    private MonsterBase _monster;
    public MonsterBase Monster => _monster;


    private MonsterFSM _monsterFSM;
    public MonsterFSM MonsterFSM => _monsterFSM;

    public MonsterState(MonsterBase monster, MonsterFSM monsterFSM)
    {
        _monster = monster;
        _monsterFSM = monsterFSM;
    }

    public abstract void Enter();

    public abstract void Update();

    public virtual void PhysicsUpdate()
    {

    }

    public abstract void Exit();

    public virtual void OnAnimationFinished()
    {
    }

}
