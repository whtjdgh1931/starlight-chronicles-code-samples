using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 보스 체력 HUD 표시
/// </summary>
public sealed class BossHudView : MonoBehaviour
{
    [SerializeField] private Image _healthFill;
    [SerializeField] private Image _secondPhaseFill;
    [SerializeField] private TextMeshProUGUI _healthPercentText;
    [SerializeField] private TextMeshProUGUI _remainingBarText;
    private float _phaseChangeHpRatio;

    private void ValidateReferences()
    {
        if (_healthFill == null || _secondPhaseFill == null)
        {
            throw new MissingReferenceException($"{name}: 초록·빨강 체력 Fill Image 참조 필요");
        }

        if (_healthPercentText == null)
        {
            throw new MissingReferenceException($"{name}: 체력 퍼센트 Text 참조 필요");
        }

        if (_remainingBarText == null)
        {
            throw new MissingReferenceException($"{name}: 남은 체력바 Text 참조 필요");
        }

        if (_healthFill.type != Image.Type.Filled || _secondPhaseFill.type != Image.Type.Filled)
        {
            throw new InvalidOperationException($"{name}: 체력 Fill Image Type은 Filled 필요");
        }
    }

    /// <summary>
    /// 보스 HUD 표시와 페이즈별 체력 구간 설정
    /// </summary>
    public void Show(float currentHp, float maxHp, float phaseChangeHpRatio)
    {
        ValidateReferences();

        if (float.IsNaN(phaseChangeHpRatio) || phaseChangeHpRatio <= 0f || phaseChangeHpRatio >= 1f)
        {
            throw new ArgumentOutOfRangeException(nameof(phaseChangeHpRatio));
        }

        _phaseChangeHpRatio = phaseChangeHpRatio;
        gameObject.SetActive(true);
        SetHealth(currentHp, maxHp);
    }

    /// <summary>
    /// 초록·빨강 체력바와 전체 체력 비율 갱신
    /// </summary>
    public void SetHealth(float currentHp, float maxHp)
    {
        if (maxHp <= 0f)
        {
            throw new ArgumentOutOfRangeException(nameof(maxHp), $"{name}: 최대 체력은 0 초과 필요");
        }

        float hp = Mathf.Clamp(currentHp, 0f, maxHp);
        float secondPhaseHp = maxHp * _phaseChangeHpRatio;
        float firstPhaseHp = maxHp - secondPhaseHp;

        // 빨강 체력 위에 남아 있는 초록 체력 표시
        _healthFill.fillAmount = Mathf.Clamp01((hp - secondPhaseHp) / firstPhaseHp);
        _secondPhaseFill.fillAmount = Mathf.Clamp01(hp / secondPhaseHp);

        int remainingBars = hp > secondPhaseHp ? 2 : hp > 0f ? 1 : 0;
        int healthPercent = Mathf.CeilToInt(hp / maxHp * 100f);
        _remainingBarText.SetText("x{0}", remainingBars);
        _healthPercentText.SetText("{0}%", healthPercent);
    }

    /// <summary>
    /// 보스 HUD 숨김
    /// </summary>
    public void Hide()
    {
        gameObject.SetActive(false);
    }
}
