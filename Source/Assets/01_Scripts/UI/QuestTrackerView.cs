using System.Text;
using TMPro;
using UnityEngine;

/// <summary>
/// 수락한 미완료 의뢰의 진행도와 보상 수령 가능 상태 표시
/// 의뢰 판정과 보상 지급은 기존 진행도·서비스 담당
/// </summary>
public sealed class QuestTrackerView : MonoBehaviour
{
    [SerializeField] private TMP_Text _content;
    [SerializeField] private UIWindowMotion _windowMotion;
    [SerializeField] private Color _readyColor = new(0.157f, 0.42f, 0.196f);
    [Header("표시 전환")]
    [SerializeField] private UnityEngine.UI.Button _toggleButton;
    [SerializeField] private UnityEngine.UI.Button _closeButton;
    private bool _userVisible = true;
    private PlayerQuestProgress _progress;
    private bool _sceneVisible;
    private bool _resetScroll = true;
    private UnityEngine.UI.ScrollRect _scrollRect;

    /// <summary>
    /// 버튼과 단축키의 의뢰 창 토글 연결
    /// 숨김 상태에서도 입력 구독 유지
    /// </summary>
    public void Initialize()
    {
        if (_content == null || _windowMotion == null || _toggleButton == null || _closeButton == null)
        {
            throw new System.InvalidOperationException($"{name}: 의뢰 진행 창 텍스트와 버튼 참조 필요");
        }
        _scrollRect = _content.GetComponentInParent<UnityEngine.UI.ScrollRect>(true);
        if (_scrollRect == null || _scrollRect.content != _content.rectTransform)
        {
            throw new MissingReferenceException($"{name}: 의뢰 본문의 ScrollRect 연결 필요");
        }
        _windowMotion.Initialize();
        _toggleButton.onClick.RemoveListener(Toggle);
        _toggleButton.onClick.AddListener(Toggle);
        _closeButton.onClick.RemoveListener(Close);
        _closeButton.onClick.AddListener(Close);
        EventBus.OnQuestTrackerInput -= HandleToggleInput;
        EventBus.OnQuestTrackerInput += HandleToggleInput;
        Refresh();
    }

    /// <summary>
    /// 버튼과 단축키의 공통 표시 전환
    /// 표시 상태는 현재 실행 동안 씬 이동·캐릭터 재연결 시 유지
    /// </summary>
    public void Toggle()
    {
        if (!_sceneVisible || _progress == null) { return; }
        _userVisible = !_userVisible;
        Refresh();
    }

    /// <summary>
    /// 사용자 요청에 따른 의뢰 창 숨김
    /// </summary>
    public void Close()
    {
        _userVisible = false;
        Refresh();
    }

    private void HandleToggleInput()
    {
        var eventSystem = UnityEngine.EventSystems.EventSystem.current;
        GameObject selected = eventSystem != null ? eventSystem.currentSelectedGameObject : null;
        if (selected != null && selected.GetComponentInParent<TMP_InputField>() != null) { return; }
        Toggle();
    }

    public void Bind(PlayerQuestProgress progress)
    {
        Unbind();
        _resetScroll = true;
        _progress = progress;
        _progress.Changed += Refresh;
        Refresh();
    }

    public void Unbind()
    {
        if (_progress != null) { _progress.Changed -= Refresh; }
        _progress = null;
        Refresh();
    }

    public void SetSceneVisible(bool visible)
    {
        _sceneVisible = visible;
        Refresh();
    }

    private void Refresh()
    {
        bool canShow = _sceneVisible && _progress != null;
        _toggleButton.gameObject.SetActive(canShow);
        if (!canShow || !_userVisible)
        {
            _windowMotion.SetVisible(false, !canShow);
            return;
        }

        _content.text = BuildUnfinishedText();
        _windowMotion.SetVisible(true);
        if (_resetScroll)
        {
            _resetScroll = false;
            Canvas.ForceUpdateCanvases();
            _scrollRect.StopMovement();
            _scrollRect.verticalNormalizedPosition = 1f;
        }
    }

    private string BuildUnfinishedText()
    {
        var quests = _progress.GetAcceptedUnclaimed();
        var text = new StringBuilder();
        foreach (PlayerQuestProgress.QuestInfo quest in quests)
        {
            if (text.Length > 0) { text.Append("\n\n"); }
            bool ready = quest.Status == PlayerQuestProgress.DisplayState.Ready;
            if (ready) { text.Append("<color=#").Append(ColorUtility.ToHtmlStringRGB(_readyColor)).Append('>'); }
            text.Append(Escape(quest.Name));
            if (ready) { text.Append(" (보상 수령 가능)"); }
            foreach (PlayerQuestProgress.ObjectiveInfo objective in quest.Objectives)
            {
                text.Append('\n').Append(Escape(objective.Name)).Append("  ")
                    .Append(objective.Current).Append(" / ").Append(objective.Required)
                    .Append(objective.Type == QuestObjectiveType.KillMonster ? "마리" : "개");
            }
            if (ready) { text.Append("</color>"); }
        }
        return text.Length > 0 ? text.ToString() : "미완료 의뢰가 없습니다.";
    }

    private static string Escape(string text) => text.Replace("<", "‹").Replace(">", "›");
    private void OnDestroy()
    {
        EventBus.OnQuestTrackerInput -= HandleToggleInput;
        if (_toggleButton != null) { _toggleButton.onClick.RemoveListener(Toggle); }
        if (_closeButton != null) { _closeButton.onClick.RemoveListener(Close); }
        if (_progress != null) { _progress.Changed -= Refresh; }
    }
}
