using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>보스 결과의 개인 보상 행과 포기 버튼 표시</summary>
internal sealed class BossRewardSelectionView
{
    private const float RowHeight = 44f;
    private const float RowSpacing = 5f;
    private readonly RectTransform _content;
    private readonly UnityEngine.UI.ScrollRect _scroll;
    private readonly UnityEngine.UI.Button _forfeit;
    private readonly TextMeshProUGUI _forfeitText;
    private readonly List<TextMeshProUGUI> _rows = new();
    private readonly TMP_FontAsset _font;
    private readonly Action<int> _claimRequested;
    private string _resultId;

    internal BossRewardSelectionView(Transform parent, TMP_FontAsset font, Action<int> claimRequested,
        Action forfeitRequested)
    {
        _font = font;
        _claimRequested = claimRequested;
        RectTransform list = CreateRect("BossRewardList", parent, new Vector2(0.2f, 0.27f), new Vector2(0.8f, 0.49f));
        AddImage(list, new Color(0.09f, 0.13f, 0.20f, 0.9f));
        _scroll = list.gameObject.AddComponent<UnityEngine.UI.ScrollRect>();
        RectTransform viewport = CreateRect("Viewport", list, Vector2.zero, Vector2.one);
        viewport.gameObject.AddComponent<UnityEngine.UI.RectMask2D>();
        _content = CreateRect("Content", viewport, Vector2.up, Vector2.one);
        _content.pivot = new Vector2(0.5f, 1f);
        _scroll.viewport = viewport;
        _scroll.content = _content;
        _scroll.horizontal = false;
        _scroll.movementType = UnityEngine.UI.ScrollRect.MovementType.Clamped;
        _scroll.scrollSensitivity = RowHeight;

        RectTransform forfeit = CreateRect("ForfeitBossRewards", parent, new Vector2(0.5f, 0.1f), new Vector2(0.5f, 0.1f));
        forfeit.sizeDelta = new Vector2(295f, 58f);
        UnityEngine.UI.Image image = AddImage(forfeit, new Color(0.38f, 0.19f, 0.21f));
        _forfeit = forfeit.gameObject.AddComponent<AnimatedButton>();
        _forfeit.targetGraphic = image;
        forfeit.gameObject.AddComponent<ButtonHoverMotion>();
        _forfeit.onClick.AddListener(() => forfeitRequested());
        _forfeitText = CreateLabel(forfeit, "포기", TextAlignmentOptions.Center);
        Hide();
    }

    /// <summary>결과의 개인 보상과 수령 상태 표시</summary>
    internal void Refresh(NetworkBattleResultSession results, bool confirmForfeit, DataManager dataManager)
    {
        IReadOnlyList<LocalBattleResult.Reward> rewards = results.Rewards;
        _scroll.gameObject.SetActive(rewards.Count > 0);
        _forfeit.gameObject.SetActive(results.UnclaimedRewardCount > 0);
        _forfeitText.text = confirmForfeit
            ? dataManager.GetString(Constants.STR_Boss_OnlineRewardForfeitConfirmButton)
            : dataManager.GetString(Constants.STR_Boss_OnlineRewardForfeitButtonFormat, results.UnclaimedRewardCount);
        while (_rows.Count < rewards.Count)
        {
            AddRow(_rows.Count);
        }

        for (int i = 0; i < _rows.Count; i++)
        {
            TextMeshProUGUI row = _rows[i];
            bool visible = i < rewards.Count;
            row.transform.parent.gameObject.SetActive(visible);
            if (!visible) { continue; }
            LocalBattleResult.Reward reward = rewards[i];
            string statusKey = reward.Status switch
            {
                LocalBattleResult.RewardStatus.Claimed => Constants.STR_Boss_OnlineRewardClaimed,
                LocalBattleResult.RewardStatus.Forfeited => Constants.STR_Boss_OnlineRewardForfeited,
                _ => Constants.STR_Boss_OnlineRewardClaimHint
            };
            row.text = dataManager.GetString(Constants.STR_Boss_OnlineRewardRowFormat,
                results.GetRewardName(reward), reward.Count, dataManager.GetString(statusKey));
        }

        _content.sizeDelta = new Vector2(0f, rewards.Count * (RowHeight + RowSpacing));
        if (_resultId != results.Current?.Id)
        {
            _resultId = results.Current?.Id;
            _scroll.verticalNormalizedPosition = 1f;
        }
    }

    internal void Hide()
    {
        _forfeit.gameObject.SetActive(false);
        _scroll.gameObject.SetActive(false);
        _resultId = null;
    }

    private void AddRow(int index)
    {
        RectTransform row = CreateRect("Reward", _content, Vector2.up, Vector2.one);
        row.pivot = new Vector2(0.5f, 1f);
        row.sizeDelta = new Vector2(0f, RowHeight);
        row.anchoredPosition = new Vector2(0f, -index * (RowHeight + RowSpacing));
        AddImage(row, new Color(0.16f, 0.23f, 0.32f));
        var input = row.gameObject.AddComponent<EventTrigger>();
        var click = new EventTrigger.Entry { eventID = EventTriggerType.PointerClick };
        click.callback.AddListener(eventData =>
        {
            if (eventData is PointerEventData pointer &&
                pointer.button == PointerEventData.InputButton.Left && pointer.clickCount == 2)
            {
                _claimRequested(index);
            }
        });
        input.triggers.Add(click);
        ForwardPointerEvent(input, EventTriggerType.InitializePotentialDrag, _scroll.OnInitializePotentialDrag);
        ForwardPointerEvent(input, EventTriggerType.BeginDrag, _scroll.OnBeginDrag);
        ForwardPointerEvent(input, EventTriggerType.Drag, _scroll.OnDrag);
        ForwardPointerEvent(input, EventTriggerType.EndDrag, _scroll.OnEndDrag);
        ForwardPointerEvent(input, EventTriggerType.Scroll, _scroll.OnScroll);
        _rows.Add(CreateLabel(row, "아이템", TextAlignmentOptions.MidlineLeft));
    }

    private static void ForwardPointerEvent(EventTrigger input, EventTriggerType type, Action<PointerEventData> handler)
    {
        var entry = new EventTrigger.Entry { eventID = type };
        entry.callback.AddListener(eventData => handler((PointerEventData)eventData));
        input.triggers.Add(entry);
    }

    private TextMeshProUGUI CreateLabel(Transform parent, string name, TextAlignmentOptions alignment)
    {
        RectTransform rect = CreateRect(name, parent, Vector2.zero, Vector2.one);
        rect.offsetMin = new Vector2(12f, 0f);
        rect.offsetMax = new Vector2(-12f, 0f);
        var label = rect.gameObject.AddComponent<TextMeshProUGUI>();
        label.font = _font;
        label.fontSize = 23f;
        label.alignment = alignment;
        label.overflowMode = TextOverflowModes.Ellipsis;
        label.richText = false;
        label.raycastTarget = false;
        return label;
    }

    private static RectTransform CreateRect(string name, Transform parent, Vector2 min, Vector2 max)
    {
        var rect = (RectTransform)new GameObject(name, typeof(RectTransform)).transform;
        rect.SetParent(parent, false);
        rect.anchorMin = min;
        rect.anchorMax = max;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
        return rect;
    }

    private static UnityEngine.UI.Image AddImage(RectTransform rect, Color color)
    {
        var image = rect.gameObject.AddComponent<UnityEngine.UI.Image>();
        image.color = color;
        return image;
    }
}
