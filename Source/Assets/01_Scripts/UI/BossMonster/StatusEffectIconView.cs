using System;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 체력바에 표시될 버프/디버프 아이콘 클래스
/// </summary>
public sealed class StatusEffectIconView : MonoBehaviour
{
    [SerializeField] private Image _iconImage;
    [SerializeField] private Image _durationFillImage;

    private float _startTime;
    private float _duration;
    private bool _referencesValidated;

    /// <summary>
    /// 상태 아이콘과 지속시간 초기화
    /// </summary>
    public void Initialize(Sprite icon, float startTime, float duration)
    {
        ValidateReferences();

        if (icon == null)
        {
            throw new ArgumentNullException(nameof(icon));
        }

        if (duration <= 0f)
        {
            throw new ArgumentOutOfRangeException(nameof(duration));
        }

        _iconImage.sprite = icon;
        _startTime = startTime;
        _duration = duration;
        _durationFillImage.fillAmount = 0f;
    }

    private void ValidateReferences()
    {
        if (_referencesValidated)
        {
            return;
        }

        if (_iconImage == null)
        {
            throw new MissingReferenceException($"{name}: 상태 아이콘 Image 참조 필요");
        }

        if (_durationFillImage == null)
        {
            throw new MissingReferenceException($"{name}: 지속시간 Overlay Image 참조 필요");
        }

        if (_durationFillImage.type != Image.Type.Filled)
        {
            throw new InvalidOperationException($"{name}: Overlay Image Type은 Filled 필요");
        }

        _referencesValidated = true;
    }

    /// <summary>
    /// 경과 시간 암전 비율 갱신
    /// </summary>
    public void Tick(float currentTime)
    {
        float elapsed = currentTime - _startTime;
        _durationFillImage.fillAmount = Mathf.Clamp01(elapsed / _duration);
    }
}