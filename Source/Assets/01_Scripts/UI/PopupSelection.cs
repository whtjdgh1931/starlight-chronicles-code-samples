using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

/// <summary>팝업 기본 선택과 닫을 때의 이전 선택 복원</summary>
internal sealed class PopupSelection
{
    private sealed class BackgroundClickState
    {
        public readonly bool OriginalDeselect;
        public int OpenPopupCount;

        public BackgroundClickState(bool originalDeselect)
        {
            OriginalDeselect = originalDeselect;
        }
    }

    private static readonly Dictionary<InputSystemUIInputModule, BackgroundClickState> BackgroundClickStates = new();
    private EventSystem _eventSystem;
    private InputSystemUIInputModule _inputModule;
    private GameObject _previousSelection;

    internal void Open(GameObject popup, Selectable defaultSelection)
    {
        EventSystem eventSystem = EventSystem.current;
        if (eventSystem != null && eventSystem == _eventSystem)
        {
            return;
        }

        Close(popup);
        if (eventSystem == null)
        {
            return;
        }

        _eventSystem = eventSystem;
        GameObject selected = eventSystem.currentSelectedGameObject;
        _previousSelection = IsInside(selected, popup) ? null : selected;
        if (eventSystem.TryGetComponent(out _inputModule))
        {
            PreserveSelectionOnBackgroundClick(_inputModule);
        }

        eventSystem.SetSelectedGameObject(CanSelect(defaultSelection) ? defaultSelection.gameObject : null);
    }

    internal void Close(GameObject popup)
    {
        EventSystem eventSystem = _eventSystem;
        GameObject previousSelection = _previousSelection;
        _eventSystem = null;
        _previousSelection = null;
        ReleaseBackgroundClickProtection();
        if (eventSystem == null || !eventSystem.isActiveAndEnabled || eventSystem != EventSystem.current)
        {
            return;
        }

        GameObject selected = eventSystem.currentSelectedGameObject;
        if (selected != null && !IsInside(selected, popup))
        {
            return;
        }

        bool canRestore = previousSelection != null && previousSelection.TryGetComponent(out Selectable selectable) && CanSelect(selectable);
        eventSystem.SetSelectedGameObject(canRestore ? previousSelection : null);
    }

    private static void PreserveSelectionOnBackgroundClick(InputSystemUIInputModule module)
    {
        if (!BackgroundClickStates.TryGetValue(module, out BackgroundClickState state))
        {
            state = new BackgroundClickState(module.deselectOnBackgroundClick);
            BackgroundClickStates.Add(module, state);
            module.deselectOnBackgroundClick = false;
        }

        state.OpenPopupCount++;
    }

    private void ReleaseBackgroundClickProtection()
    {
        InputSystemUIInputModule module = _inputModule;
        _inputModule = null;
        // 파괴된 Unity 객체도 등록된 모듈 키에서 제거
        if (ReferenceEquals(module, null) || !BackgroundClickStates.TryGetValue(module, out BackgroundClickState state))
        {
            return;
        }

        state.OpenPopupCount--;
        if (state.OpenPopupCount == 0)
        {
            BackgroundClickStates.Remove(module);
            if (module != null) { module.deselectOnBackgroundClick = state.OriginalDeselect; }
        }
    }

    private static bool IsInside(GameObject selected, GameObject popup)
    {
        return selected != null && popup != null && selected.transform.IsChildOf(popup.transform);
    }

    private static bool CanSelect(Selectable selectable)
    {
        return selectable != null && selectable.isActiveAndEnabled && selectable.IsInteractable();
    }
}
