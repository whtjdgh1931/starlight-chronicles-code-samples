using UnityEngine.EventSystems;

/// <summary>버튼 입력을 선택적 연출 완료 뒤에 실행하는 uGUI 버튼</summary>
public sealed class AnimatedButton : UnityEngine.UI.Button
{
    private ButtonHoverMotion _motion;

    protected override void Awake()
    {
        base.Awake();
        TryGetComponent(out _motion);
    }

    /// <summary>왼쪽 클릭의 기본 처리 또는 버튼 연출 후 클릭 실행</summary>
    public override void OnPointerClick(PointerEventData eventData)
    {
        if (eventData.button == PointerEventData.InputButton.Left && TryDeferClick())
        {
            return;
        }

        base.OnPointerClick(eventData);
    }

    /// <summary>키보드·게임패드 제출의 기본 처리 또는 버튼 연출 후 클릭 실행</summary>
    public override void OnSubmit(BaseEventData eventData)
    {
        if (!TryDeferClick())
        {
            base.OnSubmit(eventData);
        }
    }

    private bool TryDeferClick() =>
        IsActive() && IsInteractable() && _motion != null && _motion.TryPlayPress(InvokeClick);

    private void InvokeClick()
    {
        if (IsActive() && IsInteractable())
        {
            onClick.Invoke();
        }
    }

    protected override void OnDisable()
    {
        CancelPendingClick();
        base.OnDisable();
    }

    internal void CancelPendingClick()
    {
        if (_motion != null)
        {
            _motion.CancelPress();
        }
    }
}
