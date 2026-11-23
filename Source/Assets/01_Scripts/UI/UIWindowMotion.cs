using DG.Tweening;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 창의 페이드·확대 연출과 전환 중 입력 관리
/// </summary>
[DisallowMultipleComponent]
public sealed class UIWindowMotion : MonoBehaviour
{
    [SerializeField] private CanvasGroup _canvasGroup;
    [SerializeField] private RectTransform _visualRoot;
    [SerializeField] private bool _deactivateWhenHidden;
    [SerializeField, Range(0.8f, 1f)] private float _closedScale = 0.96f;
    [SerializeField, Min(0.01f)] private float _openDuration = 0.16f;
    [SerializeField, Min(0.01f)] private float _closeDuration = 0.1f;

    private Sequence _transition;
    private Vector3 _baseScale;
    private bool _initialized;

    /// <summary>애니메이션 도중에도 즉시 갱신되는 목표 표시 상태</summary>
    public bool IsVisible { get; private set; }

    /// <summary>창 전환이 끝나 입력을 받을 수 있는 상태</summary>
    internal bool CanInteract => IsVisible && _transition == null && isActiveAndEnabled && _canvasGroup.interactable;

    /// <summary>기존 UI 루트에 런타임 전환 연결과 초기 숨김</summary>
    internal static UIWindowMotion Attach(GameObject root, float closedScale = 0.96f, float openDuration = 0.16f)
    {
        if (root.transform is not RectTransform rect)
        {
            throw new MissingComponentException($"{root.name}: 창의 RectTransform 필요");
        }
        if (!root.TryGetComponent(out CanvasGroup group)) { group = root.AddComponent<CanvasGroup>(); }

        var motion = root.AddComponent<UIWindowMotion>();
        motion._canvasGroup = group;
        motion._visualRoot = rect;
        motion._deactivateWhenHidden = true;
        motion._closedScale = closedScale;
        motion._openDuration = openDuration;
        motion.Initialize();
        return motion;
    }

    private void OnEnable()
    {
        SceneManager.sceneUnloaded += HandleSceneUnloaded;
    }

    /// <summary>필수 참조 확인과 초기 표시 설정, 재호출 시 기준 크기 보존</summary>
    public void Initialize(bool visible = false)
    {
        if (_canvasGroup == null || _visualRoot == null)
        {
            throw new MissingReferenceException($"{name}: 창 CanvasGroup과 시각 RectTransform 참조 필요");
        }
        if (!_initialized)
        {
            _baseScale = _visualRoot.localScale;
            _initialized = true;
        }
        SetVisible(visible, true);
    }

    /// <summary>
    /// 목표 표시 전환, immediate는 초기화·씬 전환 시 연출 없이 정리
    /// 반복 요청은 유지하고 반대 요청은 현재 표시에서 전환
    /// </summary>
    public void SetVisible(bool visible, bool immediate = false)
    {
        if (!_initialized) { throw new System.InvalidOperationException($"{name}: UIWindowMotion 초기화 필요"); }
        if (!immediate && IsVisible == visible) { return; }

        KillTransition();
        IsVisible = visible;
        if (visible) { gameObject.SetActive(true); }

        if (immediate || !isActiveAndEnabled)
        {
            FinishTransition(visible);
            return;
        }

        if (visible && _canvasGroup.alpha <= 0f) { _visualRoot.localScale = _baseScale * _closedScale; }
        _canvasGroup.interactable = false;
        // 페이드 중 배경 UI로 클릭이 통과하지 않도록 표시 종료까지 유지
        _canvasGroup.blocksRaycasts = true;
        float duration = visible ? _openDuration : _closeDuration;
        _transition = DOTween.Sequence().SetUpdate(true);
        _transition.Join(_canvasGroup.DOFade(visible ? 1f : 0f, duration).SetEase(Ease.Linear));
        _transition.Join(_visualRoot.DOScale(_baseScale * (visible ? 1f : _closedScale), duration).SetEase(Ease.OutCubic));
        _transition.OnComplete(() =>
        {
            _transition = null;
            FinishTransition(visible);
        });
    }

    private void FinishTransition(bool visible)
    {
        _canvasGroup.alpha = visible ? 1f : 0f;
        _canvasGroup.interactable = visible;
        _canvasGroup.blocksRaycasts = visible;
        _visualRoot.localScale = _baseScale;
        if (!visible && _deactivateWhenHidden) { gameObject.SetActive(false); }
    }

    private void KillTransition()
    {
        _transition?.Kill();
        _transition = null;
    }

    private void OnDisable()
    {
        SceneManager.sceneUnloaded -= HandleSceneUnloaded;
        KillTransition();
        if (!_initialized) { return; }
        IsVisible = false;
        _canvasGroup.alpha = 0f;
        _canvasGroup.interactable = false;
        _canvasGroup.blocksRaycasts = false;
        _visualRoot.localScale = _baseScale;
    }

    private void OnDestroy()
    {
        SceneManager.sceneUnloaded -= HandleSceneUnloaded;
        KillTransition();
    }

    private void HandleSceneUnloaded(Scene scene)
    {
        if (_initialized) { SetVisible(IsVisible, true); }
    }
}
