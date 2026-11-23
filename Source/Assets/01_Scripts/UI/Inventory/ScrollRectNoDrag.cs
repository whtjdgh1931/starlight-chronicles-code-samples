using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// 화면 콘텐츠 드래그 스크롤은 비활성화하고,
/// 오직 스크롤바 및 마우스 휠(선택 사항)로만 조작되도록 하는 ScrollRect입니다.
/// </summary>
public class ScrollRectNoDrag : ScrollRect
{
    public override void OnInitializePotentialDrag(PointerEventData eventData) { }

    public override void OnBeginDrag(PointerEventData eventData) { }

    public override void OnEndDrag(PointerEventData eventData) { }

    public override void OnDrag(PointerEventData eventData) { }
}
