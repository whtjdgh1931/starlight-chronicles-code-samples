using System;
using System.Collections.Generic;
using System.Text;
using DisplayState = PlayerQuestProgress.DisplayState;

/// <summary>
/// 상인 퀘스트 선택·요청·상태 조회와 화면 갱신
/// NPC 접근 판정과 캐릭터 조작 제한 제외
/// </summary>
public sealed class MerchantQuestPresenter
{
    private readonly Player _player;
    private readonly DataManager _data;
    private readonly MerchantView _view;
    private readonly QuestData[] _quests;
    private readonly List<QuestData> _visibleQuests = new();
    private int? _selectedQuestId;
    private bool _showCompleted;

    public bool IsOpen => _view.IsOpen;
    public event Action CloseRequested
    {
        add => _view.CloseRequested += value;
        remove => _view.CloseRequested -= value;
    }

    /// <summary>
    /// NPC에 지정된 의뢰만 표시 대상으로 구성
    /// </summary>
    public MerchantQuestPresenter(Player player, DataManager data, MerchantView view, IReadOnlyList<int> questIds)
    {
        _player = player;
        _data = data;
        _view = view;
        _quests = new QuestData[questIds.Count];
        var uniqueIds = new HashSet<int>();
        for (int index = 0; index < questIds.Count; index++)
        {
            int id = questIds[index];
            if (!uniqueIds.Add(id) || !data.QuestTable.TryGetValue(id, out _quests[index]))
            {
                throw new InvalidOperationException($"상인 퀘스트 중복 또는 정의 누락: {id}");
            }
        }
    }

    /// <summary>
    /// 캐릭터와 화면 준비 이후 한 번 구독
    /// </summary>
    public void Initialize()
    {
        _view.Initialize(_quests.Length);
        _view.MenuRequested += ShowMenu;
        _view.QuestsRequested += ShowQuests;
        _view.QuestSelected += SelectQuest;
        _view.QuestTabRequested += SelectTab;
        _view.ActionRequested += HandleAction;
        _player.QuestProgress.Changed += Refresh;
    }

    /// <summary>
    /// 소유 컨트롤러의 연결 해제 시 대응 이벤트 정리
    /// </summary>
    public void Shutdown()
    {
        _view.MenuRequested -= ShowMenu;
        _view.QuestsRequested -= ShowQuests;
        _view.QuestSelected -= SelectQuest;
        _view.QuestTabRequested -= SelectTab;
        _view.ActionRequested -= HandleAction;
        _player.QuestProgress.Changed -= Refresh;
        Close();
    }

    public void ShowMenu() => _view.ShowMenu();

    public void Close()
    {
        // 영구 UI와 씬 컨트롤러의 종료 순서 경계
        if (_view != null)
        {
            _view.Close();
        }
    }

    private void ShowQuests()
    {
        _showCompleted = false;
        _view.ShowQuests();
        Refresh();
    }

    private void SelectTab(bool completed)
    {
        if (!_view.IsQuestPageOpen || _showCompleted == completed) { return; }

        _showCompleted = completed;
        _selectedQuestId = null;
        _view.ShowMessage(string.Empty);
        Refresh();
    }

    private void SelectQuest(int index)
    {
        if (index < 0 || index >= _visibleQuests.Count) { return; }
        _selectedQuestId = _visibleQuests[index].ID;
        _view.ShowMessage(string.Empty);
        Refresh();
    }

    private void HandleAction()
    {
        if (!_view.IsQuestPageOpen || _showCompleted) { return; }
        int? requestedQuestId = _selectedQuestId;
        Refresh();
        // 갱신으로 선택 대상이 달라진 경우 다른 의뢰 요청 방지
        if (!requestedQuestId.HasValue || requestedQuestId != _selectedQuestId) { return; }
        int selectedIndex = _visibleQuests.FindIndex(quest => quest.ID == requestedQuestId);
        if (selectedIndex < 0) { return; }

        QuestData quest = _visibleQuests[selectedIndex];
        bool accepted = _player.QuestProgress.TryGetProgress(quest.ID, out _, out _);
        QuestRequestResult result = accepted ? _player.ClaimQuestReward(quest.ID) : _player.AcceptQuest(quest.ID);
        Refresh();
        _view.ShowMessage(GetFeedback(result, accepted));
    }

    private void Refresh()
    {
        if (!_view.IsQuestPageOpen)
        {
            return;
        }
        _visibleQuests.Clear();
        _view.SetQuestTab(_showCompleted);
        var labels = new List<string>();
        foreach (QuestData quest in _quests)
        {
            DisplayState state = _player.QuestProgress.GetDisplayInfo(quest.ID).Status;
            if (state == DisplayState.Locked) { continue; }
            if ((state == DisplayState.Claimed) != _showCompleted) { continue; }
            _visibleQuests.Add(quest);
            labels.Add($"{quest.Name}\n{GetStateLabel(state)}");
        }
        _view.SetQuestList(labels);

        if (_visibleQuests.Count == 0)
        {
            _selectedQuestId = null;
            _view.SetDetail(_showCompleted ? "완료한 의뢰가 없습니다." : "표시할 미완료 의뢰가 없습니다.",
                _showCompleted ? "완료" : "수락", false);
            return;
        }
        int selectedIndex = _visibleQuests.FindIndex(quest => quest.ID == _selectedQuestId);
        if (selectedIndex < 0) { selectedIndex = 0; }
        QuestData selectedQuest = _visibleQuests[selectedIndex];
        _selectedQuestId = selectedQuest.ID;
        RefreshDetail(selectedQuest);
    }

    /// <summary>
    /// 선택한 의뢰의 목표·보상과 요청 버튼 갱신
    /// </summary>
    private void RefreshDetail(QuestData quest)
    {
        PlayerQuestProgress.QuestInfo info = _player.QuestProgress.GetDisplayInfo(quest.ID);
        DisplayState state = info.Status;
        string rewardItem = _data.ItemTable[quest.RewardItemID].Name;
        string objective = state == DisplayState.Claimed ? "보상 수령 완료" : BuildObjectiveText(info);
        string detail = $"{quest.Name}\n\n{quest.Description}\n\n목표\n{objective}\n\n" +
            $"보상\n골드 {quest.RewardGold} / 경험치 {quest.RewardExp}\n{rewardItem} {quest.RewardItemCount}개";
        if (quest.RequiredQuestID > 0)
        {
            detail += $"\n\n선행 의뢰: {_data.QuestTable[quest.RequiredQuestID].Name}";
        }
        string actionLabel = state switch
        {
            DisplayState.Available => "수락",
            DisplayState.Ready => "보상 받기",
            DisplayState.Claimed => "완료",
            _ => GetStateLabel(state)
        };
        _view.SetDetail(detail, actionLabel, state is DisplayState.Available or DisplayState.Ready);
    }

    /// <summary>
    /// 공통 목표 정보를 화면 표시 문자열로 변환
    /// </summary>
    private static string BuildObjectiveText(PlayerQuestProgress.QuestInfo info)
    {
        var text = new StringBuilder();
        foreach (PlayerQuestProgress.ObjectiveInfo objective in info.Objectives)
        {
            string unit = objective.Type == QuestObjectiveType.KillMonster ? "마리" : "개";
            text.AppendLine($"{objective.Name}: {objective.Current} / {objective.Required}{unit}");
        }
        return text.ToString().TrimEnd();
    }

    private static string GetStateLabel(DisplayState state) => state switch
    {
        DisplayState.Locked => "선행 의뢰 필요",
        DisplayState.Available => "수락 가능",
        DisplayState.InProgress => "진행 중",
        DisplayState.Ready => "보상 수령 가능",
        _ => "완료"
    };

    private static string GetFeedback(QuestRequestResult result, bool wasAccepted) => result.Reason switch
    {
        QuestRequestResult.Code.Success => wasAccepted
            ? "보상을 받았어요."
            : "의뢰를 수락했어요.",
        QuestRequestResult.Code.AlreadyAccepted => "이미 수락한 의뢰예요.",
        QuestRequestResult.Code.AlreadyClaimed => "이미 보상을 받은 의뢰예요.",
        QuestRequestResult.Code.RewardCapacity => "보상 전체를 받을 공간이 부족해요. 가방을 정리한 뒤 다시 받아 주세요.",
        QuestRequestResult.Code.ItemTarget => "필요한 수집품이 부족해요. 현재 보유 수량을 확인해 주세요.",
        QuestRequestResult.Code.KillTarget => "아직 처치 목표를 달성하지 못했어요.",
        QuestRequestResult.Code.Prerequisite => "먼저 이전 의뢰의 보상을 받아 주세요.",
        QuestRequestResult.Code.Dead => "부활한 뒤 다시 이야기해 주세요.",
        QuestRequestResult.Code.Busy => "이전 요청을 처리하고 있어요. 잠시 후 다시 시도해 주세요.",
        _ => "지금은 의뢰를 처리할 수 없어요. 캐릭터와 의뢰 정보를 확인해 주세요."
    };

}
