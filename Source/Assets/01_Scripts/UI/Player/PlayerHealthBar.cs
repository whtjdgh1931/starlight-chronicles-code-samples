using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 다른 플레이어의 머리 위 체력 표시
/// </summary>
public sealed class PlayerHealthBar : MonoBehaviour
{
    [SerializeField] private GameObject _root;
    [SerializeField] private Slider _slider;

    /// <summary>
    /// 소유자 여부에 따른 표시 설정과 초기 체력 반영
    /// </summary>
    public void Initialize(bool isVisible, Vector2 health)
    {
        _slider.interactable = false;
        SetHealth(health.x, health.y);
        _root.SetActive(isVisible);
    }

    /// <summary>
    /// 서버에서 전달된 현재·최대 체력 표시
    /// </summary>
    public void SetHealth(float currentHp, float maxHp)
    {
        _slider.maxValue = maxHp;
        _slider.SetValueWithoutNotify(currentHp);
    }
}
