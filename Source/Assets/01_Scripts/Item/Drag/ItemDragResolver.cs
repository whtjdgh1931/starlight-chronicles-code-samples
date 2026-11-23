using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// 아이템 드래그 시 아이템 정보 가져오기
/// </summary>
public static class ItemDragResolver
{
    public static bool TryResolve(PointerEventData eventData, out ItemDrag itemDrag)
    {
        itemDrag = null;

        GameObject draggedObject = eventData.pointerDrag;
        if (draggedObject == null)
        {
            return false;
        }

        var dragSource = draggedObject.GetComponent(typeof(IItemDragSource)) as IItemDragSource;

        return dragSource != null && dragSource.TryGetDrag(out itemDrag);
    }
}
