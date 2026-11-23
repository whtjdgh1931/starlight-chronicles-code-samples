using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

/// <summary>
/// 영구 GameManager의 전투 재생 채널과 효과음 믹서 연결
/// 기존 BGM·볼륨 설정 UI·캐릭터와 지형 구성 보존
/// </summary>
public static class CombatSoundSetup
{
    private const int LocalChannelCount = 4;
    private const int RemoteChannelCount = 2;

    [MenuItem("Tools/Audio/Configure Combat Sound Limits")]
    public static void Configure()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || Enumerable.Range(0, SceneManager.sceneCount).Any(i => SceneManager.GetSceneAt(i).isDirty))
        {
            throw new InvalidOperationException("플레이 종료와 열린 씬 저장 후 전투 사운드 구성 필요");
        }
        SceneSetup[] previous = EditorSceneManager.GetSceneManagerSetup();
        try
        {
            foreach (string name in new[] { "TitleScene", "HomeScene" })
            {
                Scene scene = EditorSceneManager.OpenScene($"Assets/00_Scenes/{name}.unity", OpenSceneMode.Single);
                GameManager manager = Object.FindAnyObjectByType<GameManager>();
                CombatSoundPlayer player = manager.GetComponent<CombatSoundPlayer>();
                if (player == null)
                {
                    player = manager.gameObject.AddComponent<CombatSoundPlayer>();
                }
                var settings = new SerializedObject(player);
                SetChannels(settings.FindProperty("_localSources"), manager.transform, "LocalCombat", LocalChannelCount);
                SetChannels(settings.FindProperty("_remoteSources"), manager.transform, "RemoteCombat", RemoteChannelCount);
                settings.ApplyModifiedPropertiesWithoutUndo();
                var managerSettings = new SerializedObject(manager);
                managerSettings.FindProperty("_combatSoundPlayer").objectReferenceValue = player;
                managerSettings.ApplyModifiedPropertiesWithoutUndo();
                EditorSceneManager.MarkSceneDirty(scene);
                if (!EditorSceneManager.SaveScene(scene))
                {
                    throw new IOException($"{name}: 전투 사운드 저장 실패");
                }
            }
            AssetDatabase.SaveAssets();
            Debug.Log("COMBAT_SOUND_SETUP_OK");
        }
        finally
        {
            if (previous.Length > 0)
            {
                EditorSceneManager.RestoreSceneManagerSetup(previous);
            }
        }
    }

    private static void SetChannels(SerializedProperty field, Transform parent, string prefix, int count)
    {
        field.arraySize = count;
        for (int i = 0; i < count; i++)
        {
            string name = $"{prefix}{i + 1}";
            Transform existing = parent.Find(name);
            AudioSource source;
            if (existing == null)
            {
                var root = new GameObject(name, typeof(AudioSource));
                root.transform.SetParent(parent, false);
                source = root.GetComponent<AudioSource>();
            }
            else
            {
                source = existing.GetComponent<AudioSource>();
            }
            source.playOnAwake = false;
            source.loop = false;
            source.spatialBlend = 0f;
            source.priority = prefix == "LocalCombat" ? 96 : 160;
            AudioSettingsSetup.AssignOutput(source);
            field.GetArrayElementAtIndex(i).objectReferenceValue = source;
        }
    }
}
