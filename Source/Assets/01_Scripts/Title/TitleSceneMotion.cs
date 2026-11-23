using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class TitleSceneMotion : MonoBehaviour
{
    [SerializeField] private CanvasGroup _logo;
    [SerializeField] private CanvasGroup _star;
    [SerializeField] private RectTransform _background;
    [SerializeField] private Image _lightRay;
    [SerializeField, Min(0.01f)] private float _logoDuration = 4f;
    [SerializeField] private Vector2 _rayInterval = new Vector2(8f, 12f);
    [SerializeField, Min(0.01f)] private float _rayDuration = 2f;

    [SerializeField] private Vector2 _raySize = new Vector2(0.88f, 0.27f);
    [SerializeField] private Vector2[] _glintAnchors =
    {
        new Vector2(0.82f, 0.76f),
        new Vector2(0.19f, 0.62f),
        new Vector2(0.73f, 0.27f),
        new Vector2(0.36f, 0.85f),
    };

    private Sequence _intro;
    private Sequence _glints;
    private Tween _rayMotion;
    private RectTransform _logoRect;
    private RectTransform _starRect;
    private Vector2 _logoPosition;
    private Vector3 _logoScale;
    private Vector3 _starScale;

    private void Awake()
    {
        _logoRect = (RectTransform)_logo.transform;
        _logoPosition = _logoRect.anchoredPosition;
        _logoScale = _logoRect.localScale;
        _starRect = (RectTransform)_star.transform;
        _starScale = _starRect.localScale;
    }

    private void OnEnable()
    {
        _logo.alpha = 0f;
        _logoRect.anchoredPosition = _logoPosition + Vector2.down * 8f;
        _logoRect.localScale = _logoScale * 0.98f;
        _star.alpha = 0f;
        _intro = DOTween.Sequence().SetUpdate(true);
        _intro.Append(_logo.DOFade(1f, _logoDuration).SetEase(Ease.Linear));
        _intro.Join(_logoRect.DOAnchorPos(_logoPosition, _logoDuration).SetEase(Ease.OutCubic));
        _intro.Join(_logoRect.DOScale(_logoScale, _logoDuration).SetEase(Ease.OutCubic));
        _intro.InsertCallback(_logoDuration * 0.75f, PlayGlints);

        SetAlpha(_lightRay, 0f);
        ScheduleRay(_logoDuration + 0.5f);
    }

    private void PlayGlints()
    {
        const float glintDuration = 0.55f;
        _glints = DOTween.Sequence().SetUpdate(true).SetLoops(-1);
        foreach (Vector2 anchor in _glintAnchors)
        {
            _glints.AppendCallback(() =>
            {
                _starRect.anchorMin = _starRect.anchorMax = anchor;
                _starRect.anchoredPosition = Vector2.zero;
            });
            _glints.Append(DOVirtual.Float(0f, 1f, glintDuration, progress =>
            {
                _star.alpha = Mathf.Clamp01(Mathf.Sin(progress * Mathf.PI));
                _starRect.localScale = _starScale * Mathf.Lerp(0.72f, 1.84f, progress);
            }).SetEase(Ease.Linear));
            _glints.AppendInterval(1.1f);
        }
    }

    private void ScheduleRay(float delay)
    {
        _rayMotion = DOVirtual.DelayedCall(delay, PlayRay, ignoreTimeScale: true);
    }

    private void PlayRay()
    {
        _rayMotion = DOVirtual.Float(0f, 1f, _rayDuration, progress =>
        {
            Vector2 size = _background.rect.size;
            var corner = size * 0.5f;
            Vector2 offset = corner + new Vector2(180f, 120f);
            Vector2 position = Vector2.Lerp(offset,
                -offset, progress);
            RectTransform ray = _lightRay.rectTransform;
            ray.anchoredPosition = position;
            ray.sizeDelta = new Vector2(size.magnitude * _raySize.x, size.y * _raySize.y);
            ray.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(offset.y, offset.x) * Mathf.Rad2Deg);
            SetAlpha(_lightRay, Mathf.Sin(progress * Mathf.PI) * 0.38f);
        }).SetEase(Ease.Linear).SetUpdate(true).OnComplete(() =>
        {
            SetAlpha(_lightRay, 0f);
            ScheduleRay(Random.Range(_rayInterval.x, _rayInterval.y));
        });
    }

    private void OnDisable()
    {
        _intro?.Kill();
        _glints?.Kill();
        _rayMotion?.Kill();
    }

    private static void SetAlpha(Graphic image, float alpha)
    {
        Color color = image.color;
        color.a = alpha;
        image.color = color;
    }
}
