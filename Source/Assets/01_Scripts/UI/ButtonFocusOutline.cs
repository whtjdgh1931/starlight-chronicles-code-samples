using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>공통 확인 입력으로 실행될 버튼의 금색 테두리</summary>
[DisallowMultipleComponent]
internal sealed class ButtonFocusOutline : MonoBehaviour, ISelectHandler, IDeselectHandler
{
    private static readonly Color FocusColor = new Color(1f, 0.77f, 0.25f);
    private UnityEngine.UI.Outline _outline;

    internal static void Attach(UnityEngine.UI.Button button)
    {
        if (!button.TryGetComponent(out UnityEngine.UI.Outline outline))
        {
            throw new MissingComponentException($"{button.name}: 팝업 포커스 Outline 필요");
        }

        ButtonFocusOutline focus = button.gameObject.AddComponent<ButtonFocusOutline>();
        focus._outline = outline;
        outline.effectColor = FocusColor;
        outline.effectDistance = new Vector2(2f, -2f);
        outline.useGraphicAlpha = true;
        focus.Refresh();
    }

    public void OnSelect(BaseEventData eventData) => _outline.enabled = true;

    public void OnDeselect(BaseEventData eventData) => _outline.enabled = false;

    private void OnEnable() => Refresh();

    private void OnDisable()
    {
        if (_outline != null) { _outline.enabled = false; }
    }

    private void Refresh()
    {
        if (_outline == null) { return; }
        EventSystem eventSystem = EventSystem.current;
        _outline.enabled = eventSystem != null && eventSystem.currentSelectedGameObject == gameObject;
    }
}
