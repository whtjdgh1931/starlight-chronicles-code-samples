using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
public class PlayerUI : MonoBehaviour
{
    [SerializeField] private GameObject _hudPanel;

    [Header("슬라이더 연결")]
    [SerializeField] private Slider _hpSlider;
    [SerializeField] private Image _hpFill;
    [SerializeField, Min(0.02f)] private float _hpBlinkInterval = 0.1f;
    [SerializeField, Range(0f, 1f)] private float _hpBlinkAlpha = 0.35f;
    private Sequence _hpFeedback;
    private Color _hpFillColor;
    private float _targetHp;
    private bool _hasHpValue;
    [SerializeField] private Slider _mpSlider;
    [SerializeField] private Slider _expSlider;

    [Header("텍스트 연결")]
    [SerializeField] private TextMeshProUGUI _levelText;
    [SerializeField] private TextMeshProUGUI _hpText;
    [SerializeField] private TextMeshProUGUI _mpText;
    [SerializeField] private TextMeshProUGUI _expText;
    private bool _isMaxLevel;

    public void Initialize()
    {
        if (_hudPanel != null)
        {
            _hudPanel.SetActive(true); // HUD 패널 활성화
        }
    }

    private void OnEnable()
    {
        DOTween.Init();
        _hpFillColor = _hpFill.color;
        _hasHpValue = false;



        EventBus.OnPlayerHpChanged -= UpdateHpUI;
        EventBus.OnPlayerHpChanged += UpdateHpUI;

        EventBus.OnPlayerMpChanged -= UpdateMpUI;
        EventBus.OnPlayerMpChanged += UpdateMpUI;

        EventBus.OnPlayerExpChanged -= UpdateExpUI;
        EventBus.OnPlayerExpChanged += UpdateExpUI;

        EventBus.OnPlayerLevelChanged -= UpdateLevelUI;
        EventBus.OnPlayerLevelChanged += UpdateLevelUI;
    }

    private void UpdateHpUI(float curHp, float maxHp)
    {
        _hpText.text = $"{Mathf.RoundToInt(curHp)} / {Mathf.RoundToInt(maxHp)}";
        bool sameMaximum = Mathf.Approximately(_hpSlider.maxValue, maxHp);
        if (_hasHpValue && sameMaximum && Mathf.Approximately(_targetHp, curHp)) { return; }

        // 표시 중인 값 대신 마지막 실제 HP와 비교하여 연속 피격·회복 구분
        bool isDamage = _hasHpValue && sameMaximum && curHp < _targetHp;
        _targetHp = curHp;
        _hasHpValue = true;
        StopHpFeedback();
        _hpSlider.maxValue = maxHp;
        if (!isDamage)
        {
            _hpSlider.SetValueWithoutNotify(curHp);
            return;
        }

        float duration = Constants.FLOAT_SliderAnimateDuration;
        int blinkCount = Mathf.Max(1, Mathf.RoundToInt(duration / (_hpBlinkInterval * 2f)));
        float halfBlinkDuration = duration / (blinkCount * 2f);
        _hpFeedback = DOTween.Sequence();
        _hpFeedback.Append(_hpSlider.DOValue(curHp, duration).SetEase(Ease.Linear));
        _hpFeedback.Join(_hpFill.DOFade(_hpFillColor.a * _hpBlinkAlpha, halfBlinkDuration)
            .SetEase(Ease.Linear).SetLoops(blinkCount * 2, LoopType.Yoyo));
        _hpFeedback.OnComplete(() =>
        {
            _hpFill.color = _hpFillColor;
            _hpFeedback = null;
        });
    }

    /// <summary>
    /// HP 감소 연출 중단과 원래 색상 복원
    /// </summary>
    private void StopHpFeedback()
    {
        _hpFeedback?.Kill();
        _hpFeedback = null;
        _hpFill.color = _hpFillColor;
    }

    private void UpdateMpUI(float curMp, float maxMp)
    {
        _mpSlider.maxValue = maxMp;
        _mpText.text = $"{Mathf.RoundToInt(curMp)} / {Mathf.RoundToInt(maxMp)}";

        _mpSlider.DOKill();
        _mpSlider.DOValue(curMp, Constants.FLOAT_SliderAnimateDuration);

    }

    private void UpdateExpUI(float curExp, float maxExp)
    {
        if (_isMaxLevel)
        {
            _expSlider.DOKill();
            _expSlider.maxValue = 1f;
            _expSlider.value = 1f;
            _expText.text = "MAX";
            return;
        }
        _expSlider.maxValue = maxExp;

        float expPercentage = 0f;
        if (maxExp > 0f)
        {
            expPercentage = (curExp / maxExp) * 100f;
        }
        _expText.text = $"{expPercentage:F2}%";
        _expSlider.DOKill();
        _expSlider.DOValue(curExp, Constants.FLOAT_SliderAnimateDuration);
    }

    private void UpdateLevelUI(int level)
    {
        _isMaxLevel = level >= Constants.MaxPlayerLevel;
        _levelText.text = $"Lv. {level}";
    }

    private void OnDisable()
    {

        EventBus.OnPlayerHpChanged -= UpdateHpUI;
        EventBus.OnPlayerMpChanged -= UpdateMpUI;
        EventBus.OnPlayerExpChanged -= UpdateExpUI;
        EventBus.OnPlayerLevelChanged -= UpdateLevelUI;

        StopHpFeedback();
        _hasHpValue = false;
        _mpSlider.DOKill();
        _expSlider.DOKill();
    }


}
