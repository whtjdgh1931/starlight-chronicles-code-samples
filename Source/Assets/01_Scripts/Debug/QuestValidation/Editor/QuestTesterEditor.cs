using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Inspector 버튼과 상태·검사 결과 표시
/// </summary>
[CustomEditor(typeof(QuestTester))]
public sealed class QuestTesterEditor : Editor
{
    private QuestValidationRunner _runner;
    private int _historyIndex;
    private bool _showDetails;
    private bool _showSettings;
    private bool _showHistory;
    private bool _showUnchangedValues;
    private bool _showAcceptedQuests = true;
    private bool _showClaimedQuests = true;
    private int _collectionQuestId;
    private int _collectionTargetIndex;
    private const float StatusBarHeight = 4f;

    [MenuItem("Tools/Debug/Create Quest Tester")]
    private static void CreateTester()
    {
        var gameObject = new GameObject("QuestTester");
        gameObject.tag = "EditorOnly";
        Undo.RegisterCreatedObjectUndo(gameObject, "퀘스트 검증 도구 생성");
        Undo.AddComponent<QuestTester>(gameObject);
        Selection.activeGameObject = gameObject;
    }

    [MenuItem("Tools/Debug/Select Quest Testers")]
    private static void SelectTesters()
    {
        QuestTester[] testers = FindObjectsByType<QuestTester>(FindObjectsInactive.Include);
        var objects = new Object[testers.Length];
        for (int i = 0; i < testers.Length; i++) { objects[i] = testers[i].gameObject; }
        Selection.objects = objects;
    }

    private void OnEnable()
    {
        _runner = new QuestValidationRunner(target.GetEntityId());
    }

    public override bool RequiresConstantRepaint() => Application.isPlaying;

    public override void OnInspectorGUI()
    {
        var tester = (QuestTester)target;
        DrawQuestEntries(tester);
        serializedObject.Update();
        DrawQuestSelector();
        serializedObject.ApplyModifiedProperties();
        EditorGUILayout.LabelField("현재 선택한 퀘스트 · 실시간 상태", EditorStyles.boldLabel);
        bool ready = _runner.TryDescribe(tester, out string description);
        EditorGUILayout.HelpBox(description, ready ? MessageType.Info : MessageType.Warning);
        DrawCollectionProgress(tester);
        EditorGUILayout.Space();
        DrawResults(tester);
        _showSettings = EditorGUILayout.Foldout(_showSettings, "대상·아이템·반복 실행 설정", true);
        if (_showSettings)
        {
            serializedObject.Update();
            DrawPropertiesExcluding(serializedObject, "m_Script", "_questId", "_simulatedKillCount");
            serializedObject.ApplyModifiedProperties();
        }

        EditorGUILayout.HelpBox(
            "일반 수락·보상은 메모리에만 반영됩니다. 저장 검사·초기화 후 저장은 실제 파일을 변경합니다.\n" +
            "성공/실패 검사는 실제 요청입니다. 실패를 예상해도 조건이 충족되면 보상이 지급됩니다.\n" +
            "이 도구는 Play Mode의 오프라인 검증 전용입니다.", MessageType.Warning);

        using (new EditorGUI.DisabledScope(!Application.isPlaying))
        {
            if (GUILayout.Button("현재 로컬 캐릭터 연결")) { BindActivePlayer(); }
        }
        using (new EditorGUI.DisabledScope(!ready))
        {
            if (GUILayout.Button("퀘스트 목표 몬스터 찾기")) { FindMonster(tester); }
            if (GUILayout.Button("선택한 수집 아이템으로 지급 ID 설정")) { UseQuestItem(tester); }

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("실제 게임 요청", EditorStyles.boldLabel);
            Button(tester, "선택 퀘스트 수락", QuestValidationRunner.Command.Accept);
            Button(tester, "지정 몬스터 실제 처치 시도", QuestValidationRunner.Command.KillMonster);
            Button(tester, "지정 아이템 지급", QuestValidationRunner.Command.GiveItem);
            Button(tester, "퀘스트 보상 수령 요청", QuestValidationRunner.Command.Claim);

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("처치 집계 모의 실행 · 전투 경로 제외", EditorStyles.boldLabel);
            serializedObject.Update();
            EditorGUILayout.PropertyField(serializedObject.FindProperty("_simulatedKillCount"), new GUIContent("모의 처치 횟수"));
            serializedObject.ApplyModifiedProperties();
            EditorGUILayout.HelpBox("실제 몬스터 없이 퀘스트 처치 집계만 실행합니다. 같은 몬스터를 목표로 하는 다른 수락 퀘스트도 반영됩니다. " +
                "경험치·골드·드롭·사망 이벤트·자동 저장은 실행하지 않습니다.", MessageType.Info);
            Button(tester, "목표 몬스터 처치 집계 모의 실행", QuestValidationRunner.Command.SimulateMonsterKills);

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("결과 검사 · 현재 조건 사용", EditorStyles.boldLabel);
            Button(tester, "퀘스트 성공 검사 · 보상 수령", QuestValidationRunner.Command.ExpectClaimSuccess);
            Button(tester, "퀘스트 실패 검사 · 보상/진행 불변", QuestValidationRunner.Command.ExpectClaimFailure);
            Button(tester, "반복 수락 검사", QuestValidationRunner.Command.RepeatAccept);
            Button(tester, "반복 보상 수령 검사", QuestValidationRunner.Command.RepeatClaim);

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("저장 실패와 재시도", EditorStyles.boldLabel);
            Button(tester, "수락 후 별도 저장 · 쓰기 1회 실패", QuestValidationRunner.Command.AcceptWithSaveFailure);
            Button(tester, "보상 후 별도 저장 · 쓰기 1회 실패", QuestValidationRunner.Command.ClaimWithSaveFailure);
            Button(tester, "보상 후 저장 실패 → 저장만 재시도 검사", QuestValidationRunner.Command.SaveFailureAndRetry);
            Button(tester, "현재 상태 저장만 재시도", QuestValidationRunner.Command.RetrySave);
            Button(tester, "본 저장 파일과 현재 상태 비교", QuestValidationRunner.Command.CompareFile);

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("검증용 퀘스트 초기화", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("미수락 상태로 되돌리고 저장합니다. 이미 받은 보상·보유 수집품은 유지됩니다.", MessageType.Info);
            if (_runner.TryDescribeReset(tester, QuestValidationRunner.Command.ResetSelectedQuest, out string resetDescription))
            {
                EditorGUILayout.HelpBox(resetDescription, MessageType.None);
            }
            ResetButton(tester, "선택 퀘스트 초기화 후 저장", QuestValidationRunner.Command.ResetSelectedQuest);
            ResetButton(tester, "모든 퀘스트 초기화 후 저장", QuestValidationRunner.Command.ResetAllQuests);

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("수동 시나리오 비교", EditorStyles.boldLabel);
            Button(tester, "현재 상태를 기준으로 캡처", QuestValidationRunner.Command.CaptureBaseline);
            Button(tester, "기준 상태와 현재 상태 비교", QuestValidationRunner.Command.CompareBaseline);
        }

    }

    /// <summary>
    /// 최근 결과를 설정 위에 표시하고 요청·판정·값 변화를 분리
    /// </summary>
    private void DrawResults(QuestTester tester)
    {
        IReadOnlyList<QuestValidationResult> entries = _runner.History;
        EditorGUILayout.LabelField("실행 당시 결과 · 현재 상태와 별도 기록", EditorStyles.boldLabel);
        if (entries.Count == 0)
        {
            EditorGUILayout.HelpBox("실행 기록이 없습니다. 요청 버튼을 누르면 결과와 실패 이유가 이곳에 표시됩니다.", MessageType.Info);
            return;
        }
        _showHistory = EditorGUILayout.Foldout(_showHistory, "최근 20개 실행 이력 선택", true);
        if (_showHistory)
        {
            var names = new string[entries.Count];
            for (int i = 0; i < entries.Count; i++)
            {
                QuestValidationResult entry = entries[i];
                names[i] = $"#{entry.Sequence} {entry.Time} · {entry.Action} · {QuestValidationResult.CheckLabel(entry.Result.Verification)}";
            }
            _historyIndex = EditorGUILayout.Popup("기록 선택", Mathf.Clamp(_historyIndex, 0, entries.Count - 1), names);
        }
        _historyIndex = Mathf.Clamp(_historyIndex, 0, entries.Count - 1);
        QuestValidationResult record = entries[_historyIndex];
        QuestValidationResult.Outcome outcome = record.Result;
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        EditorGUILayout.LabelField($"{(_historyIndex == 0 ? "최신 실행" : "이전 실행")} #{record.Sequence} · {record.Time}", EditorStyles.boldLabel);
        EditorGUILayout.LabelField(record.Action, EditorStyles.wordWrappedLabel);
        string slot = record.Slot < 0 ? "해당 없음" : record.Slot.ToString();
        EditorGUILayout.LabelField($"실행 대상: {record.PlayerName} / 슬롯 {slot}\n퀘스트: {record.QuestId} · {record.QuestName}",
            EditorStyles.wordWrappedLabel);

        bool rejected = outcome.Request == QuestValidationResult.RequestState.Rejected ||
            outcome.Request == QuestValidationResult.RequestState.PartiallyApplied ||
            outcome.Request == QuestValidationResult.RequestState.Error;
        Color requestColor = rejected ? Color.red
            : outcome.Request == QuestValidationResult.RequestState.Applied || outcome.Request == QuestValidationResult.RequestState.Unchanged
                ? Color.green : Color.yellow;
        DrawStatus("요청 적용 결과", QuestValidationResult.RequestLabel(outcome.Request), requestColor);
        string headline = string.IsNullOrEmpty(outcome.RequestSummary) ? outcome.Summary : outcome.RequestSummary;
        EditorGUILayout.LabelField(headline, EditorStyles.wordWrappedLabel);
        if (!string.IsNullOrEmpty(outcome.RequestDetail))
        {
            EditorGUILayout.HelpBox(outcome.RequestDetail, rejected ? MessageType.Error : MessageType.Info);
        }
        else if ((rejected || outcome.Verification == QuestValidationResult.CheckState.Blocked) && !string.IsNullOrEmpty(outcome.Detail))
        {
            EditorGUILayout.HelpBox(outcome.Detail, MessageType.Warning);
        }
        if (!string.IsNullOrEmpty(outcome.NextAction))
        {
            EditorGUILayout.HelpBox("다음 조치: " + outcome.NextAction, MessageType.Info);
        }
        EditorGUILayout.Space();
        Color checkColor = outcome.Verification == QuestValidationResult.CheckState.Passed ? Color.green
            : outcome.Verification == QuestValidationResult.CheckState.Failed || outcome.Verification == QuestValidationResult.CheckState.Error
                ? Color.red : Color.yellow;
        DrawStatus("검사 판정", QuestValidationResult.CheckLabel(outcome.Verification), checkColor);
        if (!string.IsNullOrEmpty(outcome.RequestSummary))
        {
            EditorGUILayout.LabelField(outcome.Summary, EditorStyles.wordWrappedLabel);
        }
        if (outcome.Verification == QuestValidationResult.CheckState.Passed && rejected)
        {
            EditorGUILayout.HelpBox("요청은 거절됐습니다. 검사 통과는 의도한 거절과 데이터 불변을 확인했다는 뜻입니다.", MessageType.Info);
        }
        _showDetails = EditorGUILayout.Foldout(_showDetails, "검사 기대값·실제값과 전체 근거", true);
        if (_showDetails && !string.IsNullOrEmpty(outcome.Detail))
        {
            EditorGUILayout.HelpBox(outcome.Detail, MessageType.None);
        }
        EditorGUILayout.EndVertical();
        if (record.QuestId != tester.QuestId)
        {
            EditorGUILayout.HelpBox("현재 선택한 퀘스트와 다른 실행 기록입니다. 위 결과는 실행 당시 값입니다.", MessageType.Warning);
        }
        int changedCount = 0;
        foreach (QuestValidationResult.Change row in record.Changes)
        {
            if (row.Before != row.After) { changedCount++; }
        }
        EditorGUILayout.LabelField($"실행 전후 · 변경 {changedCount}개 항목", EditorStyles.boldLabel);
        _showUnchangedValues = EditorGUILayout.ToggleLeft("변경 없는 항목도 표시", _showUnchangedValues);
        if (changedCount == 0)
        {
            EditorGUILayout.HelpBox(record.Changes.Count == 0 ? "전후 비교를 수행하지 않은 기록입니다. 실행 제한과 요청 결과를 확인하세요."
                : "기록된 비교 항목에 변경이 없습니다. 적용·거절 여부는 요청 결과를 확인하세요.", MessageType.None);
        }
        if (changedCount > 0 || _showUnchangedValues)
        {
            DrawChangeRow("항목", "실행 전", "실행 후", EditorStyles.boldLabel);
            foreach (QuestValidationResult.Change row in record.Changes)
            {
                bool changed = row.Before != row.After;
                if (!changed && !_showUnchangedValues) { continue; }
                var style = new GUIStyle(EditorStyles.wordWrappedLabel);
                if (changed) { style.fontStyle = FontStyle.Bold; }
                DrawChangeRow(row.Label, row.Before, row.After, style);
            }
        }
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("이 결과 복사")) { GUIUtility.systemCopyBuffer = record.ToReport(); }
        if (GUILayout.Button("이력만 지우기"))
        {
            _runner.ClearHistory();
            _historyIndex = 0;
        }
        EditorGUILayout.EndHorizontal();
        EditorGUILayout.Space();
    }

    private static void DrawStatus(string title, string text, Color color)
    {
        EditorGUI.DrawRect(EditorGUILayout.GetControlRect(false, StatusBarHeight), color);
        var style = new GUIStyle(EditorStyles.boldLabel) { wordWrap = true };
        EditorGUILayout.LabelField(title + ": " + text, style);
    }

    private static void DrawChangeRow(string label, string before, string after, GUIStyle style)
    {
        EditorGUILayout.BeginHorizontal();
        GUILayout.Label(label, style, GUILayout.MinWidth(65f), GUILayout.ExpandWidth(true));
        GUILayout.Label(before, style, GUILayout.MinWidth(65f), GUILayout.ExpandWidth(true));
        GUILayout.Label(after, style, GUILayout.MinWidth(65f), GUILayout.ExpandWidth(true));
        EditorGUILayout.EndHorizontal();
    }

    /// <summary>
    /// 현재 캐릭터의 수락 기록과 보상 수령 완료 목록 표시
    /// </summary>
    private void DrawQuestEntries(QuestTester tester)
    {
        if (!Application.isPlaying) { return; }
        if (!_runner.TryGetQuestEntries(tester, out List<QuestValidationRunner.QuestEntry> entries, out string error))
        {
            EditorGUILayout.HelpBox("캐릭터 퀘스트 목록: " + error, MessageType.Info);
            return;
        }
        int accepted = 0;
        foreach (QuestValidationRunner.QuestEntry entry in entries) { if (!entry.Claimed) { accepted++; } }
        _showAcceptedQuests = EditorGUILayout.Foldout(_showAcceptedQuests, $"수락한 퀘스트 · 보상 미수령 ({accepted})", true);
        if (_showAcceptedQuests) { DrawQuestGroup(entries, false); }
        _showClaimedQuests = EditorGUILayout.Foldout(_showClaimedQuests, $"보상 수령 완료 퀘스트 ({entries.Count - accepted})", true);
        if (_showClaimedQuests) { DrawQuestGroup(entries, true); }
        if (entries.Count == 0) { EditorGUILayout.HelpBox("현재 캐릭터에 수락한 퀘스트 기록이 없습니다.", MessageType.None); }
        EditorGUILayout.Space();
    }

    private void DrawQuestGroup(IReadOnlyList<QuestValidationRunner.QuestEntry> entries, bool claimed)
    {
        foreach (QuestValidationRunner.QuestEntry entry in entries)
        {
            if (entry.Claimed != claimed) { continue; }
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField($"{entry.Id} · {entry.Name}", EditorStyles.wordWrappedLabel);
            EditorGUILayout.LabelField(entry.Summary, EditorStyles.wordWrappedLabel);
            if (GUILayout.Button("이 퀘스트 상세 선택"))
            {
                serializedObject.Update();
                serializedObject.FindProperty("_questId").intValue = entry.Id;
                serializedObject.ApplyModifiedProperties();
            }
            EditorGUILayout.EndVertical();
        }
    }

    /// <summary>
    /// 아이템별 실시간 진행도와 검증용 지급 대상 선택
    /// </summary>
    private void DrawCollectionProgress(QuestTester tester)
    {
        if (!_runner.TryGetItemProgress(tester, out List<ItemRequirementProgress> rows, out string error)) { return; }
        if (_collectionQuestId != tester.QuestId)
        {
            _collectionQuestId = tester.QuestId;
            _collectionTargetIndex = 0;
        }
        EditorGUILayout.LabelField("수집 아이템별 현재 진행도", EditorStyles.boldLabel);
        DrawChangeRow("아이템", "보유 / 필요", "상태", EditorStyles.boldLabel);
        foreach (ItemRequirementProgress row in rows)
        {
            DrawChangeRow($"{row.ItemId} · {row.Name}", $"{row.Current} / {row.Required}",
                row.Required <= 0 ? "수량 오류" : row.Satisfied ? "충족" : $"{row.Missing}개 부족", EditorStyles.wordWrappedLabel);
        }
        if (!string.IsNullOrEmpty(error)) { EditorGUILayout.HelpBox(error, MessageType.Error); }
        if (rows.Count == 0) { return; }
        var choices = new string[rows.Count];
        for (int i = 0; i < rows.Count; i++) { choices[i] = $"{rows[i].ItemId} · {rows[i].Name} (필요 {rows[i].Required})"; }
        _collectionTargetIndex = EditorGUILayout.Popup("검증용 지급 대상", Mathf.Clamp(_collectionTargetIndex, 0, rows.Count - 1), choices);
    }

    private void DrawQuestSelector()
    {
        SerializedProperty questId = serializedObject.FindProperty("_questId");
        GameManager manager = Application.isPlaying ? FindAnyObjectByType<GameManager>() : null;
        if (manager == null || !manager.DataManager.IsDataLoaded || manager.DataManager.QuestTable.Count == 0)
        {
            EditorGUILayout.PropertyField(questId, new GUIContent("퀘스트 ID"));
            return;
        }
        var quests = new List<QuestData>(manager.DataManager.QuestTable.Values);
        quests.Sort((a, b) => a.ID.CompareTo(b.ID));
        var names = new string[quests.Count + 1];
        names[0] = "선택하세요";
        int selected = 0;
        for (int i = 0; i < quests.Count; i++)
        {
            names[i + 1] = $"{quests[i].ID} · {quests[i].Name}";
            if (quests[i].ID == questId.intValue) { selected = i + 1; }
        }
        int next = EditorGUILayout.Popup("검증 퀘스트", selected, names);
        if (next != selected) { questId.intValue = next == 0 ? 0 : quests[next - 1].ID; }
    }

    private void Button(QuestTester tester, string label, QuestValidationRunner.Command command)
    {
        if (GUILayout.Button(label))
        {
            _runner.Execute(tester, command, label);
            _historyIndex = 0;
            Repaint();
        }
    }

    /// <summary>
    /// 대상과 유지 범위를 확인한 초기화 요청
    /// </summary>
    private void ResetButton(QuestTester tester, string label, QuestValidationRunner.Command command)
    {
        if (!GUILayout.Button(label)) { return; }
        if (!_runner.TryDescribeReset(tester, command, out string description))
        {
            _runner.Execute(tester, command, label);
        }
        else if (EditorUtility.DisplayDialog(label, description, "초기화 후 저장", "취소"))
        {
            _runner.Execute(tester, command, label);
        }
        else { return; }
        _historyIndex = 0;
        Repaint();
    }

    private void BindActivePlayer()
    {
        GameManager manager = FindAnyObjectByType<GameManager>();
        if (manager == null || !manager.DataManager.IsDataLoaded || !manager.PlaySessionController.HasActivePlayer)
        {
            _runner.RecordSetup((QuestTester)target, "현재 로컬 캐릭터 연결", false, "현재 플레이 중인 캐릭터가 없습니다.");
            _historyIndex = 0;
            return;
        }
        serializedObject.Update();
        Player player = manager.PlaySessionController.ActivePlayer;
        serializedObject.FindProperty("_player").objectReferenceValue = player;
        serializedObject.ApplyModifiedProperties();
        _runner.RecordSetup((QuestTester)target, "현재 로컬 캐릭터 연결", true,
            $"Player 필드: {player.name} / 선택 슬롯: {manager.CharacterSession.SelectedSlotId}");
        _historyIndex = 0;
        Repaint();
    }

    private void FindMonster(QuestTester tester)
    {
        QuestData quest = GameManager.Instance.DataManager.QuestTable[tester.QuestId];
        MonsterBase found = null;
        foreach (MonsterBase monster in FindObjectsByType<MonsterBase>(FindObjectsInactive.Exclude))
        {
            if (monster.CanDealDamage && monster.Data != null && monster.Data.ID == quest.TargetID &&
                MapManager.Instance != null && monster.gameObject.scene == MapManager.Instance.gameObject.scene)
            {
                found = monster;
                break;
            }
        }
        serializedObject.Update();
        serializedObject.FindProperty("_monster").objectReferenceValue = found;
        serializedObject.ApplyModifiedProperties();
        _runner.RecordSetup(tester, "퀘스트 목표 몬스터 찾기", found != null,
            found == null ? "목표 몬스터가 없습니다. 처치 퀘스트와 현재 맵을 확인하세요."
                : $"Monster 필드: {found.name} / ID {found.Data.ID} / HP {found.CurrentHp}");
        if (found != null) { EditorGUIUtility.PingObject(found); }
        _historyIndex = 0;
        Repaint();
    }

    private void UseQuestItem(QuestTester tester)
    {
        if (!_runner.TryGetItemProgress(tester, out List<ItemRequirementProgress> rows, out string error) ||
            rows.Count == 0 || !string.IsNullOrEmpty(error))
        {
            _runner.RecordSetup(tester, "수집 아이템 설정", false,
                string.IsNullOrEmpty(error) ? "정상적인 수집 목표가 있는 퀘스트를 선택하세요." : error);
            _historyIndex = 0;
            return;
        }
        ItemRequirementProgress row = rows[Mathf.Clamp(_collectionTargetIndex, 0, rows.Count - 1)];
        serializedObject.Update();
        serializedObject.FindProperty("_itemId").intValue = row.ItemId;
        serializedObject.FindProperty("_itemCount").intValue = (int)row.Required;
        serializedObject.ApplyModifiedProperties();
        _runner.RecordSetup(tester, "수집 아이템 설정", true,
            $"지급 설정: {row.ItemId} · {row.Name} × {row.Required}. 실제 지급은 아이템 지급 버튼으로 실행하세요.");
        _historyIndex = 0;
        Repaint();
    }
}
