using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

public sealed class MonsterHealthBar : MonoBehaviour
{
    [SerializeField] private GameObject _root;
    [SerializeField] private Slider _slider;
    [SerializeField] private float _decreaseDuration = 0.35f;
    [SerializeField] private float _visibleDuration = 3f;

    private Tween _valueTween;
    private Tween _hideTween;

    public void Initialize(float maxHp)
    {
        CheckComponent();
        KillTweens();

        _slider.maxValue = maxHp;
        _slider.value = maxHp;
        _root.SetActive(false);
    }

    private void CheckComponent()
    {
        if (_root == null || _slider == null)
        {
            throw new MissingReferenceException($"{name}: HP Bar Root와 Slider 참조 필요");
        }
    }

    public void Show(float currentHp)
    {
        _root.SetActive(true);

        _valueTween?.Kill();
        _hideTween?.Kill();

        _valueTween = _slider
            .DOValue(currentHp, _decreaseDuration)
            .SetEase(Ease.OutQuad);

        _hideTween = DOVirtual.DelayedCall(_visibleDuration, Hide);
    }

    public void Hide()
    {
        KillTweens();
        _root.SetActive(false);
    }

    private void KillTweens()
    {
        _valueTween?.Kill();
        _hideTween?.Kill();

        _valueTween = null;
        _hideTween = null;
    }

    private void OnDisable()
    {
        KillTweens();
    }
}