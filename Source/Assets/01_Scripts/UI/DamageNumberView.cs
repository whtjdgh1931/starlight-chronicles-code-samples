using TMPro;
using UnityEngine;

/// <summary>
/// 데미지 숫자의 오른쪽 위 이동, 확대와 페이드 표시
/// </summary>
[RequireComponent(typeof(PoolableObject))]
public sealed class DamageNumberView : MonoBehaviour
{
    [SerializeField] private TMP_Text _text;
    [SerializeField] private PoolableObject _poolableObject;
    [SerializeField] private MeshRenderer _textRenderer;
    [SerializeField, Min(0.01f)] private float _displayDuration = 0.55f;
    [SerializeField, Min(0f)] private float _riseDistance = 1.2f;
    [SerializeField, Min(0f)] private float _horizontalDistance = 0.20f;
    [SerializeField, Min(0f)] private float _initialScale = 0.75f;
    [SerializeField, Min(0f)] private float _peakScale = 1.15f;
    [SerializeField, Range(0.01f, 0.99f)] private float _scalePeakTime = 0.2f;
    [SerializeField, Range(0f, 0.9f)] private float _fadeStartTime = 0.45f;

    private Color _color;
    private Vector3 _startPosition;
    private float _elapsed;
    private bool _isPlaying;
    private int _baseSortingOrder;

    public UnityEngine.EntityId EntityId { get; private set; }
    public bool IsPlaying => _isPlaying;

    private void Awake()
    {
        ValidateReferences();
        _baseSortingOrder = _textRenderer.sortingOrder;
    }

    /// <summary>
    /// 프리팹 필수 참조 검증
    /// </summary>
    public void ValidateReferences()
    {
        if (_text == null || _poolableObject == null || _textRenderer == null)
        {
            throw new MissingReferenceException($"{name}: TMP_Text, PoolableObject, MeshRenderer 참조 필요");
        }
    }

    /// <summary>
    /// 피해량과 표시 스타일 적용 및 피격 위치에서 표시 시작
    /// </summary>
    public void Show(DamageNumberRequest request, Color color)
    {
        EntityId = request.EntityId;
        _color = color;
        _elapsed = 0f;
        _startPosition = request.Position;
        _isPlaying = true;

        transform.position = _startPosition;
        transform.localScale = Vector3.one * _initialScale;
        _text.SetText("{0}", request.Amount);
        _text.color = _color;
    }

    /// <summary>프리팹 기준 순서에 현재 표시 순번 적용</summary>
    internal void SetDisplayOrder(int order)
    {
        _textRenderer.sortingOrder = _baseSortingOrder + order;
    }

    /// <summary>상승 진행도를 유지한 채 기존 숫자의 최소 높이 보정</summary>
    internal void RaiseTo(float minimumY)
    {
        Vector3 position = transform.position;
        if (position.y >= minimumY)
        {
            return;
        }

        _startPosition.y += minimumY - position.y;
        position.y = minimumY;
        transform.position = position;
    }

    private void Update()
    {
        if (!_isPlaying)
        {
            return;
        }

        _elapsed += Time.deltaTime;
        float normalizedTime = Mathf.Clamp01(_elapsed / _displayDuration);
        float scale = normalizedTime < _scalePeakTime ? Mathf.SmoothStep(_initialScale, _peakScale, normalizedTime / _scalePeakTime) : Mathf.SmoothStep(_peakScale, 1f, (normalizedTime - _scalePeakTime) / (1f - _scalePeakTime));
        float riseProgress = 1f - Mathf.Pow(1f - normalizedTime, 3f);
        float fadeProgress = Mathf.InverseLerp(_fadeStartTime, 1f, normalizedTime);

        Vector3 travel = new(_horizontalDistance, _riseDistance, 0f);
        transform.position = _startPosition + travel * riseProgress;
        transform.localScale = Vector3.one * scale;
        _text.color = new Color(_color.r, _color.g, _color.b, _color.a * (1f - Mathf.SmoothStep(0f, 1f, fadeProgress)));

        if (normalizedTime < 1f)
        {
            return;
        }

        _isPlaying = false;
        _poolableObject.Release();
    }

    private void OnDisable()
    {
        _isPlaying = false;
        EntityId = default;
    }
}
