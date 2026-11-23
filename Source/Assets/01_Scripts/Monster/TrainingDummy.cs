using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 체력 소모와 사망 없이 반복 타격을 받는 고정 훈련 대상
/// </summary>
public sealed class TrainingDummy : MonsterBase
{
    [SerializeField] private bool _canAttack;
    [SerializeField, Min(1)] private int _sourceMonsterId = 1014;

    public int SourceMonsterId => _sourceMonsterId;
    protected override bool UsesWorldHealthBar => false;
    protected override bool UsesStandardMotionPresentation => false;
    protected override bool ConsumesHealth => false;
    protected override bool RetaliatesOnHit => _canAttack;
    protected override IMonsterAttackRuntime AttackRuntime => _canAttack ? base.AttackRuntime : null;

    /// <summary>
    /// 원본 시트 데이터의 복사본으로 무보상 고정 훈련 대상 초기화
    /// </summary>
    public override void Initialize(MonsterData data, IReadOnlyDictionary<int, MonsterAttackData> attackTable)
    {
        var trainingData = JsonUtility.FromJson<MonsterData>(JsonUtility.ToJson(data));
        trainingData.Speed = 0f;
        trainingData.IdleDuration = float.PositiveInfinity;
        trainingData.Exp = 0f;
        trainingData.ContactDamageEnabled = false;
        trainingData.MinGold = 0;
        trainingData.MaxGold = 0;
        trainingData.GoldDropChance = 0f;
        trainingData.DropItemIDs = string.Empty;
        trainingData.DropItemChances = string.Empty;
        base.Initialize(trainingData, attackTable);
        Rb.constraints = RigidbodyConstraints2D.FreezeAll;
        Rb.gravityScale = 0f;
    }
}
