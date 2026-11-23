using System.Collections;
using UnityEngine;

/// <summary>
/// 플레이어 스킬 실행 방식
/// </summary>
public enum PlayerSkillExecutionType
{
    Area,
    Projectile,
    Dash
}



/// <summary>
/// 실행 방식별 플레이어 스킬 기반 클래스
/// </summary>
public abstract class PlayerSkillExecutorBehaviour : MonoBehaviour
{
    public abstract PlayerSkillExecutionType ExecutionType { get; }

    /// <summary>
    /// 실행기의 준비·이동·복귀를 포함한 행동 연출 시간
    /// 별도 시간 제어가 없는 실행기는 기존 애니메이션 길이 사용
    /// </summary>
    public virtual float GetActionDuration(SkillData skillData) => 0f;

    /// <summary>
    /// 실행기 필수 참조 초기화
    /// </summary>
    public virtual void Initialize()
    {
    }

    /// <summary>
    /// 스킬 실행
    /// </summary>
    public abstract IEnumerator Execute(SkillData skillData, Player attacker, IAttackPowerProvider attackPowerProvider);

    /// <summary>
    /// 진행 중인 스킬 취소
    /// </summary>
    public virtual void Cancel()
    {
    }
}