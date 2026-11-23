
using System.Collections.Generic;

/// <summary>
/// 몬스터 FSM에서 사용하는 공격 인터페이스
/// </summary>
public interface IMonsterAttackRuntime
{
    bool IsComplete { get; }

    /// <summary>
    /// 공격 완료 후 강제로 유지할 Idle 시간
    /// </summary>
    float CompletionIdleDuration { get; }

    /// <summary>
    /// 공격 실행 준비 완료 여부
    /// </summary>
    bool IsPrepared { get; }

    void Initialize(MonsterData monsterData, IReadOnlyDictionary<int, MonsterAttackData> attackTable);

    bool CanStart(Player target);

    void Begin(Player target);

    bool TryExecute();

    bool PhysicsUpdate();

    void Complete();

    void Cancel();

    bool CanInterrupt();


    /// <summary>
    /// 공격 프레임 갱신과 진행 가능 여부 반환
    /// </summary>
    bool UpdateAttack();
}
