using System;
using System.Collections.Generic;
using UnityEngine;


/// <summary>
/// 보스 전용 데이터와 다중 공격 초기화
/// </summary>
public sealed class BossMonster : MonsterBase
{
    private const float HitReactionCooldownSeconds = 0.5f;

    public enum Phase : byte
    {
        First = 1,
        Second = 2
    }

    [SerializeField] private MonsterAttackController _attackController;

    private BossData _bossData;
    public BossData BossData => _bossData;


    protected override bool UsesWorldHealthBar => false;
    internal override float HitReactionCooldown => HitReactionCooldownSeconds;
    protected override bool UsesStandardMotionPresentation => false;
    protected override IMonsterAttackRuntime AttackRuntime => _attackController;

    /// <summary>
    /// 일반 몬스터 초기화 경로 차단
    /// </summary>
    public override void Initialize(MonsterData data, IReadOnlyDictionary<int, MonsterAttackData> attackTable)
    {
        throw new InvalidOperationException($"{name}: 보스 전용 Initialize 호출 필요");
    }

    /// <summary>
    /// 보스 데이터와 1페이즈 공격 초기화
    /// </summary>
    public void Initialize(
        MonsterData monsterData,
        BossData bossData,
        IReadOnlyDictionary<int, MonsterAttackData> attackTable,
        IReadOnlyList<BossPhaseAttackData> phaseAttacks)
    {
        ValidateBossData(monsterData, bossData, phaseAttacks);


        _bossData = bossData;
        base.Initialize(monsterData, attackTable);
        _attackController.ConfigurePhaseAttacks(phaseAttacks);
    }

    /// <summary>
    /// 클라이언트 보스 데이터와 표시 상태 초기화
    /// 공격 패턴과 페이즈 판정 실행 제외
    /// </summary>
    public void InitializeReplica(MonsterData monsterData, BossData bossData)
    {
        if (bossData.BossId != monsterData.ID)
        {
            throw new ArgumentException("보스 데이터와 몬스터 데이터 ID 불일치");
        }

        _bossData = bossData;
        base.InitializeReplica(monsterData);
    }

    /// <summary>
    /// 현재 페이즈의 공격 목록과 선택 비중 적용
    /// </summary>
    public void ApplyPhaseAttacks(IReadOnlyList<BossPhaseAttackData> phaseAttacks)
    {
        _attackController.ConfigurePhaseAttacks(phaseAttacks);
    }

    private void ValidateBossData(
     MonsterData monsterData,
     BossData bossData,
     IReadOnlyList<BossPhaseAttackData> phaseAttacks)
    {
        if (_attackController == null)
        {
            throw new MissingReferenceException($"{name}: MonsterAttackController 참조 필요");
        }

        if (bossData == null)
        {
            throw new ArgumentNullException(nameof(bossData));
        }

        if (bossData.BossId != monsterData.ID)
        {
            throw new InvalidOperationException($"{name}: MonsterData와 BossData ID 불일치");
        }

        if (phaseAttacks.Count == 0)
        {
            throw new InvalidOperationException($"{name}: 1페이즈 공격 데이터 필요");
        }



        foreach (BossPhaseAttackData phaseAttack in phaseAttacks)
        {
            if (phaseAttack.BossID != bossData.BossId || phaseAttack.Phase != (int)Phase.First)
            {
                throw new InvalidOperationException($"{name}: 다른 보스 또는 페이즈 공격 데이터 포함");
            }
        }
    }



}
