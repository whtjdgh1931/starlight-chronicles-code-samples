using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// BGM·효과음 슬라이더 연결과 설정창 표시
/// 공통 Canvas에서 씬 전환 중에도 수명 유지
/// </summary>
public sealed class AudioSettingsView : MonoBehaviour
{
    [SerializeField] private AudioSettingsController _settings;
    [SerializeField] private Slider _bgmSlider;
    [SerializeField] private Slider _sfxSlider;
    private readonly PopupSelection _selection = new();

    public bool IsOpen => gameObject.activeSelf;

    /// <summary>
    /// 필수 참조 확인과 중복 없는 슬라이더 연결
    /// </summary>
    public void Initialize()
    {
        if (_settings == null || _bgmSlider == null || _sfxSlider == null)
        {
            throw new MissingReferenceException($"{name}: 오디오 설정과 슬라이더 참조 필요");
        }
        _bgmSlider.onValueChanged.RemoveListener(_settings.SetBgmVolume);
        _sfxSlider.onValueChanged.RemoveListener(_settings.SetSfxVolume);
        _bgmSlider.onValueChanged.AddListener(_settings.SetBgmVolume);
        _sfxSlider.onValueChanged.AddListener(_settings.SetSfxVolume);
        RefreshSliders();
    }

    /// <summary>
    /// 현재 볼륨 반영 후 설정창 표시
    /// </summary>
    public void Open()
    {
        RefreshSliders();
        gameObject.SetActive(true);
        transform.SetAsLastSibling();
        _selection.Open(gameObject, _bgmSlider);
    }

    /// <summary>
    /// 설정 저장 후 설정창 숨김
    /// </summary>
    public void Close()
    {
        _settings.Save();
        gameObject.SetActive(false);
    }

    private void RefreshSliders()
    {
        _bgmSlider.SetValueWithoutNotify(_settings.BgmVolume);
        _sfxSlider.SetValueWithoutNotify(_settings.SfxVolume);
    }

    private void OnDisable() => _selection.Close(gameObject);

    private void OnDestroy()
    {
        // 필수 참조 누락으로 초기화가 실패한 객체의 파괴 경계
        if (_settings == null)
        {
            return;
        }
        _bgmSlider?.onValueChanged.RemoveListener(_settings.SetBgmVolume);
        _sfxSlider?.onValueChanged.RemoveListener(_settings.SetSfxVolume);
    }
}
