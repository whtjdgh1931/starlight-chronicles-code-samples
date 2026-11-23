using System;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 활성 씬의 배경 음악 반복 재생과 교차 전환
/// 지속되는 GameManager 하위에서 초기화 후 수명 유지
/// </summary>
public sealed class BgmPlayer : MonoBehaviour
{
    [SerializeField] private BgmCatalog _catalog;
    [SerializeField] private AudioSource _firstSource;
    [SerializeField] private AudioSource _secondSource;

    private Coroutine _transition;

    /// <summary>
    /// 재생 채널 초기화와 현재 씬 음악 적용
    /// GameManager의 중복 초기화 방지 흐름에서 일회 호출
    /// </summary>
    public void Initialize()
    {
        if (_catalog == null || _firstSource == null || _secondSource == null || _firstSource == _secondSource)
        {
            throw new MissingReferenceException($"{name}: BGM 설정과 서로 다른 재생 채널 두 개 필요");
        }

        ConfigureSource(_firstSource);
        ConfigureSource(_secondSource);
        SceneManager.activeSceneChanged += HandleActiveSceneChanged;
        ApplyScene(SceneManager.GetActiveScene());
    }

    private static void ConfigureSource(AudioSource source)
    {
        source.playOnAwake = false;
        source.loop = true;
        source.spatialBlend = 0f;
        source.volume = 0f;
        source.Stop();
    }

    private void ApplyScene(Scene scene)
    {
        // 게임 씬 외 편집기·보조 씬 제외
        if (!Enum.TryParse(scene.name, out GameSceneId sceneId) || sceneId == GameSceneId.None)
        {
            return;
        }

        Play(_catalog.Get(sceneId));
    }

    /// <summary>
    /// 같은 곡의 재생 위치 유지 또는 새로운 곡으로 전환
    /// </summary>
    private void Play(BgmCatalog.Entry entry)
    {
        if (_transition != null)
        {
            StopCoroutine(_transition);
        }

        AudioSource incoming = SelectSource(entry.Clip);
        AudioSource outgoing = incoming == _firstSource ? _secondSource : _firstSource;
        if (incoming.clip != entry.Clip)
        {
            incoming.Stop();
            incoming.clip = entry.Clip;
            incoming.volume = 0f;
        }

        if (!incoming.isPlaying)
        {
            incoming.Play();
        }

        _transition = StartCoroutine(Crossfade(incoming, outgoing, entry.Volume));
    }

    private AudioSource SelectSource(AudioClip clip)
    {
        if (_firstSource.clip == clip)
        {
            return _firstSource;
        }

        if (_secondSource.clip == clip)
        {
            return _secondSource;
        }

        // 세 번째 곡 요청 시 더 작게 들리는 채널 교체
        return _firstSource.volume <= _secondSource.volume ? _firstSource : _secondSource;
    }

    private IEnumerator Crossfade(AudioSource incoming, AudioSource outgoing, float targetVolume)
    {
        float incomingVolume = incoming.volume;
        float outgoingVolume = outgoing.volume;
        float elapsed = 0f;
        float duration = Mathf.Max(0.01f, _catalog.FadeDuration);
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float progress = Mathf.Clamp01(elapsed / duration);
            incoming.volume = Mathf.Lerp(incomingVolume, targetVolume, progress);
            outgoing.volume = Mathf.Lerp(outgoingVolume, 0f, progress);
            yield return null;
        }

        incoming.volume = targetVolume;
        outgoing.volume = 0f;
        outgoing.Stop();
        outgoing.clip = null;
        _transition = null;
    }

    private void HandleActiveSceneChanged(Scene previous, Scene current)
    {
        ApplyScene(current);
    }

    private void OnDestroy()
    {
        SceneManager.activeSceneChanged -= HandleActiveSceneChanged;
    }
}
