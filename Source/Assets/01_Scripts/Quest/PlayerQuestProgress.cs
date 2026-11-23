using System;
using System.Collections.Generic;

/// <summary>
/// 캐릭터별 퀘스트 수락·진행 관리와 독립 저장 스냅샷 생성
/// 아이템 소모와 보상 지급 책임 제외
/// </summary>
public sealed class PlayerQuestProgress
{
    /// <summary>
    /// 화면에서 공통으로 사용하는 의뢰 상태
    /// </summary>
    public enum DisplayState
    {
        Locked,
        Available,
        InProgress,
        Ready,
        Claimed
    }

    /// <summary>
    /// 조회 시점의 개별 목표 진행도
    /// </summary>
    public readonly struct ObjectiveInfo
    {
        public QuestObjectiveType Type { get; }
        public int TargetId { get; }
        public string Name { get; }
        public long Current { get; }
        public long Required { get; }
        public bool Satisfied => Required > 0 && Current >= Required;

        public ObjectiveInfo(QuestObjectiveType type, int targetId, string name, long current, long required)
        {
            Type = type;
            TargetId = targetId;
            Name = name;
            Current = current;
            Required = required;
        }
    }

    /// <summary>
    /// 조회 시점의 의뢰 상태와 목표별 수량
    /// 완료 의뢰의 과거 아이템 보유량은 별도 보관하지 않음
    /// </summary>
    public readonly struct QuestInfo
    {
        public int QuestId { get; }
        public string Name { get; }
        public DisplayState Status { get; }
        public bool PrerequisiteSatisfied { get; }
        public IReadOnlyList<ObjectiveInfo> Objectives { get; }

        public QuestInfo(int questId, string name, DisplayState status,
            bool prerequisiteSatisfied, IReadOnlyList<ObjectiveInfo> objectives)
        {
            QuestId = questId;
            Name = name;
            Status = status;
            PrerequisiteSatisfied = prerequisiteSatisfied;
            Objectives = new List<ObjectiveInfo>(objectives).AsReadOnly();
        }
    }

    private readonly Dictionary<int, QuestSaveData> _records = new();
    private DataManager _dataManager;
    private PlayerInventory _inventory;

    /// <summary>
    /// 기록 또는 표시용 아이템 수량의 변경 알림
    /// </summary>
    public event Action Changed;

    /// <summary>
    /// 의뢰 조회 의존성과 인벤토리 변경 알림 연결
    /// 재초기화 시 이전 구독 해제, 기존 기록 유지
    /// </summary>
    public void Initialize(DataManager dataManager, PlayerInventory inventory)
    {
        if (dataManager == null) { throw new ArgumentNullException(nameof(dataManager)); }
        if (inventory == null) { throw new ArgumentNullException(nameof(inventory)); }
        Shutdown();
        _dataManager = dataManager;
        _inventory = inventory;
        _inventory.InventoryChanged += HandleInventoryChanged;
    }

    /// <summary>
    /// 캐릭터 정리 시 인벤토리 알림과 조회 의존성 해제
    /// 저장용 기록은 Clear에서 별도 정리
    /// </summary>
    public void Shutdown()
    {
        // 파괴된 Unity 컴포넌트의 관리 이벤트도 대응 해제
        if (_inventory is not null)
        {
            _inventory.InventoryChanged -= HandleInventoryChanged;
        }
        _inventory = null;
        _dataManager = null;
    }

    /// <summary>
    /// 정의가 존재하는 의뢰의 현재 상태와 목표별 진행 조회
    /// 실제 보상 지급의 공간·수량 검사는 보상 서비스에서 최종 판정
    /// </summary>
    public QuestInfo GetDisplayInfo(int questId)
    {
        EnsureQueryInitialized();
        QuestData quest = _dataManager.QuestTable[questId];
        bool accepted = TryGetProgress(questId, out int killCount, out bool claimed);
        bool prerequisiteSatisfied = quest.RequiredQuestID <= 0 ||
            (TryGetProgress(quest.RequiredQuestID, out _, out bool prerequisiteClaimed) && prerequisiteClaimed);
        List<ObjectiveInfo> objectives = BuildObjectives(quest, killCount);
        bool objectivesSatisfied = objectives.Count > 0;
        foreach (ObjectiveInfo objective in objectives) { objectivesSatisfied &= objective.Satisfied; }

        DisplayState state;
        if (claimed) { state = DisplayState.Claimed; }
        else if (accepted) { state = objectivesSatisfied ? DisplayState.Ready : DisplayState.InProgress; }
        else { state = prerequisiteSatisfied ? DisplayState.Available : DisplayState.Locked; }
        return new QuestInfo(quest.ID, quest.Name, state, prerequisiteSatisfied, objectives);
    }

    /// <summary>
    /// 수락했고 보상을 받지 않은 의뢰의 현재 정보 조회
    /// 저장 이후 삭제된 정의 제외, ID 순서로 반환
    /// </summary>
    public IReadOnlyList<QuestInfo> GetAcceptedUnclaimed()
    {
        EnsureQueryInitialized();
        var result = new List<QuestInfo>();
        foreach (QuestSaveData record in _records.Values)
        {
            if (!record.RewardClaimed && _dataManager.QuestTable.ContainsKey(record.QuestId))
            {
                result.Add(GetDisplayInfo(record.QuestId));
            }
        }
        result.Sort((left, right) => left.QuestId.CompareTo(right.QuestId));
        return result.AsReadOnly();
    }

    private void EnsureQueryInitialized()
    {
        if (_dataManager == null || _inventory == null)
        {
            throw new InvalidOperationException("의뢰 정보 조회 전에 PlayerQuestProgress 초기화 필요");
        }
    }

    private List<ObjectiveInfo> BuildObjectives(QuestData quest, int killCount)
    {
        var objectives = new List<ObjectiveInfo>();
        switch (quest.ObjectiveType)
        {
            case QuestObjectiveType.KillMonster:
                objectives.Add(new ObjectiveInfo(quest.ObjectiveType, quest.TargetID,
                    _dataManager.MonsterTable[quest.TargetID].Name, killCount, quest.TargetCount));
                break;
            case QuestObjectiveType.CollectItem:
                foreach (ItemRequirement requirement in quest.GetRequiredItems())
                {
                    ItemData item = _dataManager.ItemTable[requirement.ItemId];
                    objectives.Add(new ObjectiveInfo(quest.ObjectiveType, item.ID,
                        item.Name, _inventory.CountItems(item), requirement.Count));
                }
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(quest.ObjectiveType), quest.ObjectiveType,
                    "지원하지 않는 의뢰 목표 유형");
        }
        return objectives;
    }

    private void HandleInventoryChanged(InventoryTab tab, InventoryItemData[] items, int slotCount)
    {
        // 미수락 의뢰의 상세 화면도 현재 보유량 갱신
        Changed?.Invoke();
    }

    /// <summary>
    /// 검증된 퀘스트 정의로 수락 기록 생성
    /// 중복 수락과 선행 퀘스트 보상 미수령 시 수락 제한
    /// </summary>
    public bool TryAccept(QuestData quest) => Accept(quest).Reason == QuestRequestResult.Code.Success;

    /// <summary>
    /// 수락 조건 확인과 실행 당시 선행 수령 상태 반환
    /// </summary>
    public QuestRequestResult Accept(QuestData quest)
    {
        if (_records.ContainsKey(quest.ID))
        {
            return new QuestRequestResult(QuestRequestResult.Code.AlreadyAccepted, "이미 수락한 퀘스트입니다. 추가 변경 없음");
        }
        if (quest.RequiredQuestID > 0 &&
            (!_records.TryGetValue(quest.RequiredQuestID, out QuestSaveData required) || !required.RewardClaimed))
        {
            bool accepted = _records.TryGetValue(quest.RequiredQuestID, out required);
            string state = accepted ? $"수락됨 · 진행 {required.ProgressCount} · 보상 미수령" : "미수락";
            return new QuestRequestResult(QuestRequestResult.Code.Prerequisite,
                $"선행 퀘스트 {quest.RequiredQuestID}: {state}\n필요 조건: 선행 퀘스트 보상 수령 완료",
                0, 1, quest.RequiredQuestID);
        }
        _records.Add(quest.ID, new QuestSaveData { QuestId = quest.ID });
        Changed?.Invoke();
        return new QuestRequestResult(QuestRequestResult.Code.Success, "퀘스트를 수락했습니다. 메모리에 반영되며 자동 저장하지 않습니다.");
    }

    /// <summary>
    /// 사망이 확정된 몬스터의 수락 퀘스트 진행도 갱신
    /// 목표 초과·수령 완료·저장 이후 삭제된 퀘스트 집계 제외
    /// </summary>
    public void RecordMonsterKill(int monsterId, IReadOnlyDictionary<int, QuestData> definitions)
    {
        bool changed = false;
        foreach (QuestSaveData record in _records.Values)
        {
            if (record.RewardClaimed || !definitions.TryGetValue(record.QuestId, out QuestData quest))
            {
                continue;
            }
            if (quest.ObjectiveType != QuestObjectiveType.KillMonster ||
                quest.TargetID != monsterId || record.ProgressCount >= quest.TargetCount)
            {
                continue;
            }
            record.ProgressCount++;
            changed = true;
        }
        if (changed) { Changed?.Invoke(); }
    }

    /// <summary>
    /// 특정 퀘스트의 진행도와 수령 여부 조회
    /// 내부 기록의 외부 변경 방지를 위한 값 반환
    /// </summary>
    public bool TryGetProgress(int questId, out int progressCount, out bool rewardClaimed)
    {
        if (_records.TryGetValue(questId, out QuestSaveData record))
        {
            progressCount = record.ProgressCount;
            rewardClaimed = record.RewardClaimed;
            return true;
        }

        progressCount = 0;
        rewardClaimed = false;
        return false;
    }

    /// <summary>
    /// 보상 지급을 마친 수락 퀘스트의 수령 완료 반영
    /// 보상 서비스에서 수락·미수령 확인 후 호출
    /// </summary>
    public void CompleteReward(int questId)
    {
        _records[questId].RewardClaimed = true;
        Changed?.Invoke();
    }

    /// <summary>
    /// 저장 데이터와 참조를 공유하지 않는 현재 기록 추출
    /// </summary>
    public List<QuestSaveData> Capture()
    {
        List<QuestSaveData> snapshot = new(_records.Count);
        foreach (QuestSaveData record in _records.Values) { snapshot.Add(Copy(record)); }
        snapshot.Sort((left, right) => left.QuestId.CompareTo(right.QuestId));
        return snapshot;
    }

    /// <summary>
    /// 검증된 캐릭터 기록으로 교체, 기존 캐릭터 기록 제거
    /// </summary>
    public void Restore(IReadOnlyList<QuestSaveData> records)
    {
        _records.Clear();
        foreach (QuestSaveData record in records) { _records.Add(record.QuestId, Copy(record)); }
        Changed?.Invoke();
    }

    /// <summary>
    /// 네트워크 풀 반환 시 이전 캐릭터 기록 초기화
    /// </summary>
    public void Clear()
    {
        _records.Clear();
        Changed?.Invoke();
    }

    private static QuestSaveData Copy(QuestSaveData record)
    {
        return new QuestSaveData
        {
            QuestId = record.QuestId,
            ProgressCount = record.ProgressCount,
            RewardClaimed = record.RewardClaimed
        };
    }
}
