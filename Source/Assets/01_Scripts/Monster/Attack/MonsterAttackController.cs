using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 패턴이 여러 개인 몬스터의 공격 선택과 실행 생명주기 관리
/// </summary>
public class MonsterAttackController : MonoBehaviour, IMonsterAttackRuntime
{
    [SerializeField] private MonsterAttackBehaviour[] _attacks;

    private MonsterAttackBehaviour _preparedAttack;
    private MonsterAttackBehaviour _currentAttack;
    private MonsterAttackBehaviour _lastAttack;
    private readonly Dictionary<int, float> _phaseAttackWeights = new Dictionary<int, float>();
    public bool IsComplete => _currentAttack != null && _currentAttack.IsComplete;
    public float CompletionIdleDuration => _currentAttack != null ? _currentAttack.CompletionIdleDuration : 0f;

    public bool IsPrepared => _currentAttack != null && _currentAttack.IsPrepared;

    /// <summary>
    /// 모든 공격 초기화
    /// </summary>
    public void Initialize(MonsterData monsterData, IReadOnlyDictionary<int, MonsterAttackData> attackTable)
    {
        ValidateReferences();

        foreach (MonsterAttackBehaviour attack in _attacks)
        {
            attack.Initialize(monsterData, attackTable);
        }

        _preparedAttack = null;
        _currentAttack = null;
        _lastAttack = null;
        _phaseAttackWeights.Clear();
    }

    /// <summary>
    /// 현재 보스 페이즈의 사용 가능 공격과 선택 비중 설정
    /// </summary>
    public void ConfigurePhaseAttacks(IReadOnlyList<BossPhaseAttackData> phaseAttacks)
    {
        if (phaseAttacks.Count == 0)
        {
            throw new ArgumentException($"{name}: 한 개 이상의 페이즈 공격 데이터 필요", nameof(phaseAttacks));
        }

        var weights = new Dictionary<int, float>(phaseAttacks.Count);

        foreach (BossPhaseAttackData phaseAttack in phaseAttacks)
        {
            if (!ContainsAttackPattern(phaseAttack.PatternID))
            {
                throw new InvalidOperationException($"{name}: 연결되지 않은 페이즈 공격 패턴 PatternID: {phaseAttack.PatternID}");
            }

            if (phaseAttack.Weight <= 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(phaseAttacks), $"{name}: 페이즈 공격 비중은 0 초과 필요 PatternID: {phaseAttack.PatternID}");
            }

            if (!weights.TryAdd(phaseAttack.PatternID, phaseAttack.Weight))
            {
                throw new InvalidOperationException($"{name}: 페이즈 공격 패턴 중복 PatternID: {phaseAttack.PatternID}");
            }
        }

        // 검증 완료 후 기존 설정 교체
        _phaseAttackWeights.Clear();
        foreach (KeyValuePair<int, float> weight in weights)
        {
            _phaseAttackWeights.Add(weight.Key, weight.Value);
        }
        _preparedAttack = null;
    }

    /// <summary>
    /// 공격 목록 참조 검증
    /// </summary>
    private void ValidateReferences()
    {
        if (_attacks == null || _attacks.Length == 0)
        {
            throw new MissingReferenceException($"{name}: 한 개 이상의 몬스터 공격 필요");
        }

        for (int index = 0; index < _attacks.Length; index++)
        {
            if (_attacks[index] == null)
            {
                throw new MissingReferenceException($"{name}: {index}번 몬스터 공격 참조 필요");
            }

            ValidateDuplicateAttack(index);
        }
    }

    private void ValidateDuplicateAttack(int targetIndex)
    {
        for (int index = targetIndex + 1; index < _attacks.Length; index++)
        {
            if (_attacks[targetIndex] == _attacks[index])
            {
                throw new InvalidOperationException($"{name}: 동일한 몬스터 공격 중복 연결 불가");
            }
        }
    }

    /// <summary>
    /// 실행 가능한 공격 선택
    /// </summary>
    public bool CanStart(Player target)
    {
        if (_currentAttack != null)
        {
            return false;
        }

        _preparedAttack = SelectAttack(target);
        return _preparedAttack != null;
    }

    /// <summary>
    /// 선택된 공격 준비 시작
    /// </summary>
    public void Begin(Player target)
    {
        if (_preparedAttack == null)
        {
            throw new InvalidOperationException($"{name}: 준비된 몬스터 공격 필요");
        }

        _currentAttack = _preparedAttack;
        _preparedAttack = null;
        _currentAttack.Begin(target);
    }

    /// <summary>
    /// 현재 공격 프레임 갱신
    /// </summary>
    public bool UpdateAttack()
    {
        return _currentAttack != null && _currentAttack.UpdateAttack();
    }

    /// <summary>
    /// 현재 공격 판정 실행
    /// </summary>
    public bool TryExecute()
    {
        return _currentAttack != null && _currentAttack.TryExecute();
    }

    /// <summary>
    /// 현재 공격 물리 갱신
    /// </summary>
    public bool PhysicsUpdate()
    {
        return _currentAttack != null && _currentAttack.PhysicsUpdate();
    }

    /// <summary>
    /// 현재 공격 정상 완료
    /// </summary>
    public void Complete()
    {
        if (_currentAttack == null)
        {
            return;
        }

        _currentAttack.Complete();
        _lastAttack = _currentAttack;
        _currentAttack = null;
    }

    /// <summary>
    /// 현재 공격의 피격 중단 가능 여부 확인
    /// </summary>
    public bool CanInterrupt()
    {
        return _currentAttack == null || _currentAttack.CanInterrupt();
    }

    private bool ContainsAttackPattern(int patternId)
    {
        foreach (MonsterAttackBehaviour attack in _attacks)
        {
            if (attack.PatternId == patternId)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// 쿨다운과 사거리 조건을 만족하는 공격 선택
    /// </summary>
    private MonsterAttackBehaviour SelectAttack(Player target)
    {
        MonsterAttackBehaviour selectedAttack = SelectWeightedAttack(target, true);
        return selectedAttack != null ? selectedAttack : SelectWeightedAttack(target, false);
    }

    /// <summary>
    /// 직전 공격 제외 여부와 데이터 비중 기반 공격 선택
    /// </summary>
    private MonsterAttackBehaviour SelectWeightedAttack(Player target, bool excludeLastAttack)
    {
        float totalWeight = 0f;

        foreach (MonsterAttackBehaviour attack in _attacks)
        {
            float weight = GetSelectionWeight(attack);

            if (weight > 0f && (!excludeLastAttack || attack != _lastAttack) && attack.CanStart(target))
            {
                totalWeight += weight;
            }
        }

        if (totalWeight <= 0f)
        {
            return null;
        }

        float selectionValue = UnityEngine.Random.value * totalWeight;
        MonsterAttackBehaviour lastSelectableAttack = null;

        foreach (MonsterAttackBehaviour attack in _attacks)
        {
            float weight = GetSelectionWeight(attack);

            if (weight <= 0f || (excludeLastAttack && attack == _lastAttack) || !attack.CanStart(target))
            {
                continue;
            }

            lastSelectableAttack = attack;
            selectionValue -= weight;

            if (selectionValue <= 0f)
            {
                return attack;
            }
        }

        return lastSelectableAttack;
    }

    private float GetSelectionWeight(MonsterAttackBehaviour attack)
    {
        if (_phaseAttackWeights.Count == 0)
        {
            return 1f;
        }

        return _phaseAttackWeights.TryGetValue(attack.PatternId, out float weight) ? weight : 0f;
    }

    /// <summary>
    /// 준비 또는 실행 중인 공격 취소
    /// </summary>
    public void Cancel()
    {
        _preparedAttack = null;

        if (_currentAttack == null)
        {
            return;
        }

        _currentAttack.Cancel();
        _lastAttack = _currentAttack;
        _currentAttack = null;
    }

}

