using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// 퀵슬롯 옆 스킬 목록의 아이콘 드래그
/// 스킬 데이터 변경 없이 슬롯 번호만 전달
/// </summary>
public sealed class SkillQuickSlotSource : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    [SerializeField] private Image _icon;
    [SerializeField] private TextMeshProUGUI _label;
    [Header("등록 상태 표시")]
    [SerializeField] private Color _normalColor = Color.white;
    [SerializeField, Range(0f, 1f)] private float _registeredBrightness = 0.55f;
    private readonly ItemDragVisual _dragVisual = new();
    private bool _isDragging;
    private Vector2 _iconPositionBeforeDrag;

    public int SkillSlotIndex { get; private set; }

    public void Bind(int skillSlotIndex, SkillData skill, Sprite icon)
    {
        SkillSlotIndex = skillSlotIndex;
        _icon.sprite = icon;
        _label.text = skill.SkillName;
        SetRegistered(false);
    }

    /// <summary>
    /// 퀵슬롯 등록 여부에 따른 목록 아이콘 밝기 갱신
    /// </summary>
    public void SetRegistered(bool registered)
    {
        float brightness = registered ? _registeredBrightness : 1f;
        _icon.color = new Color(
            _normalColor.r * brightness,
            _normalColor.g * brightness,
            _normalColor.b * brightness,
            _normalColor.a);
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        if (_isDragging)
        {
            return;
        }

        _iconPositionBeforeDrag = _icon.rectTransform.anchoredPosition;
        _dragVisual.Begin(_icon, GetComponentInParent<Canvas>().rootCanvas);
        _isDragging = true;
    }

    public void OnDrag(PointerEventData eventData)
    {
        _dragVisual.Move(eventData.position);
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        EndDragVisual();
    }

    private void OnDisable()
    {
        EndDragVisual();
    }

    /// <summary>
    /// 드래그 종료 또는 취소 시 스킬 목록 아이콘 위치 복원
    /// </summary>
    private void EndDragVisual()
    {
        if (!_isDragging)
        {
            return;
        }

        _isDragging = false;
        _dragVisual.End();
        _icon.rectTransform.anchoredPosition = _iconPositionBeforeDrag;
    }
}
