using System;
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using Outcome = QuestValidationResult.Outcome;
using RequestState = QuestValidationResult.RequestState;
using CheckState = QuestValidationResult.CheckState;
using Unity.Netcode;
using UnityEngine;

/// <summary>
/// 실제 게임 API를 이용한 Inspector 퀘스트 검증 실행
/// 파일 읽기·보상 비교·일회성 저장 실패 검사 조율
/// </summary>
internal sealed class QuestValidationRunner
{
    internal enum Command
    {
        Accept,
        KillMonster,
        GiveItem,
        Claim,
        ExpectClaimSuccess,
        ExpectClaimFailure,
        RepeatAccept,
        RepeatClaim,
        AcceptWithSaveFailure,
        ClaimWithSaveFailure,
        SaveFailureAndRetry,
        RetrySave,
        CompareFile,
        CaptureBaseline,
        CompareBaseline,
        ResetSelectedQuest,
        ResetAllQuests,
        SimulateMonsterKills
    }

    private sealed class Context
    {
        internal GameManager Manager;
        internal Player Player;
        internal PlayerSaveController Save;
        internal QuestData Quest;
        internal int Slot;

        internal QuestValidationSnapshot Capture() =>
            new QuestValidationSnapshot(Player.CaptureCharacterState());

        internal SaveFileStorage Storage => Manager.CharacterSaveLocator.CreateStorage(Slot);
    }

    private QuestValidationSnapshot _baseline;
    private Player _baselinePlayer;
    private int _baselineSlot;


    private const int HistoryLimit = 20;
    private readonly string _historyKey;
    private QuestValidationResult.History _history;

    internal IReadOnlyList<QuestValidationResult> History => _history.Entries;
    internal string Report => _history.Entries.Count == 0 ? "아직 실행하지 않았습니다." : _history.Entries[0].ToReport();

    internal QuestValidationRunner(EntityId testerEntityId)
    {
        _historyKey = "QuestValidation.History.v2." + testerEntityId;
        string json = SessionState.GetString(_historyKey, string.Empty);
        try
        {
            _history = string.IsNullOrEmpty(json) ? new QuestValidationResult.History()
                : JsonUtility.FromJson<QuestValidationResult.History>(json);
        }
        catch (ArgumentException)
        {
            _history = new QuestValidationResult.History();
        }
        if (_history == null || _history.Entries == null) { _history = new QuestValidationResult.History(); }
        _history.Entries.RemoveAll(entry => entry == null || entry.Result == null);
    }

    private void Store(QuestValidationResult record)
    {
        record.Sequence = _history.Entries.Count == 0 ? 1 : _history.Entries[0].Sequence + 1;
        record.Time = DateTime.Now.ToString("HH:mm:ss.fff");
        _history.Entries.Insert(0, record);
        if (_history.Entries.Count > HistoryLimit) { _history.Entries.RemoveRange(HistoryLimit, _history.Entries.Count - HistoryLimit); }
        SessionState.SetString(_historyKey, JsonUtility.ToJson(_history));
    }

    internal void ClearHistory()
    {
        _history.Entries.Clear();
        SessionState.EraseString(_historyKey);
    }

    internal void RecordSetup(QuestTester tester, string action, bool success, string detail)
    {
        Store(new QuestValidationResult
        {
            Action = action,
            QuestId = tester.QuestId,
            PlayerName = tester.TargetPlayer == null ? "현재 로컬 캐릭터" : tester.TargetPlayer.name,
            Result = new Outcome(RequestState.NotRequested, success ? CheckState.NotChecked : CheckState.Blocked,
                success ? "설정에 반영했습니다. 게임 요청은 실행하지 않았습니다." : "설정을 적용하지 못했습니다.", detail)
        });
    }


    /// <summary>
    /// 데이터 준비·현재 캐릭터·저장 슬롯과 오프라인 실행 조건 확인
    /// </summary>
    private static bool TryContext(QuestTester tester, out Context context, out string error, bool requireQuest = true)
    {
        context = null;
        error = string.Empty;
        if (!Application.isPlaying)
        {
            error = "Play Mode에서 실행하세요.";
            return false;
        }
        GameManager manager = UnityEngine.Object.FindAnyObjectByType<GameManager>();
        if (manager == null || !manager.DataManager.IsDataLoaded)
        {
            error = "GameManager와 게임 데이터 준비가 필요합니다.";
            return false;
        }
        Player active = manager.PlaySessionController.ActivePlayer;
        Player player = tester.TargetPlayer != null ? tester.TargetPlayer : active;
        if (player == null || player != active || !player.CanCaptureCharacterState ||
            !manager.CharacterSession.HasSelectedCharacter)
        {
            error = "현재 플레이 세션의 준비된 로컬 캐릭터를 지정하세요.";
            return false;
        }
        NetworkManager network = NetworkManager.Singleton;
        if (player.IsNetworkControlled || player.IsNetworkJoinLocked || network != null && network.IsListening)
        {
            error = "오프라인 캐릭터만 지원합니다. 협동 보스 퀘스트는 실제 서버 결과로 검증하세요.";
            return false;
        }
        bool hasQuest = manager.DataManager.QuestTable.TryGetValue(tester.QuestId, out QuestData quest);
        if (requireQuest && !hasQuest)
        {
            error = "목록에서 검증할 퀘스트를 선택하세요.";
            return false;
        }
        if (!player.TryGetComponent(out PlayerSaveController save) || !save.IsInitialized)
        {
            error = "플레이어의 저장 컴포넌트 초기화가 필요합니다.";
            return false;
        }
        if (save.SlotIdForTesting != manager.CharacterSession.SelectedSlotId)
        {
            error = "선택된 슬롯과 현재 캐릭터의 저장 슬롯이 다릅니다. 캐릭터 전환 완료 후 실행하세요.";
            return false;
        }
        context = new Context
        {
            Manager = manager,
            Player = player,
            Save = save,
            Quest = quest,
            Slot = save.SlotIdForTesting
        };
        return true;
    }

    internal sealed class QuestEntry
    {
        internal int Id;
        internal string Name;
        internal string Summary;
        internal bool Claimed;
    }

    /// <summary>
    /// 선택 전에도 현재 캐릭터의 수락·수령 완료 기록 조회
    /// </summary>
    internal bool TryGetQuestEntries(QuestTester tester, out List<QuestEntry> entries, out string error)
    {
        entries = new List<QuestEntry>();
        if (!TryContext(tester, out Context context, out error, false)) { return false; }
        foreach (QuestSaveData record in context.Player.QuestProgress.Capture())
        {
            bool defined = context.Manager.DataManager.QuestTable.TryGetValue(record.QuestId, out QuestData quest);
            entries.Add(new QuestEntry
            {
                Id = record.QuestId,
                Name = defined ? quest.Name : "정의 없음 · 저장 기록 유지",
                Claimed = record.RewardClaimed,
                Summary = record.RewardClaimed ? "보상 수령 완료 · 추가 지급 없음"
                    : defined ? DescribeQuest(context, quest) : $"처치 진행 {record.ProgressCount} · 보상 미수령"
            });
        }
        return true;
    }

    private static List<ItemRequirementProgress> ReadItemProgress(Context context, QuestData quest, out string error)
    {
        var rows = new List<ItemRequirementProgress>();
        var ids = new HashSet<int>();
        var errors = new StringBuilder();
        foreach (ItemRequirement required in quest.GetRequiredItems())
        {
            if (required == null) { errors.AppendLine("비어 있는 수집 목표가 있습니다."); continue; }
            bool validItem = context.Manager.DataManager.ItemTable.TryGetValue(required.ItemId, out ItemData item);
            if (required.ItemId <= 0 || required.Count <= 0 || !ids.Add(required.ItemId) || !validItem || item.GetItemType() == GameItemType.Equipment)
            {
                errors.AppendLine($"수집 목표 {required.ItemId}: ID·양수 수량·비장비 여부·중복 ID 확인 필요");
            }
            rows.Add(new ItemRequirementProgress(required.ItemId, validItem ? item.Name : "정의 없음",
                validItem ? context.Player.PlayerInventory.CountItems(item) : 0, required.Count));
        }
        error = errors.ToString();
        return rows;
    }

    internal bool TryGetItemProgress(QuestTester tester, out List<ItemRequirementProgress> rows, out string error)
    {
        rows = new List<ItemRequirementProgress>();
        if (!TryContext(tester, out Context context, out error)) { return false; }
        if (context.Quest.ObjectiveType != QuestObjectiveType.CollectItem) { return false; }
        rows = ReadItemProgress(context, context.Quest, out error);
        return true;
    }

    private static string DescribeQuest(Context context, QuestData quest)
    {
        bool accepted = context.Player.QuestProgress.TryGetProgress(quest.ID, out int progress, out bool claimed);
        string target;
        bool complete;
        string definitionError = string.Empty;
        if (quest.ObjectiveType == QuestObjectiveType.CollectItem)
        {
            List<ItemRequirementProgress> rows = ReadItemProgress(context, quest, out definitionError);
            var text = new StringBuilder();
            complete = rows.Count > 0 && definitionError.Length == 0;
            foreach (ItemRequirementProgress row in rows)
            {
                text.AppendLine($"{row.ItemId} · {row.Name}: {row.Current}/{row.Required} · " +
                    (row.Satisfied ? "충족" : $"{row.Missing}개 부족"));
                complete &= row.Satisfied;
            }
            target = text.ToString().TrimEnd();
        }
        else
        {
            string name = context.Manager.DataManager.MonsterTable.TryGetValue(quest.TargetID, out MonsterData monster)
                ? monster.Name : "정의 없음";
            target = $"{quest.TargetID} · {name}: {progress}/{quest.TargetCount} · 추가 필요 {Math.Max(0L, (long)quest.TargetCount - progress)}마리";
            complete = quest.TargetCount > 0 && progress >= quest.TargetCount;
        }
        string state = claimed ? "수령 완료 · 추가 지급 없음" : !accepted ? "미수락"
            : definitionError.Length > 0 ? "목표 정의 오류" : complete ? "모든 목표 충족 · 보상 공간은 실제 요청 시 판정" : "진행 중 · 목표 미달";
        return $"현재 상태: {state}\n{target}" + (definitionError.Length == 0 ? string.Empty : "\n" + definitionError);
    }

    internal bool TryDescribe(QuestTester tester, out string description)
    {
        if (!TryContext(tester, out Context context, out description)) { return false; }
        QuestData quest = context.Quest;
        bool accepted = context.Player.QuestProgress.TryGetProgress(quest.ID, out _, out _);
        bool prerequisiteMet = quest.RequiredQuestID <= 0 ||
            context.Player.QuestProgress.TryGetProgress(quest.RequiredQuestID, out _, out bool claimed) && claimed;
        string prerequisite = "없음";
        if (quest.RequiredQuestID > 0)
        {
            string name = context.Manager.DataManager.QuestTable.TryGetValue(quest.RequiredQuestID, out QuestData required)
                ? required.Name : "정의 없음";
            prerequisite = $"{quest.RequiredQuestID} · {name} / 보상 {(prerequisiteMet ? "수령" : "미수령")}";
        }
        string rewardName = context.Manager.DataManager.ItemTable.TryGetValue(quest.RewardItemID, out ItemData reward)
            ? reward.Name : "정의 없음";
        description = $"캐릭터: {context.Player.name} / 저장 슬롯: {context.Slot} (0부터 시작)\n" +
            $"퀘스트: {quest.ID} · {quest.Name}\n{DescribeQuest(context, quest)}\n" +
            $"실행 조건: {(context.Player.IsDead ? "캐릭터 사망 · 부활 필요" : "실행 가능")}\n" +
            $"수락 조건: {(accepted ? "이미 수락" : prerequisiteMet ? "선행 조건 충족" : "선행 보상 수령 필요")}\n" +
            $"선행 퀘스트: {prerequisite}\n보상: 골드 {quest.RewardGold}, 경험치 {quest.RewardExp}, {rewardName} × {quest.RewardItemCount}";
        return true;
    }

    /// <summary>
    /// 클릭마다 번호·대상·요청 결과·검사 판정·전후 값을 독립 기록
    /// </summary>
    internal void Execute(QuestTester tester, Command command, string label)
    {
        var record = new QuestValidationResult
        {
            Action = label,
            QuestId = tester.QuestId,
            PlayerName = tester.TargetPlayer == null ? "현재 로컬 캐릭터" : tester.TargetPlayer.name
        };
        try
        {
            if (!TryContext(tester, out Context context, out string error))
            {
                record.Result = Outcome.Blocked(error);
            }
            else
            {
                record.PlayerName = context.Player.name;
                record.Slot = context.Slot;
                record.QuestName = context.Quest.Name;
                QuestValidationSnapshot before = context.Capture();
                try
                {
                    record.Result = Run(tester, context, command);
                }
                finally
                {
                    record.Changes = before.CompareRows(context.Capture(), context.Quest, context.Manager.DataManager.ItemTable);
                }
            }
        }
        catch (Exception exception)
        {
            Debug.LogException(exception, tester);
            record.Result = new Outcome(RequestState.Error, CheckState.Error,
                "예외가 발생했습니다. 적용 범위를 전후 표에서 확인하세요.", exception.Message);
        }
        Store(record);
    }

    private Outcome Run(QuestTester tester, Context context, Command command)
    {
        QuestValidationSnapshot before = context.Capture();
        string error;
        bool result;
        switch (command)
        {
            case Command.ResetSelectedQuest:
            case Command.ResetAllQuests:
                return ResetQuests(context, command);
            case Command.Accept:
                return RunQuestRequest(context, false, before);
            case Command.KillMonster:
                return KillMonster(tester, context);
            case Command.SimulateMonsterKills:
                return SimulateMonsterKills(tester, context);
            case Command.GiveItem:
                return GiveItem(tester, context);
            case Command.Claim:
                return RunQuestRequest(context, true, before);
            case Command.ExpectClaimSuccess:
                return ExpectClaim(context, true);
            case Command.ExpectClaimFailure:
                return ExpectClaim(context, false);
            case Command.RepeatAccept:
                return Repeat(context, tester.RepeatCount, false);
            case Command.RepeatClaim:
                return Repeat(context, tester.RepeatCount, true);
            case Command.AcceptWithSaveFailure:
                return InjectFailure(context, false);
            case Command.ClaimWithSaveFailure:
                return InjectFailure(context, true);
            case Command.SaveFailureAndRetry:
                return VerifySaveRetry(context);
            case Command.RetrySave:
                result = context.Player.TrySave(out error);
                return Outcome.Operation(result, error, !before.Matches(context.Capture()));
            case Command.CompareFile:
                return CompareFile(context);
            case Command.CaptureBaseline:
                _baseline = before;
                _baselinePlayer = context.Player;
                _baselineSlot = context.Slot;
                return new Outcome(RequestState.NotRequested, CheckState.NotChecked, "비교 기준을 캡처했습니다.",
                    "게임 데이터를 바꾸지 않았습니다. 검사기를 다시 만들면 기준을 다시 캡처하세요.");
            case Command.CompareBaseline:
                return _baseline != null && _baselinePlayer == context.Player && _baselineSlot == context.Slot
                    ? new Outcome(RequestState.NotRequested, CheckState.NotChecked, "기준 상태와 현재 상태를 비교했습니다.",
                        _baseline.Difference(before, context.Quest.ID))
                    : Outcome.Pending("같은 캐릭터와 슬롯에서 기준을 먼저 캡처하세요.");
            default:
                throw new ArgumentOutOfRangeException(nameof(command));
        }
    }

    private static Outcome RunQuestRequest(Context context, bool claim, QuestValidationSnapshot before)
    {
        QuestRequestResult request = claim ? context.Player.ClaimQuestReward(context.Quest.ID)
            : context.Player.AcceptQuest(context.Quest.ID);
        Outcome outcome = Outcome.Operation(request.Succeeded, request.Message, !before.Matches(context.Capture()));
        outcome.Summary = "일반 요청입니다. 별도 성공·실패 검사 버튼에서 판정할 수 있습니다.";
        return QuestValidationFeedback.Apply(outcome, request, context.Quest, context.Manager.DataManager, claim);
    }

    /// <summary>
    /// 초기화 전 저장 대상·유지할 보상과 후속 퀘스트 기록 안내
    /// </summary>
    internal bool TryDescribeReset(QuestTester tester, Command command, out string description)
    {
        if (!TryContext(tester, out Context context, out description)) { return false; }
        description = DescribeReset(context, command);
        return true;
    }

    private static string DescribeReset(Context context, Command command)
    {
        List<QuestSaveData> records = context.Player.QuestProgress.Capture();
        string target = command == Command.ResetAllQuests
            ? $"모든 퀘스트 기록 {records.Count}개"
            : $"선택 퀘스트 {context.Quest.ID} · {context.Quest.Name}";
        string description = $"캐릭터: {context.Player.name} / 저장 슬롯: {context.Slot} (0부터 시작)\n" +
            $"초기화 대상: {target}\n수락·처치 진행·보상 수령 기록을 제거하고 현재 캐릭터를 저장합니다.\n" +
            "이미 지급된 골드·경험치·아이템과 보유 수집품은 유지됩니다.\n" +
            "수집품을 보유하면 재수락 직후 목표를 충족할 수 있습니다.";
        if (command == Command.ResetAllQuests) { return description; }

        // 순환된 정의에서도 종료하는 간접 후속 퀘스트 탐색
        var descendants = new HashSet<int> { context.Quest.ID };
        var pending = new Queue<int>();
        pending.Enqueue(context.Quest.ID);
        while (pending.Count > 0)
        {
            int requiredId = pending.Dequeue();
            foreach (QuestData quest in context.Manager.DataManager.QuestTable.Values)
            {
                if (quest.RequiredQuestID == requiredId && descendants.Add(quest.ID))
                {
                    pending.Enqueue(quest.ID);
                }
            }
        }
        var retained = new List<string>();
        foreach (QuestSaveData record in records)
        {
            if (record.QuestId != context.Quest.ID && descendants.Contains(record.QuestId))
            {
                retained.Add($"{record.QuestId} (진행 {record.ProgressCount}, 수령 {record.RewardClaimed})");
            }
        }
        return description + "\n유지되는 후속 퀘스트 기록: " +
            (retained.Count == 0 ? "없음" : string.Join(", ", retained)) +
            "\n후속 기록까지 처음부터 검증하려면 모든 퀘스트 초기화를 사용하세요.";
    }

    /// <summary>
    /// 퀘스트 기록만 제거한 기대 상태와 실제 메모리·본 저장 파일 검증
    /// </summary>
    private static Outcome ResetQuests(Context context, Command command)
    {
        string scope = DescribeReset(context, command);
        QuestValidationSnapshot before = context.Capture();
        SaveData expectedData = context.Player.CaptureCharacterState();
        int originalCount = expectedData.Quests.Count;
        if (command == Command.ResetAllQuests) { expectedData.Quests.Clear(); }
        else { expectedData.Quests.RemoveAll(record => record.QuestId == context.Quest.ID); }
        int removedCount = originalCount - expectedData.Quests.Count;
        var expected = new QuestValidationSnapshot(expectedData);
        if (removedCount > 0) { context.Player.QuestProgress.Restore(expectedData.Quests); }
        bool memoryMatches = expected.Matches(context.Capture());
        if (!memoryMatches)
        {
            return Outcome.Check(false, Request(false, before, context.Capture()),
                "초기화 후 메모리가 기대 상태와 다릅니다. 추가 저장을 실행하지 않았습니다.",
                scope + "\n퀘스트 기록 제거 외 보상 데이터 불변 기대 True / 실제 False\n" +
                expected.Difference(context.Capture(), context.Quest.ID));
        }

        // 이미 비어 있는 경우에도 저장 재시도가 가능하도록 저장 실행
        bool saved = context.Player.TrySave(out string error);
        QuestValidationSnapshot after = context.Capture();
        memoryMatches = expected.Matches(after);
        string evidence = scope + $"\n제거한 퀘스트 기록: {removedCount}개\n" +
            $"메모리 기대 상태 일치 True / 실제 {memoryMatches}\n저장 성공 기대 True / 실제 {saved}";
        if (!saved)
        {
            return Outcome.Check(false, Request(false, before, after),
                "메모리 초기화 완료 / 저장 실패. 현재 상태 저장만 재시도로 저장하세요.",
                evidence + "\n" + error + "\n저장 성공 전에는 재접속 후 초기화 유지를 보장하지 않습니다.");
        }
        Outcome file = CompareFile(context);
        return new Outcome(Request(true, before, after),
            !memoryMatches ? CheckState.Failed : file.Verification,
            !memoryMatches ? "저장 후 메모리가 기대 상태와 달라졌습니다. 전후 값을 확인하세요."
                : file.Verification == CheckState.Passed ? "퀘스트 초기화·보상 데이터 유지·본 파일 저장을 확인했습니다."
                : "초기화 저장 요청은 성공했지만 본 파일 일치 검증이 완료되지 않았습니다.",
            evidence + "\n" + file.Summary + "\n" + file.Detail);
    }

    private static RequestState Request(bool success, QuestValidationSnapshot before, QuestValidationSnapshot after) =>
        success ? (before.Matches(after) ? RequestState.Unchanged : RequestState.Applied)
            : (before.Matches(after) ? RequestState.Rejected : RequestState.PartiallyApplied);

    private static Outcome GiveItem(QuestTester tester, Context context)
    {
        if (context.Player.IsDead || tester.ItemCount <= 0 ||
            !context.Manager.DataManager.ItemTable.TryGetValue(tester.ItemId, out ItemData item))
        {
            return Outcome.Blocked("살아 있는 캐릭터와 유효한 아이템 ID·양수 수량이 필요합니다.");
        }
        GameItemType type = item.GetItemType();
        if (type == GameItemType.Equipment && tester.ItemCount != 1)
        {
            return Outcome.Blocked("장비는 한 번에 1개씩 지급하세요.");
        }
        if (type != GameItemType.Equipment && type != GameItemType.Consumable && type != GameItemType.Etc)
        {
            return Outcome.Blocked("인벤토리 아이템만 지급할 수 있습니다.");
        }
        foreach (InventoryTab tab in Enum.GetValues(typeof(InventoryTab)))
        {
            if (tab == InventoryTab.None) { continue; }
            InventoryItemData[] items = context.Player.PlayerInventory.GetListByTab(tab);
            int slots = context.Player.PlayerInventory.GetCurrentSlotCount(tab);
            for (int i = 0; i < slots; i++)
            {
                InventoryItemData slot = items[i];
                if (slot != null && !slot.IsEmpty && slot.ItemData.ID == item.ID &&
                    (long)slot.Count + tester.ItemCount > int.MaxValue)
                {
                    return Outcome.Blocked("기존 중첩 수량의 정수 범위를 초과합니다.");
                }
            }
        }
        QuestValidationSnapshot before = context.Capture();
        InventoryResult result = context.Player.PlayerInventory.AddItem(item, tester.ItemCount);
        QuestValidationSnapshot after = context.Capture();
        long actual = after.ItemCount(item.ID) - before.ItemCount(item.ID);
        bool success = result == InventoryResult.Success;
        bool passed = success && actual == tester.ItemCount;
        return Outcome.Check(passed, Request(success, before, after),
            success ? "아이템 지급 결과를 실제 수량으로 확인했습니다." : "아이템 지급에 실패했습니다. 공간과 수량을 확인하세요.",
            $"아이템 {item.ID} · {item.Name}\nAPI: {result}\n기대 증가: {tester.ItemCount}, 실제 증가: {actual}\n" +
            "이 검사는 인벤토리 추가 경로이며 필드 드롭·줍기 검사는 별도입니다.");
    }

    /// <summary>
    /// 실제 몬스터 없이 기존 처치 집계 경로 실행과 전체 캐릭터 기대 상태 비교
    /// 피해·사망 이벤트·경험치·드롭·파일 저장 제외
    /// </summary>
    private static Outcome SimulateMonsterKills(QuestTester tester, Context context)
    {
        if (context.Quest.ObjectiveType != QuestObjectiveType.KillMonster)
        {
            return Outcome.Blocked("처치형 퀘스트를 선택하세요. 수집형 퀘스트에는 처치 집계를 적용하지 않습니다.");
        }
        if (context.Player.IsDead) { return Outcome.Blocked("부활 후 처치 집계 모의 실행을 사용하세요."); }
        if (tester.SimulatedKillCount < 1 || tester.SimulatedKillCount > QuestTester.MaximumSimulatedKillsPerRequest)
        {
            return Outcome.Blocked($"모의 처치 횟수는 1~{QuestTester.MaximumSimulatedKillsPerRequest}회입니다.");
        }
        if (!context.Manager.DataManager.MonsterTable.TryGetValue(context.Quest.TargetID, out MonsterData monster))
        {
            return Outcome.Blocked($"몬스터 ID {context.Quest.TargetID}의 데이터 정의가 없습니다. 실제 씬 오브젝트는 필요하지 않습니다.");
        }

        QuestValidationSnapshot before = context.Capture();
        SaveData expectedData = context.Player.CaptureCharacterState();
        List<QuestSaveData> originalRecords = context.Player.QuestProgress.Capture();
        int expectedChanges = 0;
        foreach (QuestSaveData record in expectedData.Quests)
        {
            if (record.RewardClaimed || !context.Manager.DataManager.QuestTable.TryGetValue(record.QuestId, out QuestData quest) ||
                quest.ObjectiveType != QuestObjectiveType.KillMonster || quest.TargetID != monster.ID || record.ProgressCount >= quest.TargetCount)
            {
                continue;
            }
            // 비교용 독립 복사본에만 기대 진행도 계산
            record.ProgressCount = (int)Math.Min(quest.TargetCount, (long)record.ProgressCount + tester.SimulatedKillCount);
            expectedChanges++;
        }
        var expected = new QuestValidationSnapshot(expectedData);
        for (int i = 0; i < tester.SimulatedKillCount; i++)
        {
            context.Player.QuestProgress.RecordMonsterKill(monster.ID, context.Manager.DataManager.QuestTable);
        }
        QuestValidationSnapshot after = context.Capture();
        bool passed = expected.Matches(after);
        var evidence = new StringBuilder();
        evidence.AppendLine($"모의 처치 대상: {monster.ID} · {monster.Name}");
        evidence.AppendLine($"요청 횟수: {tester.SimulatedKillCount}회 / 기대 진행 변경 퀘스트: {expectedChanges}개");
        evidence.AppendLine("미수락·보상 기수령·목표 상한·다른 몬스터 목표는 변경 대상에서 제외합니다.");
        foreach (QuestSaveData record in originalRecords)
        {
            if (!context.Manager.DataManager.QuestTable.TryGetValue(record.QuestId, out QuestData quest) ||
                quest.ObjectiveType != QuestObjectiveType.KillMonster || quest.TargetID != monster.ID)
            {
                continue;
            }
            context.Player.QuestProgress.TryGetProgress(record.QuestId, out int actual, out _);
            int target = expectedData.Quests.Find(item => item.QuestId == record.QuestId).ProgressCount;
            evidence.AppendLine($"{quest.ID} · {quest.Name}: {record.ProgressCount} → {actual} / 기대 {target} / 목표 {quest.TargetCount}" +
                (record.RewardClaimed ? " · 보상 기수령, 변경 제외" : string.Empty));
        }
        evidence.AppendLine($"집계 이후 기대 캐릭터 상태 일치 True / 실제 {passed}");
        evidence.AppendLine("기대 상태에는 골드·경험치·아이템·장비·수령 기록 유지가 포함됩니다. 자연 회복 HP/MP는 비교에서 제외합니다.");
        evidence.AppendLine("실제 몬스터·피해·사망 이벤트·드롭·전투 검증은 실행하지 않았습니다. 자동 저장하지 않습니다.");
        Outcome outcome = Outcome.Check(passed, Request(true, before, after),
            "처치 집계 모의 실행 결과를 검사했습니다. 실제 전투·사망 경로의 통과를 의미하지 않습니다.", evidence.ToString());
        outcome.RequestSummary = !passed ? "모의 집계 실행 · 기대 상태 불일치"
            : expectedChanges == 0 ? "모의 집계 실행 · 진행 변경 대상 없음" : "모의 집계 실행 · 퀘스트 진행 반영";
        outcome.RequestDetail = $"대상 몬스터: {monster.ID} · {monster.Name} / 모의 처치 {tester.SimulatedKillCount}회\n" +
            $"동일 몬스터 목표의 수락 퀘스트 중 {expectedChanges}개에 진행 증가를 기대합니다. 실제 결과는 전후 표와 검사 판정을 확인하세요.";
        outcome.NextAction = !passed ? "전후 표와 전체 검사 근거에서 예상과 다른 진행도 또는 보상 변경을 확인하세요."
            : expectedChanges == 0 ? "선택 퀘스트의 수락·수령 여부와 진행 상한을 확인하세요. 필요하면 초기화 후 다시 수락하세요."
            : "집계 검사는 완료했습니다. 실제 전투 연결은 기존 지정 몬스터 실제 처치 시도 버튼으로 별도 확인하세요.";
        return outcome;
    }

    private static Outcome KillMonster(QuestTester tester, Context context)
    {
        MonsterBase monster = tester.TargetMonster;
        if (monster == null || !monster.gameObject.activeInHierarchy || !monster.CanDealDamage ||
            monster.Data == null || context.Player.IsDead ||
            monster.gameObject.scene != context.Player.gameObject.scene &&
            (MapManager.Instance == null || monster.gameObject.scene != MapManager.Instance.gameObject.scene))
        {
            return Outcome.Blocked("현재 맵의 살아 있는 판정 권한 몬스터를 지정하세요.");
        }
        float hpBefore = monster.CurrentHp;
        double damage = (double)hpBefore * tester.DamageMultiplier;
        if (double.IsNaN(damage) || double.IsInfinity(damage) ||
            tester.DamageMultiplier < 1f || damage > float.MaxValue)
        {
            return Outcome.Blocked("피해 배율을 확인하세요.");
        }
        QuestValidationSnapshot before = context.Capture();
        bool accepted = context.Player.QuestProgress.TryGetProgress(context.Quest.ID, out _, out bool claimed);
        bool shouldAdvance = accepted && !claimed && context.Quest.ObjectiveType == QuestObjectiveType.KillMonster &&
            context.Quest.TargetID == monster.Data.ID && before.ProgressFor(context.Quest) < context.Quest.TargetCount;
        int events = 0;
        void OnKilled(MonsterBase killed) { events++; }
        monster.OnKilledEvent += OnKilled;
        try
        {
            monster.TakeDamage((float)damage, context.Player.transform.position, context.Player);
        }
        finally
        {
            monster.OnKilledEvent -= OnKilled;
        }
        QuestValidationSnapshot after = context.Capture();
        long expected = before.ProgressFor(context.Quest) + (shouldAdvance ? 1 : 0);
        long actual = after.ProgressFor(context.Quest);
        string evidence = $"몬스터 {monster.Data.ID}\nHP: {hpBefore} → {monster.CurrentHp}\n" +
            $"사망 이벤트 기대 1회 / 실제 {events}회\n퀘스트 진행 기대 {expected} / 실제 {actual}\n" +
            (shouldAdvance ? "수락한 처치 목표의 집계를 검사했습니다." : "미수락·기수령·목표 불일치·상한 상태는 진행 증가 대상이 아닙니다.");
        if (events == 0 && monster.CurrentHp > 0f)
        {
            return new Outcome(RequestState.Observed, CheckState.Inconclusive, "피해는 요청했지만 처치되지 않았습니다.",
                evidence + "\n상태 효과와 피해 배율을 확인하세요.");
        }
        return Outcome.Check(events == 1 && monster.CurrentHp <= 0f && expected == actual,
            events == 1 && monster.CurrentHp <= 0f ? RequestState.Applied : RequestState.Observed,
            "실제 처치와 퀘스트 집계 결과를 검사했습니다.", evidence);
    }

    private static Outcome ExpectClaim(Context context, bool expectedSuccess)
    {
        QuestValidationSnapshot before = context.Capture();
        bool wasClaimed = context.Player.QuestProgress.TryGetProgress(context.Quest.ID, out _, out bool claimed) && claimed;
        QuestRequestResult request = context.Player.ClaimQuestReward(context.Quest.ID);
        bool result = request.Succeeded;
        string error = request.Succeeded ? string.Empty : request.Message;
        QuestValidationSnapshot after = context.Capture();
        bool nowClaimed = context.Player.QuestProgress.TryGetProgress(context.Quest.ID, out _, out claimed) && claimed;
        bool amounts = wasClaimed ? before.Matches(after) : before.HasExpectedRewardAmounts(after, context.Quest);
        bool passed = expectedSuccess ? result && nowClaimed && amounts : !result && before.Matches(after);
        string summary = expectedSuccess ? "보상 성공·수령 기록·금액/수량을 검사했습니다."
            : "의도한 거절과 데이터 불변을 검사했습니다. 검사 통과는 보상 지급 성공을 뜻하지 않습니다.";
        string evidence = expectedSuccess
            ? $"API 기대 성공 / 실제 {(result ? "성공" : "거절")}\n수령 기록 기대 True / 실제 {nowClaimed}\n" +
                $"{(wasClaimed ? "재지급 없음" : "금액·수량 일치")} 기대 True / 실제 {amounts}"
            : $"API 기대 거절 / 실제 {(result ? "성공" : "거절")}\n데이터 불변 기대 True / 실제 {before.Matches(after)}";
        if (wasClaimed)
        {
            evidence += expectedSuccess ? "\n기수령 상태이므로 최초 지급 대신 재지급 없음을 검사합니다."
                : "\n기수령 요청은 추가 지급 없는 성공이므로 실패 조건이 아닙니다. 자동 저장하지 않습니다.";
        }
        Outcome outcome = Outcome.Check(passed, Request(result, before, after), summary, evidence + "\n" + error);
        return QuestValidationFeedback.Apply(outcome, request, context.Quest, context.Manager.DataManager, true);
    }

    private static Outcome Repeat(Context context, int count, bool claim)
    {
        if (count < 2 || count > 20) { return Outcome.Blocked("반복 횟수는 2~20회입니다."); }
        QuestValidationSnapshot before = context.Capture();
        QuestRequestResult request = claim ? context.Player.ClaimQuestReward(context.Quest.ID)
            : context.Player.AcceptQuest(context.Quest.ID);
        bool first = request.Succeeded;
        if (!first)
        {
            return QuestValidationFeedback.Apply(new Outcome(Request(first, before, context.Capture()), CheckState.Inconclusive,
                "첫 요청이 실패해 반복 검사를 진행하지 않았습니다.", request.Message),
                request, context.Quest, context.Manager.DataManager, claim);
        }
        QuestValidationSnapshot once = context.Capture();
        for (int i = 1; i < count; i++)
        {
            request = claim ? context.Player.ClaimQuestReward(context.Quest.ID) : context.Player.AcceptQuest(context.Quest.ID);
            bool result = request.Succeeded;
            if (!result || !once.Matches(context.Capture()))
            {
                return QuestValidationFeedback.Apply(Outcome.Check(false, Request(result, before, context.Capture()),
                    $"{i + 1}번째 반복 요청에서 실패 또는 추가 변경이 발생했습니다.", request.Message),
                    request, context.Quest, context.Manager.DataManager, claim);
            }
        }
        return Outcome.Check(true, Request(true, before, context.Capture()), "순차 중복 요청 검사를 통과했습니다.",
            $"실행 {count}회 / 첫 성공 이후 추가 변경 0회\n동시 요청·이벤트 재진입 검사는 별도입니다.");
    }

    /// <summary>
    /// 수락·보상 처리와 명시적 저장을 분리한 일회성 쓰기 실패 검사
    /// </summary>
    private static Outcome InjectFailure(Context context, bool claim)
    {
        QuestValidationSnapshot before = context.Capture();
        SaveFileStorage storage = context.Storage;
        SaveFileStorage.ReadResult oldResult = storage.Read(out string oldJson, out string readError);
        if (oldResult == SaveFileStorage.ReadResult.Failed) { return Outcome.Pending(readError); }
        QuestRequestResult request = claim ? context.Player.ClaimQuestReward(context.Quest.ID) : context.Player.AcceptQuest(context.Quest.ID);
        bool requested = request.Succeeded;
        QuestValidationSnapshot applied = context.Capture();
        if (!requested)
        {
            return QuestValidationFeedback.Apply(new Outcome(Request(false, before, applied), CheckState.Inconclusive,
                "게임 요청이 거절되어 저장 실패 검사를 진행하지 않았습니다.", request.Message),
                request, context.Quest, context.Manager.DataManager, claim);
        }
        SaveFileStorage.ReadResult appliedResult = storage.Read(out string appliedJson, out readError);
        bool noAutomaticSave = oldResult == appliedResult && oldJson == appliedJson;
        int failures = context.Save.SimulatedWriteFailureCount;
        bool saved;
        string error;
        using (context.Save.FailNextWriteForTesting())
        {
            saved = context.Player.TrySave(out error);
        }
        int consumed = context.Save.SimulatedWriteFailureCount - failures;
        SaveFileStorage.ReadResult failedResult = storage.Read(out string failedJson, out readError);
        bool fileUnchanged = appliedResult == failedResult && appliedJson == failedJson;
        QuestValidationSnapshot after = context.Capture();
        bool memoryUnchanged = applied.Matches(after);
        bool passed = !saved && consumed == 1 && noAutomaticSave && fileUnchanged && memoryUnchanged;
        return new Outcome(Request(saved, before, after),
            consumed == 0 ? CheckState.Inconclusive : passed ? CheckState.Passed : CheckState.Failed,
            "게임 요청 후 별도 저장의 실패와 메모리·파일 상태를 검사했습니다.",
            $"게임 요청 성공 기대 True / 실제 {requested}\n자동 저장 없음 기대 True / 실제 {noAutomaticSave}\n" +
            $"저장 실패 주입 기대 1회 / 실제 {consumed}회\n저장 거절 기대 True / 실제 {!saved}\n" +
            $"실패 시 본 파일 불변 기대 True / 실제 {fileUnchanged}\n메모리 유지 기대 True / 실제 {memoryUnchanged}\n{error}\n" +
            "이 검사는 도구가 별도 저장을 요청합니다. 복구는 현재 상태 저장만 재시도로 실행하세요.");
    }

    /// <summary>
    /// 보상 지급 이후 명시적 저장 실패와 저장만 재시도의 중복 지급 검사
    /// </summary>
    private static Outcome VerifySaveRetry(Context context)
    {
        if (!context.Player.QuestProgress.TryGetProgress(context.Quest.ID, out _, out bool claimed) || claimed)
        {
            return Outcome.Pending("수락했지만 아직 보상을 받지 않은 퀘스트가 필요합니다.");
        }
        QuestValidationSnapshot before = context.Capture();
        Outcome failure = InjectFailure(context, true);
        if (failure.Verification != CheckState.Passed) { return failure; }
        QuestValidationSnapshot paid = context.Capture();
        bool saved = context.Player.TrySave(out string error);
        QuestValidationSnapshot after = context.Capture();
        bool unchanged = paid.Matches(after);
        context.Player.QuestProgress.TryGetProgress(context.Quest.ID, out _, out claimed);
        Outcome comparison = CompareFile(context);
        bool amounts = before.HasExpectedRewardAmounts(paid, context.Quest);
        bool passed = saved && claimed && unchanged && amounts && comparison.Verification == CheckState.Passed;
        return Outcome.Check(passed, Request(saved, before, after),
            "보상 처리 후 저장 실패 → 저장만 재시도 결과를 검사했습니다.",
            failure.Detail + $"\n최초 지급 금액/수량 기대 True / 실제 {amounts}\n" +
            $"저장 재시도 성공 기대 True / 실제 {saved}\n재지급 없음 기대 True / 실제 {unchanged}\n" +
            $"수령 기록 기대 True / 실제 {claimed}\n재시도: {error}\n{comparison.Summary}\n{comparison.Detail}");
    }

    private static Outcome CompareFile(Context context)
    {
        SaveFileStorage.ReadResult result = context.Storage.Read(out string json, out string error);
        if (result != SaveFileStorage.ReadResult.Success)
        {
            return Outcome.Pending($"본 파일을 읽지 못했습니다: {result} / {error}");
        }
        var serializer = new SaveDataSerializer();
        if (!serializer.TryDeserialize(json, out SaveData data, out error) ||
            !new SaveDataChecker().TryCheck(data, context.Player.PlayerInventory.Settings,
                context.Manager.DataManager, out error))
        {
            return Outcome.Pending(error);
        }
        var saved = new QuestValidationSnapshot(data);
        QuestValidationSnapshot memory = context.Capture();
        return Outcome.Check(saved.Matches(memory), RequestState.NotRequested,
            "본 저장 파일과 메모리의 보상 관련 데이터를 비교했습니다.",
            "기대: 전체 동일 / 실제: " + (saved.Matches(memory) ? "동일" : "불일치") +
            "\n현재 HP·MP 제외\n" + saved.Difference(memory, context.Quest.ID));
    }
}
