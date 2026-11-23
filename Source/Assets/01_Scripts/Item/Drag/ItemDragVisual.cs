using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 드래그 시 아이템 아이콘 이동
/// </summary>
public class ItemDragVisual
{
    private Image _iconImage;
    private Transform _originalParent;
    private int _originalSiblingIndex;


    /// <summary>
    /// 드래그 시작
    /// </summary>
    /// <param name="iconImage"></param>
    /// <param name="canvas"></param>
    public void Begin(Image iconImage, Canvas canvas)
    {
        _iconImage = iconImage;
        _originalParent = iconImage.transform.parent;
        _originalSiblingIndex = iconImage.transform.GetSiblingIndex();

        iconImage.transform.SetParent(canvas.transform);
        iconImage.raycastTarget = false;

    }

    /// <summary>
    /// 마우스를 따라 아이콘 이동
    /// </summary>
    /// <param name="screenPosition"></param>
    public void Move(Vector2 screenPosition)
    {
        if (_iconImage == null)
        {
            return;
        }

        _iconImage.transform.position = screenPosition;
    }

    /// <summary>
    /// 드래그 종료 시 아이콘 복귀
    /// </summary>
    public void End()
    {
        if (_iconImage == null)
        {
            return;
        }

        _iconImage.transform.SetParent(_originalParent, true);
        _iconImage.rectTransform.anchoredPosition = Vector2.zero;
        _iconImage.transform.SetSiblingIndex(_originalSiblingIndex);
        _iconImage.raycastTarget = true;

        _iconImage = null;
        _originalParent = null;
    }
}
