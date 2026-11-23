using System;
using UnityEngine;

/// <summary>
/// 마을에 배치된 훈련 대상의 게임 시작과 종료 연결
/// </summary>
public sealed class HomeTrainingGround : MonoBehaviour
{
    [SerializeField] private TrainingDummy[] _dummies;
    private bool _isInitialized;

    /// <summary>
    /// 데이터 로딩과 맵 준비 후 훈련 대상의 전투 시작
    /// </summary>
    public void Initialize(DataManager dataManager)
    {
        if (_isInitialized) { return; }
        if (_dummies == null || _dummies.Length == 0)
        {
            throw new MissingReferenceException($"{name}: 훈련 대상 연결 필요");
        }
        if (!dataManager.IsDataLoaded)
        {
            throw new InvalidOperationException($"{name}: 훈련 시작 전 데이터 로딩 완료 필요");
        }
        foreach (TrainingDummy dummy in _dummies)
        {
            if (dummy == null)
            {
                throw new MissingReferenceException($"{name}: 훈련 대상 참조 누락");
            }
            if (!dataManager.MonsterTable.ContainsKey(dummy.SourceMonsterId))
            {
                throw new InvalidOperationException($"{dummy.name}: 원본 몬스터 데이터 누락 ID: {dummy.SourceMonsterId}");
            }
        }
        foreach (TrainingDummy dummy in _dummies)
        {
            dummy.Initialize(dataManager.MonsterTable[dummy.SourceMonsterId], dataManager.MonsterAttackTable);
        }
        _isInitialized = true;
    }

    private void OnDisable()
    {
        if (!_isInitialized) { return; }
        foreach (TrainingDummy dummy in _dummies)
        {
            if (dummy != null) { dummy.StopCombat(); }
        }
        _isInitialized = false;
    }
}
