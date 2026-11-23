using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 검색 결과의 스크롤 목록과 방별 참가 버튼 표시
/// 접속 요청은 호출자 콜백에 위임
/// </summary>
public sealed class InternetRoomListView
{
    private const float RowHeight = 46f;
    private const float RowSpacing = 6f;
    private readonly ScrollRect _scroll;
    private readonly RectTransform _content;
    private readonly Button _template;
    private readonly Action<string> _join;
    private readonly List<Button> _buttons = new();
    private readonly List<TMP_Text> _labels = new();
    private readonly List<string> _roomIds = new();
    private IReadOnlyList<InternetRoomInfo> _rooms = Array.Empty<InternetRoomInfo>();

    public InternetRoomListView(RectTransform parent, Button template, Vector2 size, Vector2 position, Action<string> join)
    {
        _template = template;
        _join = join;
        var root = new GameObject("InternetRoomList", typeof(RectTransform), typeof(Image), typeof(ScrollRect));
        var rect = (RectTransform)root.transform;
        rect.SetParent(parent, false);
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = size;
        rect.anchoredPosition = position;
        root.GetComponent<Image>().color = new Color(0.04f, 0.07f, 0.12f, 0.8f);
        var viewport = (RectTransform)new GameObject("Viewport", typeof(RectTransform), typeof(RectMask2D)).transform;
        viewport.SetParent(rect, false);
        viewport.anchorMin = Vector2.zero;
        viewport.anchorMax = Vector2.one;
        viewport.sizeDelta = Vector2.zero;
        _content = (RectTransform)new GameObject("Content", typeof(RectTransform)).transform;
        _content.SetParent(viewport, false);
        _content.anchorMin = new Vector2(0, 1);
        _content.anchorMax = Vector2.one;
        _content.pivot = new Vector2(0.5f, 1);
        _scroll = root.GetComponent<ScrollRect>();
        _scroll.viewport = viewport;
        _scroll.content = _content;
        _scroll.horizontal = false;
        _scroll.movementType = ScrollRect.MovementType.Clamped;
        _scroll.scrollSensitivity = RowHeight;
    }

    /// <summary>
    /// 결과 목록과 참가 가능 상태 표시, 생성한 행 재사용
    /// </summary>
    public void Refresh(IReadOnlyList<InternetRoomInfo> rooms, bool visible, bool canJoin)
    {
        _scroll.gameObject.SetActive(visible);
        if (!visible) { return; }
        bool changed = !ReferenceEquals(_rooms, rooms);
        _rooms = rooms;
        while (_buttons.Count < rooms.Count)
        {
            int index = _buttons.Count;
            Button button = UnityEngine.Object.Instantiate(_template, _content);
            button.name = "Room";
            button.onClick = new Button.ButtonClickedEvent();
            button.onClick.AddListener(() => _join(_roomIds[index]));
            var rect = (RectTransform)button.transform;
            rect.anchorMin = new Vector2(0, 1);
            rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(0.5f, 1);
            rect.sizeDelta = new Vector2(0, RowHeight);
            rect.anchoredPosition = new Vector2(0, -index * (RowHeight + RowSpacing));
            TMP_Text label = button.GetComponentInChildren<TMP_Text>(true);
            label.rectTransform.anchorMin = Vector2.zero;
            label.rectTransform.anchorMax = Vector2.one;
            label.rectTransform.sizeDelta = Vector2.zero;
            label.rectTransform.anchoredPosition = Vector2.zero;
            label.fontSize = 21;
            label.richText = false;
            label.maxVisibleLines = 1;
            label.overflowMode = TextOverflowModes.Ellipsis;
            _buttons.Add(button);
            _labels.Add(label);
            _roomIds.Add(null);
        }
        for (int i = 0; i < _buttons.Count; i++)
        {
            bool active = i < rooms.Count;
            string roomId = active ? rooms[i].Id : null;
            if (_roomIds[i] != roomId && _buttons[i] is AnimatedButton animatedButton)
            {
                animatedButton.CancelPendingClick();
            }

            _roomIds[i] = roomId;
            _buttons[i].gameObject.SetActive(active);
            if (active)
            {
                _buttons[i].interactable = canJoin;
                _labels[i].text = $"{rooms[i].Name}  {rooms[i].PlayerCount}/{rooms[i].MaxPlayers}  참가";
            }
        }
        _content.sizeDelta = new Vector2(0, rooms.Count * (RowHeight + RowSpacing));
        if (changed) { _scroll.verticalNormalizedPosition = 1f; }
    }
}
