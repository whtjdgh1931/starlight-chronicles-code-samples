using DG.Tweening;
using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// 퀵슬롯 한 칸의 아이콘·수량·재사용 대기 표시와 포인터 요청 전달
/// </summary>
public sealed class QuickSlotView : MonoBehaviour, IPointerClickHandler, IDropHandler,
    IBeginDragHandler, IDragHandler, IEndDragHandler
{
    [SerializeField] private Image _icon;
    [SerializeField] private Slider _cooldownSlider;
    [SerializeField] private Image _readyFlash;
    [SerializeField, Min(0.01f)] private float _readyFlashDuration = 0.35f;
    [SerializeField, Range(0f, 1f)] private float _readyFlashAlpha = 0.7f;
    [SerializeField] private TextMeshProUGUI _count;
    [SerializeField] private TextMeshProUGUI _key;
    [SerializeField] private TextMeshProUGUI _label;

    private readonly ItemDragVisual _dragVisual = new();
    private Canvas _rootCanvas;
    private bool _isDragging;
    private Vector2 _iconPositionBeforeDrag;
    private bool _iconRaycastBeforeDrag;

    /// <summary>
    /// 드래그 종료 목적지 전달, 슬롯 밖은 null
    /// </summary>
    public event Action<QuickSlotView> DragFinished;

    public event Action Requested;
    public event Action<int> ItemDropped;
    public event Action<int> SkillDropped;
    public event Action Cleared;

    /// <summary>
    /// 필수 표시 참조 확인과 초기 연출 정리
    /// </summary>
    public void Initialize()
    {
        if (_cooldownSlider == null || _readyFlash == null)
        {
            throw new MissingReferenceException($"{name}: 쿨타임 Slider와 완료 강조 이미지 참조 필요");
        }
        _rootCanvas = GetComponentInParent<Canvas>().rootCanvas;
        ResetCooldownFeedback();
    }

    /// <summary>
    /// 미리 배치된 슬롯에 아이콘·단축키·이름·수량 반영
    /// </summary>
    public void Render(Sprite icon, string key, string label, string count)
    {
        _icon.sprite = icon;
        _icon.enabled = icon != null;
        _key.text = key;
        _label.text = label;
        _count.text = count;
    }

    /// <summary>
    /// 위에서 아래로 걷히는 쿨타임과 대기 종료 시 한 번 강조
    /// </summary>
    public void SetCooldown(float remaining, float duration)
    {
        float value = duration > 0f ? Mathf.Clamp01(remaining / duration) : 0f;
        bool becameReady = _cooldownSlider.value > 0f && value <= 0f;
        if (_cooldownSlider.value <= 0f && value > 0f) { StopReadyFlash(); }
        _cooldownSlider.SetValueWithoutNotify(value);
        if (becameReady) { PlayReadyFlash(); }
    }

    /// <summary>
    /// 슬롯 교체·해제 시 이전 쿨타임과 강조 연출 초기화
    /// </summary>
    public void ResetCooldownFeedback()
    {
        StopReadyFlash();
        _cooldownSlider.SetValueWithoutNotify(0f);
    }

    private void PlayReadyFlash()
    {
        _readyFlash.DOKill();
        Color color = _readyFlash.color;
        color.a = _readyFlashAlpha;
        _readyFlash.color = color;
        _readyFlash.DOFade(0f, _readyFlashDuration).SetEase(Ease.OutQuad);
    }

    private void StopReadyFlash()
    {
        _readyFlash.DOKill();
        Color color = _readyFlash.color;
        color.a = 0f;
        _readyFlash.color = color;
    }

    private void OnDisable()
    {
        EndDragVisual();
        ResetCooldownFeedback();
    }

    /// <summary>
    /// 등록된 아이콘의 왼쪽 드래그 시작
    /// </summary>
    public void OnBeginDrag(PointerEventData eventData)
    {
        if (_isDragging || eventData.button != PointerEventData.InputButton.Left ||
            !_icon.enabled || _icon.sprite == null)
        {
            return;
        }
        _isDragging = true;
        eventData.eligibleForClick = false;
        _iconPositionBeforeDrag = _icon.rectTransform.anchoredPosition;
        _iconRaycastBeforeDrag = _icon.raycastTarget;
        _dragVisual.Begin(_icon, _rootCanvas);
        _dragVisual.Move(eventData.position);
    }

    /// <summary>
    /// 포인터 위치로 드래그 아이콘 이동
    /// </summary>
    public void OnDrag(PointerEventData eventData)
    {
        if (_isDragging) { _dragVisual.Move(eventData.position); }
    }

    /// <summary>
    /// 아이콘 복원 후 드롭 목적지 전달
    /// 비활성화에 따른 취소는 등록 변경 제외
    /// </summary>
    public void OnEndDrag(PointerEventData eventData)
    {
        if (!_isDragging) { return; }
        GameObject hit = eventData.pointerCurrentRaycast.gameObject;
        QuickSlotView target = hit != null ? hit.GetComponentInParent<QuickSlotView>() : null;
        EndDragVisual();
        DragFinished?.Invoke(target);
    }

    private void EndDragVisual()
    {
        if (!_isDragging) { return; }
        _isDragging = false;
        _dragVisual.End();
        _icon.rectTransform.anchoredPosition = _iconPositionBeforeDrag;
        _icon.raycastTarget = _iconRaycastBeforeDrag;
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (_isDragging || eventData.dragging || !eventData.eligibleForClick) { return; }
        if (eventData.button == PointerEventData.InputButton.Left) { Requested?.Invoke(); }
        else if (eventData.button == PointerEventData.InputButton.Right) { Cleared?.Invoke(); }
    }

    public void OnDrop(PointerEventData eventData)
    {
        if (eventData.pointerDrag != null && eventData.pointerDrag.TryGetComponent(out SkillQuickSlotSource source))
        {
            SkillDropped?.Invoke(source.SkillSlotIndex);
            return;
        }
        if (ItemDragResolver.TryResolve(eventData, out ItemDrag drag) && drag.Source == ItemDrag.SourceType.Inventory &&
            drag.InventoryTab == InventoryTab.Consumable)
        {
            ItemDropped?.Invoke(drag.Item.ItemData.ID);
        }
    }
}
