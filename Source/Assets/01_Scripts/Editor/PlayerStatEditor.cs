using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(PlayerStat))]
public sealed class PlayerStatEditor : Editor
{
    private static void DrawClassMappings()
    {
        EditorGUILayout.LabelField(
            "Warrior",
            PlayerClassStatMap
                .GetMainStat(PlayerClass.Warrior)
                .ToString());

        EditorGUILayout.LabelField(
            "Archer",
            PlayerClassStatMap
                .GetMainStat(PlayerClass.Archer)
                .ToString());

        EditorGUILayout.LabelField(
            "Magician",
            PlayerClassStatMap
                .GetMainStat(PlayerClass.Magician)
                .ToString());
    }

    private static void DrawRuntimeStats(
        PlayerStat playerStat)
    {
        float attack =
            playerStat.GetTotalStat(StatType.Atk);

        float strength =
            playerStat.GetTotalStat(StatType.Str);

        float dexterity =
            playerStat.GetTotalStat(StatType.Dex);

        float intelligence =
            playerStat.GetTotalStat(StatType.Int);

        EditorGUILayout.LabelField(
            "현재 직업",
            playerStat.Class.ToString());

        EditorGUILayout.LabelField(
            "주 능력치",
            playerStat.MainStat.ToString());

        EditorGUILayout.Space(4);

        EditorGUILayout.LabelField(
            "ATK",
            attack.ToString("0.##"));

        EditorGUILayout.LabelField(
            "STR",
            FormatStat(
                StatType.Str,
                strength,
                playerStat.MainStat));

        EditorGUILayout.LabelField(
            "DEX",
            FormatStat(
                StatType.Dex,
                dexterity,
                playerStat.MainStat));

        EditorGUILayout.LabelField(
            "INT",
            FormatStat(
                StatType.Int,
                intelligence,
                playerStat.MainStat));

        EditorGUILayout.Space(4);

        EditorGUILayout.LabelField(
            "주스탯 계수",
            playerStat.MainStatCoefficient
                .ToString("0.##"));

        EditorGUILayout.LabelField(
            "유효 공격력",
            playerStat.EffectiveAttack
                .ToString("0.##"));

        EditorGUILayout.HelpBox(
            "유효 공격력 = ATK + " +
            $"{playerStat.MainStat} × 주스탯 계수\n" +
            "다른 두 능력치는 유효 공격력 계산에 사용되지 않습니다.",
            MessageType.None);
    }

    private static string FormatStat(
        StatType statType,
        float value,
        StatType mainStat)
    {
        string suffix =
            statType == mainStat
                ? " (Main)"
                : string.Empty;

        return $"{value:0.##}{suffix}";
    }

    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        PlayerStat playerStat =
            (PlayerStat)target;

        EditorGUILayout.Space(12);
        EditorGUILayout.LabelField(
            "직업 전투 스탯 진단",
            EditorStyles.boldLabel);

        DrawClassMappings();

        EditorGUILayout.Space(8);

        if (!Application.isPlaying)
        {
            EditorGUILayout.HelpBox(
                "런타임 스탯은 Play Mode에서 확인할 수 있습니다.",
                MessageType.Info);

            return;
        }

        if (!playerStat.IsInitialized)
        {
            EditorGUILayout.HelpBox(
                "PlayerStat 초기화를 기다리고 있습니다.",
                MessageType.Info);

            return;
        }

        DrawRuntimeStats(playerStat);
    }
}