using System;
using UnityEngine;

/// <summary>
/// 게임 데이터 스냅샷 JSON 변환
/// </summary>
public sealed class GameDataSnapshotSerializer
{
    private const int LegacySchemaVersion = 1;
    private const int BossReviveRulesSchemaVersion = 2;
    private const int HuntDifficultySchemaVersion = 3;
    private const int HuntSpawnSchemaVersion = 4;

    /// <summary>
    /// 스냅샷 JSON 직렬화
    /// </summary>
    public string Serialize(GameDataSnapshot snapshot, bool prettyPrint = true)
    {
        return JsonUtility.ToJson(snapshot, prettyPrint);
    }

    /// <summary>
    /// JSON 스냅샷 역직렬화
    /// </summary>
    public bool TryDeserialize(string json, out GameDataSnapshot snapshot, out string errorMessage)
    {
        snapshot = null;

        if (string.IsNullOrWhiteSpace(json))
        {
            return Fail("게임 데이터 캐시 JSON이 비어 있습니다.", out errorMessage);
        }

        try
        {
            snapshot = JsonUtility.FromJson<GameDataSnapshot>(json);
        }
        catch (ArgumentException exception)
        {
            return Fail($"게임 데이터 캐시 JSON 형식이 올바르지 않습니다. {exception.Message}", out errorMessage);
        }

        if (snapshot == null)
        {
            return Fail("게임 데이터 캐시에서 스냅샷을 생성하지 못했습니다.", out errorMessage);
        }

        UpgradeLegacySnapshot(snapshot);
        errorMessage = string.Empty;
        return true;
    }

    /// <summary>구형 캐시의 보스 부활과 사냥터 입장·배치 규칙 보완</summary>
    private static void UpgradeLegacySnapshot(GameDataSnapshot snapshot)
    {
        if (snapshot.SchemaVersion == LegacySchemaVersion)
        {
            if (snapshot.Bosses != null)
            {
                foreach (BossData boss in snapshot.Bosses)
                {
                    if (boss == null) { continue; }
                    boss.InitialDeathCount = BossData.DefaultInitialDeathCount;
                    boss.ReviveInvincibleDuration = BossData.DefaultReviveInvincibleDuration;
                }
            }

            snapshot.SchemaVersion = BossReviveRulesSchemaVersion;
        }

        if (snapshot.SchemaVersion == BossReviveRulesSchemaVersion)
        {
            snapshot.HuntDifficulties = new System.Collections.Generic.List<HuntDifficultyData>(
                new LocalGameDataProvider().CreateHuntDifficultyTable().Values);
            snapshot.SchemaVersion = HuntDifficultySchemaVersion;
        }

        if (snapshot.SchemaVersion == HuntDifficultySchemaVersion)
        {
            snapshot.HuntSpawns = new LocalGameDataProvider().CreateHuntSpawnRows();
            snapshot.SchemaVersion = HuntSpawnSchemaVersion;
        }
    }

    private static bool Fail(string message, out string errorMessage)
    {
        errorMessage = message;
        return false;
    }
}
