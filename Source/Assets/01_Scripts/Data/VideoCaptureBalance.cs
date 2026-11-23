#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>촬영용 전투 수치와 별도 캐릭터 저장 경로</summary>
internal static class VideoCaptureBalance
{
    private const string MenuPath = "Tools/Starlight Chronicles/촬영용 밸런스";
    private const string PreferenceKey = "StarlightChronicles.VideoCaptureBalance.Enabled";
    private const int MoonGuardianId = 9001;
    private const float BossHp = 500f;
    private const float BossAttack = 125f;
    private const float BossExperience = 900f;
    private const float HuntHpMultiplier = 0.55f;
    private const float HuntAttackMultiplier = 0.65f;

    internal static bool IsEnabled => EditorPrefs.GetBool(PreferenceKey);

    /// <summary>현재 Play 세션의 사냥터 몬스터와 보스 수치 적용</summary>
    internal static void Apply(IReadOnlyDictionary<int, MonsterData> monsters, IEnumerable<HuntSpawnData> huntSpawns)
    {
        var huntMonsterIds = new HashSet<int>();
        foreach (HuntSpawnData spawn in huntSpawns) { huntMonsterIds.Add(spawn.MonsterID); }

        foreach (int monsterId in huntMonsterIds)
        {
            MonsterData monster = monsters[monsterId];
            float experienceMultiplier = monster.Level < 8f ? 2.75f : monster.Level < 15f ? 1.75f : 1.25f;
            monster.Hp = Mathf.Max(1f, Mathf.Round(monster.Hp * HuntHpMultiplier));
            monster.Atk = Mathf.Max(1f, Mathf.Round(monster.Atk * HuntAttackMultiplier));
            monster.Exp = Mathf.Round(monster.Exp * experienceMultiplier);
        }

        MonsterData boss = monsters[MoonGuardianId];
        boss.Hp = BossHp;
        boss.Atk = BossAttack;
        boss.Exp = BossExperience;
    }

    /// <summary>메인 에디터와 가상 플레이어의 촬영 슬롯 분리</summary>
    internal static string GetCharacterSaveRoot()
    {
        string editorProject = Path.GetFileName(Path.GetDirectoryName(Application.dataPath));
        return Path.Combine(Application.persistentDataPath, "VideoCapture", editorProject);
    }

    [MenuItem(MenuPath)]
    private static void Toggle()
    {
        bool enabled = !IsEnabled;
        EditorPrefs.SetBool(PreferenceKey, enabled);
        Menu.SetChecked(MenuPath, enabled);
        Debug.Log(enabled ? "촬영용 밸런스 켜짐 / 다음 Play부터 별도 촬영 슬롯 사용" : "촬영용 밸런스 꺼짐 / 다음 Play부터 기존 슬롯 사용");
    }

    [MenuItem(MenuPath, true)]
    private static bool ValidateToggle()
    {
        Menu.SetChecked(MenuPath, IsEnabled);
        return !EditorApplication.isPlayingOrWillChangePlaymode;
    }
}
#endif
