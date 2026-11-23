using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;


/// <summary>
/// 직업별 스킬 조회와 MP, 쿨다운 및 실행 상태 관리
/// </summary>
public sealed class PlayerSkillController : MonoBehaviour
{
    [SerializeField] private int[] _skillSlotIds;
    [SerializeField] private PlayerSkillExecutorBehaviour[] _executors;

    private readonly Dictionary<int, SkillData> _skills = new();
    private readonly Dictionary<int, float> _nextUseTimes = new();
    private readonly Dictionary<int, PlayerSkillExecutionType> _executionTypes = new();
    private readonly Dictionary<PlayerSkillExecutionType, PlayerSkillExecutorBehaviour> _executorTable = new();

    private PlayerStat _playerStat;
    private PlayerSkillExecutorBehaviour _currentExecutor;
    private Coroutine _skillCoroutine;

    /// <summary>
    /// 승인된 스킬의 모션 종류와 전체 연출 시간 전달
    /// 실제 타격 실행 전 로컬·네트워크 모션 시작
    /// </summary>
    public event Action<PlayerSkillExecutionType, float> ActionStarted;

    public bool IsExecuting => _currentExecutor != null;

    /// <summary>
    /// 실제 사용 승인 시 슬롯과 재사용 대기 시간 전달
    /// </summary>
    public event Action<int, float> CooldownStarted;

    public float GetCooldownRemaining(int slotIndex)
    {
        return _nextUseTimes.TryGetValue(_skillSlotIds[slotIndex], out float nextUseTime) ? Mathf.Max(0f, nextUseTime - Time.time) : 0f;
    }

    /// <summary>
    /// 서버 승인 시각으로 보정한 소유자 화면의 재사용 대기 반영
    /// 스킬 실행·MP 차감 없이 표시용 시간만 갱신
    /// </summary>
    public void ApplyCooldown(int slotIndex, float remaining)
    {
        _nextUseTimes[_skillSlotIds[slotIndex]] = Time.time + remaining;
    }


    /// <summary>
    /// 직업 스킬과 실행기 초기화
    /// </summary>
    public void Initialize(IReadOnlyDictionary<int, SkillData> skillTable, PlayerStat playerStat)
    {
        if (skillTable == null)
        {
            throw new ArgumentNullException(nameof(skillTable));
        }

        _playerStat = playerStat ?? throw new ArgumentNullException(nameof(playerStat));

        if (_skillSlotIds == null || _skillSlotIds.Length == 0)
        {
            throw new MissingReferenceException($"{name}: 한 개 이상의 스킬 슬롯 ID 필요");
        }

        if (_executors == null || _executors.Length == 0)
        {
            throw new MissingReferenceException($"{name}: 한 개 이상의 스킬 실행기 필요");
        }

        Cancel();

        _skills.Clear();
        _nextUseTimes.Clear();
        _executionTypes.Clear();
        _executorTable.Clear();

        RegisterExecutors();
        RegisterClassSkills(skillTable);
        ValidateSkillSlots();
    }

    /// <summary>
    /// Inspector에 연결된 스킬 실행기 등록
    /// </summary>
    private void RegisterExecutors()
    {
        foreach (PlayerSkillExecutorBehaviour executor in _executors)
        {
            if (executor == null)
            {
                throw new MissingReferenceException($"{name}: 스킬 실행기 참조 필요");
            }

            executor.Initialize();

            if (!_executorTable.TryAdd(executor.ExecutionType, executor))
            {
                throw new InvalidOperationException($"{name}: 중복 스킬 실행기 {executor.ExecutionType}");
            }
        }
    }

    /// <summary>
    /// 현재 직업에 해당하는 스킬 등록
    /// </summary>
    private void RegisterClassSkills(IReadOnlyDictionary<int, SkillData> skillTable)
    {
        string playerClass = _playerStat.Class.ToString();

        foreach (SkillData skillData in skillTable.Values)
        {
            if (!string.Equals(skillData.PlayerClass, playerClass, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (!Enum.TryParse(skillData.ExecutionType, true, out PlayerSkillExecutionType executionType))
            {
                throw new InvalidOperationException($"{skillData.SkillName}: 지원하지 않는 스킬 실행 방식 {skillData.ExecutionType}");
            }

            _skills.Add(skillData.ID, skillData);
            _executionTypes.Add(skillData.ID, executionType);
        }
    }

    /// <summary>
    /// 스킬 슬롯 데이터와 실행기 연결 검증
    /// </summary>
    private void ValidateSkillSlots()
    {
        foreach (int skillId in _skillSlotIds)
        {
            if (!_skills.ContainsKey(skillId))
            {
                throw new InvalidOperationException($"{name}: 현재 직업에서 사용할 수 없는 스킬 ID {skillId}");
            }

            PlayerSkillExecutionType executionType = _executionTypes[skillId];

            if (!_executorTable.ContainsKey(executionType))
            {
                throw new InvalidOperationException($"{name}: {executionType} 스킬 실행기 필요");
            }
        }
    }

    /// <summary>
    /// 지정된 슬롯의 스킬 실행 요청
    /// </summary>
    public bool TryExecuteSlot(int slotIndex, Player attacker)
    {
        if (attacker == null)
        {
            throw new ArgumentNullException(nameof(attacker));
        }

        if (IsExecuting || slotIndex < 0 || slotIndex >= _skillSlotIds.Length)
        {
            return false;
        }

        int skillId = _skillSlotIds[slotIndex];
        SkillData skillData = _skills[skillId];

        if (_nextUseTimes.TryGetValue(skillId, out float nextUseTime) && Time.time < nextUseTime)
        {
            return false;
        }

        if (_playerStat.CurrentMp < skillData.MpCost)
        {
            return false;
        }

        PlayerSkillExecutorBehaviour executor = _executorTable[_executionTypes[skillId]];

        _playerStat.ChangeMp(-skillData.MpCost);
        _nextUseTimes[skillId] = Time.time + skillData.Cooldown;
        _currentExecutor = executor;
        CooldownStarted?.Invoke(slotIndex, skillData.Cooldown);

        ActionStarted?.Invoke(executor.ExecutionType, executor.GetActionDuration(skillData));
        Coroutine coroutine = StartCoroutine(CoExecute(executor, skillData, attacker));
        _skillCoroutine = _currentExecutor == null ? null : coroutine;

        return true;
    }

    /// <summary>
    /// 선택된 실행기로 스킬 실행
    /// </summary>
    private IEnumerator CoExecute(PlayerSkillExecutorBehaviour executor, SkillData skillData, Player attacker)
    {
        try
        {
            yield return executor.Execute(skillData, attacker, _playerStat);
        }
        finally
        {
            if (_currentExecutor == executor)
            {
                _currentExecutor = null;
                _skillCoroutine = null;
            }
        }
    }

    /// <summary>
    /// 현재 스킬 실행 취소
    /// </summary>
    public void Cancel()
    {
        if (_currentExecutor == null)
        {
            return;
        }

        PlayerSkillExecutorBehaviour executor = _currentExecutor;
        Coroutine coroutine = _skillCoroutine;

        _currentExecutor = null;
        _skillCoroutine = null;

        executor.Cancel();

        if (coroutine != null)
        {
            StopCoroutine(coroutine);
        }
    }

    private void OnDisable()
    {
        Cancel();
    }

    #region Debug
    /// <summary>
    /// 지정 슬롯의 스킬 데이터 조회
    /// </summary>
    public bool TryGetSlotSkill(int slotIndex, out SkillData skillData)
    {
        skillData = null;
        return slotIndex >= 0 && slotIndex < _skillSlotIds.Length && _skills.TryGetValue(_skillSlotIds[slotIndex], out skillData);
    }

    public int SkillSlotCount => _skillSlotIds.Length;
    #endregion
}
