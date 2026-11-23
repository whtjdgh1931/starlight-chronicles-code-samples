using UnityEngine;
using UnityEngine.Audio;

/// <summary>
/// 사용자 BGM·효과음 볼륨 적용과 로컬 설정 저장
/// GameManager 하위의 활성 객체에서 수명 유지
/// </summary>
public sealed class AudioSettingsController : MonoBehaviour
{
    private const string BgmVolumeKey = "Audio.BgmVolume";
    private const string SfxVolumeKey = "Audio.SfxVolume";
    private const string BgmVolumeParameter = "BgmVolume";
    private const string SfxVolumeParameter = "SfxVolume";
    private const float DefaultVolume = 1f;
    private const float MinimumDecibels = -80f;
    private const float DecibelScale = 20f;

    [SerializeField] private AudioMixer _mixer;

    public float BgmVolume { get; private set; }
    public float SfxVolume { get; private set; }

    /// <summary>
    /// 필수 믹서 확인과 사용자 설정 불러오기
    /// UI 초기화 이전에 GameManager에서 일회 호출
    /// </summary>
    public void Initialize()
    {
        if (_mixer == null)
        {
            throw new MissingReferenceException($"{name}: AudioMixer 참조 필요");
        }
        BgmVolume = Mathf.Clamp01(PlayerPrefs.GetFloat(BgmVolumeKey, DefaultVolume));
        SfxVolume = Mathf.Clamp01(PlayerPrefs.GetFloat(SfxVolumeKey, DefaultVolume));
    }

    // Awake에서 호출할 수 없는 믹서 파라미터의 최초 적용
    private void Start()
    {
        ApplyVolume(BgmVolumeParameter, BgmVolume);
        ApplyVolume(SfxVolumeParameter, SfxVolume);
    }

    /// <summary>
    /// 0~1 범위 BGM 볼륨 변경과 즉시 적용
    /// </summary>
    public void SetBgmVolume(float volume)
    {
        BgmVolume = Mathf.Clamp01(volume);
        ApplyVolume(BgmVolumeParameter, BgmVolume);
    }

    /// <summary>
    /// 0~1 범위 효과음 볼륨 변경과 즉시 적용
    /// </summary>
    public void SetSfxVolume(float volume)
    {
        SfxVolume = Mathf.Clamp01(volume);
        ApplyVolume(SfxVolumeParameter, SfxVolume);
    }

    /// <summary>
    /// 현재 설정을 캐릭터 저장과 분리하여 기록
    /// </summary>
    public void Save()
    {
        PlayerPrefs.SetFloat(BgmVolumeKey, BgmVolume);
        PlayerPrefs.SetFloat(SfxVolumeKey, SfxVolume);
        PlayerPrefs.Save();
    }

    private void ApplyVolume(string parameter, float volume)
    {
        float decibels = volume > 0f ? Mathf.Max(MinimumDecibels, Mathf.Log10(volume) * DecibelScale) : MinimumDecibels;
        if (!_mixer.SetFloat(parameter, decibels))
        {
            Debug.LogError($"{name}: AudioMixer 노출 파라미터 확인 필요: {parameter}", this);
        }
    }

    private void OnApplicationPause(bool isPaused)
    {
        if (isPaused)
        {
            Save();
        }
    }

    private void OnApplicationQuit() => Save();
}
