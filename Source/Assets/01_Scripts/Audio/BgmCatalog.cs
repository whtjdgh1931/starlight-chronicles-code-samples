using System;
using UnityEngine;

/// <summary>
/// 씬별 배경 음악과 전환 설정
/// </summary>
[CreateAssetMenu(menuName = "Audio/BGM Catalog")]
public sealed class BgmCatalog : ScriptableObject
{
    [Serializable]
    public sealed class Entry
    {
        [SerializeField] private GameSceneId _scene;
        [SerializeField] private AudioClip _clip;
        [SerializeField, Range(0f, 1f)] private float _volume = 0.25f;

        public GameSceneId Scene => _scene;
        public AudioClip Clip => _clip;
        public float Volume => _volume;
    }

    [SerializeField, Min(0.01f)] private float _fadeDuration = 1f;
    [SerializeField] private Entry[] _entries = Array.Empty<Entry>();

    public float FadeDuration => _fadeDuration;

    /// <summary>
    /// 등록된 게임 씬의 음악 설정 조회
    /// </summary>
    public Entry Get(GameSceneId scene)
    {
        foreach (Entry entry in _entries)
        {
            if (entry.Scene == scene)
            {
                return entry;
            }
        }

        throw new InvalidOperationException($"{scene}: BGM 설정 누락");
    }
}
