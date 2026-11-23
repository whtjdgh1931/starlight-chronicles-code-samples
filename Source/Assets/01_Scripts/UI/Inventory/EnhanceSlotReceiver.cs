using UnityEngine;
using UnityEngine.EventSystems;

public class EnhanceSlotReceiver : MonoBehaviour, IDropHandler
{
    [Header("메인 강화창 UI 연결")]
    [SerializeField] private EnhanceUI _enhanceUI;

    public void OnDrop(PointerEventData eventData)
    {
        if (!ItemDragResolver.TryResolve(eventData, out ItemDrag itemDrag))
        {
            return;
        }

        _enhanceUI.SetTarget(itemDrag.Item);
    }
}



