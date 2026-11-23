using System;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 서버 플레이어 프리팹의 네트워크 스킬 연출 연결
/// 기존 이펙트 설정과 프리팹 GUID 유지
/// </summary>
public static class NetworkSkillPresentationSetup
{
    private const string PrefabPath = "Assets/Resources/Prefabs/Character/ServerPlayer.prefab";

    [MenuItem("Tools/Network/Configure Skill Presentation")]
    public static void Configure()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            throw new InvalidOperationException("플레이 종료 후 네트워크 연출 구성 필요");
        }

        GameObject root = PrefabUtility.LoadPrefabContents(PrefabPath);
        try
        {
            NetworkSkillPresentation presentation = root.GetComponent<NetworkSkillPresentation>();
            if (presentation == null)
            {
                presentation = root.AddComponent<NetworkSkillPresentation>();
            }

            var settings = new SerializedObject(presentation);
            SetReference<PlayerAreaSkillExecutor>(settings, root, "_areaExecutor");
            SetReference<PlayerDashSkillExecutor>(settings, root, "_dashExecutor");
            SetReference<AreaSkillPresentation>(settings, root, "_areaPresentation");
            SetReference<DashSkillPresentation>(settings, root, "_dashPresentation");
            settings.ApplyModifiedPropertiesWithoutUndo();
            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
        Debug.Log("NETWORK_SKILL_PRESENTATION_SETUP_OK");
    }

    private static void SetReference<T>(SerializedObject settings, GameObject root, string field) where T : Component
    {
        T component = root.GetComponent<T>();
        if (component == null)
        {
            throw new MissingReferenceException($"{root.name}: {typeof(T).Name} 참조 필요");
        }
        settings.FindProperty(field).objectReferenceValue = component;
    }
}
