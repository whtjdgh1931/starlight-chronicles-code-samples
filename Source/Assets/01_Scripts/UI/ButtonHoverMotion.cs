using System;
using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>버튼의 선택적 호버 확대와 클릭 펀치</summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(UnityEngine.UI.Button))]
public sealed class ButtonHoverMotion : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    private const float HoverScale = 1.04f;
    private const float EnterDuration = 0.12f;
    private const float ExitDuration = 0.10f;
    private const float PressDuration = 0.15f;
    private static readonly Vector3 PressPunch = new(-0.05f, -0.05f, 0f);

    [SerializeField, Tooltip("호버 확대와 클릭 펀치 사용")]
    private bool _enableMotion = false;

    private UnityEngine.UI.Button _button;
    private RectTransform _buttonRect;
    private RectTransform _hitRect;
    private UnityEngine.UI.Image _hitImage;
    private readonly List<UnityEngine.UI.Graphic> _originalRaycastTargets = new();
    private Vector3 _baseScale;
    private float _currentScale = 1f;
    private Tween _tween;
    private Tween _pressTween;
    private Vector3 _pressOffset;

    private void Awake()
    {
        _button = GetComponent<UnityEngine.UI.Button>();
        _buttonRect = (RectTransform)transform;
    }

    private void CreateHitTarget()
    {
        var target = new GameObject("HoverRaycastTarget", typeof(RectTransform), typeof(UnityEngine.UI.Image));
        target.layer = gameObject.layer;
        _hitRect = (RectTransform)target.transform;
        _hitRect.SetParent(_buttonRect, false);
        _hitRect.anchorMin = Vector2.zero;
        _hitRect.anchorMax = Vector2.one;
        _hitRect.pivot = _buttonRect.pivot;
        _hitRect.offsetMin = Vector2.zero;
        _hitRect.offsetMax = Vector2.zero;

        _hitImage = target.GetComponent<UnityEngine.UI.Image>();
        _hitImage.color = Color.clear;
        _hitImage.raycastTarget = false;
        _hitImage.canvasRenderer.cullTransparentMesh = false;
    }

    /// <summary>조작 가능한 버튼의 포인터 진입 확대</summary>
    public void OnPointerEnter(PointerEventData eventData)
    {
        if (_enableMotion && _button.IsInteractable())
        {
            PrepareMotion();
            AnimateScale(HoverScale, EnterDuration);
        }
    }

    /// <summary>포인터 이탈 시 기준 크기로 복귀</summary>
    public void OnPointerExit(PointerEventData eventData)
    {
        if (_hitImage != null && _hitImage.raycastTarget)
        {
            AnimateScale(1f, ExitDuration);
        }
    }

    private void PrepareMotion()
    {
        if (_hitRect == null)
        {
            CreateHitTarget();
        }

        if (_hitImage.raycastTarget)
        {
            return;
        }

        _baseScale = _buttonRect.localScale;
        foreach (UnityEngine.UI.Graphic graphic in GetComponentsInChildren<UnityEngine.UI.Graphic>(true))
        {
            if (graphic.raycastTarget && graphic.GetComponentInParent<UnityEngine.UI.Selectable>(true) == _button)
            {
                _originalRaycastTargets.Add(graphic);
                graphic.raycastTarget = false;
            }
        }

        _hitImage.raycastTarget = true;
    }

    private void AnimateScale(float targetScale, float duration)
    {
        StopTween();
        _tween = DOVirtual.Float(_currentScale, targetScale, duration, SetScale)
            .SetEase(Ease.OutCubic)
            .SetUpdate(true)
            .OnComplete(() => _tween = null);
    }

    private void SetScale(float scale)
    {
        _currentScale = scale;
        Vector3 visualScale = (Vector3.one + _pressOffset) * scale;
        _buttonRect.localScale = Vector3.Scale(_baseScale, visualScale);
        // 시각 배율의 역수로 클릭·호버 영역 유지
        _hitRect.localScale = new Vector3(1f / visualScale.x, 1f / visualScale.y, 1f / visualScale.z);
    }

    internal bool TryPlayPress(Action onComplete)
    {
        if (!_enableMotion || !isActiveAndEnabled)
        {
            return false;
        }

        if (_pressTween != null)
        {
            return true;
        }

        PrepareMotion();
        SetPressOffset(Vector3.zero);
        _pressTween = DOTween.Punch(() => _pressOffset, SetPressOffset, PressPunch, PressDuration, 1, 0f)
            .SetUpdate(true)
            .OnComplete(() =>
            {
                SetPressOffset(Vector3.zero);
                _pressTween = null;
                onComplete();
            });
        return true;
    }

    private void SetPressOffset(Vector3 offset)
    {
        _pressOffset = offset;
        SetScale(_currentScale);
    }

    internal void CancelPress()
    {
        _pressTween?.Kill();
        _pressTween = null;
        if (_hitRect != null)
        {
            SetPressOffset(Vector3.zero);
        }
    }

    private void StopTween()
    {
        _tween?.Kill();
        _tween = null;
    }

    private void OnDisable()
    {
        StopTween();
        CancelPress();
        if (_buttonRect != null && _hitRect != null)
        {
            SetScale(1f);
            _hitImage.raycastTarget = false;
        }

        foreach (UnityEngine.UI.Graphic graphic in _originalRaycastTargets)
        {
            if (graphic != null)
            {
                graphic.raycastTarget = true;
            }
        }

        _originalRaycastTargets.Clear();
    }

    private void OnDestroy()
    {
        if (_hitRect != null)
        {
            Destroy(_hitRect.gameObject);
        }
    }
}
