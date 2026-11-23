using System;
using System.Collections.Generic;
using System.Reflection;

/// <summary>
/// 로컬 게임 데이터 스냅샷의 필수 데이터와 식별자 검증
/// </summary>
public sealed class GameDataSnapshotValidator
{
    /// <summary>
    /// 스냅샷 사용 가능 여부 검증
    /// </summary>
    public bool TryValidate(GameDataSnapshot snapshot, out string errorMessage)
    {
        if (snapshot == null)
        {
            return Fail("게임 데이터 스냅샷이 없습니다.", out errorMessage);
        }

        if (snapshot.SchemaVersion != GameDataSnapshot.CurrentSchemaVersion)
        {
            return Fail(
                $"지원하지 않는 게임 데이터 스키마입니다. " +
                $"현재: {snapshot.SchemaVersion}, 필요: {GameDataSnapshot.CurrentSchemaVersion}",
                out errorMessage);
        }

        if (snapshot.PlayerMoveData == null)
        {
            return Fail("PlayerMove 필수 데이터 누락", out errorMessage);
        }
        if (!TryValidateNumbers(snapshot.PlayerMoveData, "PlayerMove", out errorMessage)) { return false; }

        PlayerMoveData move = snapshot.PlayerMoveData;
        if (move.MoveSpeed <= 0f || move.JumpCnt < 1 || move.NormalJumpPower <= 0f ||
            move.HighJumpPower <= 0f || move.DoubleJumpForwardPower < 0f || move.DoubleJumpUpPower < 0f)
        {
            return Fail("PlayerMove MoveSpeed·JumpCnt·일반/위 점프는 양수, 더블 점프는 0 이상 필요", out errorMessage);
        }
        if (snapshot.PlayerStatData == null)
        {
            return Fail("PlayerStat 필수 데이터 누락", out errorMessage);
        }
        if (!TryValidateNumbers(snapshot.PlayerStatData, "PlayerStat", out errorMessage)) { return false; }

        PlayerStatData stats = snapshot.PlayerStatData;
        if (stats.BaseHp <= 0f || stats.MainStatCoefficient <= 0f || stats.BaseMp < 0f ||
            stats.BaseAtk < 0f || stats.BaseDef < 0f || stats.BaseStat < 0f)
        {
            return Fail("PlayerStat BaseHp·MainStatCoefficient는 양수, 나머지 기본 능력치는 0 이상 필요", out errorMessage);
        }

        if (!TryValidateTable(snapshot.Monsters, data => data.ID, "몬스터", out errorMessage) ||
            !TryValidateTable(snapshot.MonsterAttacks, data => data.PatternId, "몬스터 공격", out errorMessage) ||
            !TryValidateTable(snapshot.Bosses, data => data.BossId, "보스", out errorMessage) ||
            !TryValidateTable(snapshot.BossPhaseAttacks, data => data.PhaseAttackId, "보스 페이즈 공격", out errorMessage) ||
            !TryValidateTable(snapshot.Skills, data => data.ID, "스킬", out errorMessage) ||
            !TryValidateTable(snapshot.Items, data => data.ID, "아이템", out errorMessage) ||
            !TryValidateTable(snapshot.EnhanceProbabilities, data => data.EnhanceLevel, "강화 확률", out errorMessage) ||
            !TryValidateTable(snapshot.EnhanceStats, data => data.ReqLevel, "강화 능력치", out errorMessage) ||
            !TryValidateTable(snapshot.Strings, data => data.Key, "문자열", out errorMessage) ||
            !TryValidateTable(snapshot.Portals, data => data.ID, "포탈", out errorMessage))
        {
            return false;
        }

        if (!TryValidateBossReviveRules(snapshot.Bosses, out errorMessage)) { return false; }
        if (!TryValidateHuntDifficulties(snapshot.HuntDifficulties, out errorMessage)) { return false; }
        if (!TryValidateHuntSpawns(snapshot, out errorMessage)) { return false; }

        var itemsById = new Dictionary<int, ItemData>();
        foreach (ItemData item in snapshot.Items) { itemsById.Add(item.ID, item); }
        foreach (MonsterData monster in snapshot.Monsters)
        {
            if (!monster.TryValidateDrops(out errorMessage)) { return false; }
            foreach (int itemId in monster.GetDropItemIDs())
            {
                if (!itemsById.TryGetValue(itemId, out ItemData item) || item.GetItemType() is GameItemType.None or GameItemType.Gold)
                {
                    return Fail($"몬스터 {monster.ID} 컬럼 DropItemIDs 값 {itemId}: 유효한 드롭 아이템 정의 필요", out errorMessage);
                }
            }
        }

        foreach (GameDataStringEntry entry in snapshot.Strings)
        {
            if (string.IsNullOrWhiteSpace(entry.Key))
            {
                return Fail("문자열 데이터에 비어 있는 키가 있습니다.", out errorMessage);
            }
            if (entry.Key.StartsWith("Boss_", StringComparison.Ordinal) && string.IsNullOrWhiteSpace(entry.Value))
            {
                return Fail($"StringData {entry.Key}: Value 컬럼에 표시 문구 필요", out errorMessage);
            }
            object[] formatArguments = entry.Key switch
            {
                Constants.STR_Boss_RemainingRevivesFormat or Constants.STR_Boss_OnlineResultWaitingFormat or
                    Constants.STR_Boss_OnlineRewardForfeitButtonFormat => new object[] { 5 },
                Constants.STR_Boss_OnlineResultSummaryFormat => new object[] { 25.5f, 1234, 3, 2 },
                Constants.STR_Boss_OnlineRewardRowFormat => new object[] { "아이템", 2, "수령 완료" },
                _ => null
            };
            if (formatArguments != null && !HasRequiredFormatArguments(entry.Value, formatArguments))
            {
                return Fail($"StringData {entry.Key}: Value 컬럼에 자리표시자 0~{formatArguments.Length - 1} 모두 필요", out errorMessage);
            }
        }

        errorMessage = string.Empty;
        return true;
    }

    /// <summary>유효한 형식과 모든 필수 인자의 표시 여부 확인</summary>
    private static bool HasRequiredFormatArguments(string format, object[] arguments)
    {
        try
        {
            string complete = string.Format(format, arguments);
            for (int i = 0; i < arguments.Length; i++)
            {
                object argument = arguments[i];
                arguments[i] = null;
                string withoutArgument = string.Format(format, arguments);
                arguments[i] = argument;
                if (withoutArgument == complete) { return false; }
            }
            return true;
        }
        catch (FormatException)
        {
            return false;
        }
    }

    /// <summary>보스 데스카운터와 부활 무적 범위 검증</summary>
    private static bool TryValidateBossReviveRules(IReadOnlyList<BossData> bosses, out string errorMessage)
    {
        foreach (BossData boss in bosses)
        {
            if (boss.InitialDeathCount < 1)
            {
                return Fail($"Boss ID {boss.BossId}: InitialDeathCount는 1 이상이어야 합니다.", out errorMessage);
            }
            if (boss.ReviveInvincibleDuration < 0f)
            {
                return Fail($"Boss ID {boss.BossId}: ReviveInvincibleDuration은 0 이상이어야 합니다.", out errorMessage);
            }
        }

        errorMessage = string.Empty;
        return true;
    }

    /// <summary>사냥터 난이도의 식별자·표시 이름·입장 레벨 검증</summary>
    private static bool TryValidateHuntDifficulties(IReadOnlyList<HuntDifficultyData> options, out string errorMessage)
    {
        if (!TryValidateTable(options, entry => entry.Difficulty, Constants.HuntDifficultySheet, out errorMessage))
        {
            return false;
        }

        var byDifficulty = new Dictionary<HuntDifficulty, HuntDifficultyData>();
        foreach (HuntDifficultyData option in options)
        {
            if (!Enum.IsDefined(typeof(HuntDifficulty), option.Difficulty) ||
                string.IsNullOrWhiteSpace(option.Name) ||
                option.RequiredLevel < 1 || option.RequiredLevel > Constants.MaxPlayerLevel)
            {
                return Fail($"HuntDifficulty {option.Difficulty}: 이름과 유효한 입장 레벨 필요", out errorMessage);
            }
            byDifficulty.Add(option.Difficulty, option);
        }

        if (!byDifficulty.TryGetValue(HuntDifficulty.Beginner, out HuntDifficultyData beginner) ||
            !byDifficulty.TryGetValue(HuntDifficulty.Intermediate, out HuntDifficultyData intermediate) ||
            !byDifficulty.TryGetValue(HuntDifficulty.Advanced, out HuntDifficultyData advanced))
        {
            return Fail("HuntDifficulty에 초급·중급·상급이 각각 필요합니다.", out errorMessage);
        }
        if (beginner.RequiredLevel > intermediate.RequiredLevel || intermediate.RequiredLevel > advanced.RequiredLevel)
        {
            return Fail("사냥터 입장 레벨은 난이도 순서대로 같거나 높아야 합니다.", out errorMessage);
        }

        errorMessage = string.Empty;
        return true;
    }

    /// <summary>사냥터 배치의 조합·몬스터 정의·생성 수치 검증</summary>
    private static bool TryValidateHuntSpawns(GameDataSnapshot snapshot, out string errorMessage)
    {
        if (!TryValidateTable(snapshot.HuntSpawns, entry => entry.ID, Constants.HuntSpawnSheet, out errorMessage))
        {
            return false;
        }

        var monsterIds = new HashSet<int>();
        foreach (MonsterData monster in snapshot.Monsters) { monsterIds.Add(monster.ID); }
        var bossIds = new HashSet<int>();
        foreach (BossData boss in snapshot.Bosses) { bossIds.Add(boss.BossId); }
        var pairs = new HashSet<(HuntDifficulty, HuntSpawnZone)>();
        foreach (HuntSpawnData rule in snapshot.HuntSpawns)
        {
            if (rule.ID <= 0 || !Enum.IsDefined(typeof(HuntDifficulty), rule.Difficulty) ||
                rule.ZoneId == HuntSpawnZone.None || !Enum.IsDefined(typeof(HuntSpawnZone), rule.ZoneId))
            {
                return Fail($"HuntSpawn ID {rule.ID}: 유효한 난이도와 구역 필요", out errorMessage);
            }
            if (!pairs.Add((rule.Difficulty, rule.ZoneId)))
            {
                return Fail($"HuntSpawn {rule.Difficulty}/{rule.ZoneId}: 중복 배치", out errorMessage);
            }
            if (!monsterIds.Contains(rule.MonsterID) || bossIds.Contains(rule.MonsterID))
            {
                return Fail($"HuntSpawn ID {rule.ID}: 일반 몬스터 ID {rule.MonsterID} 확인 필요", out errorMessage);
            }
            if (rule.MaxAlive < 1 || rule.BatchCount < 1 || rule.BatchCount > rule.MaxAlive ||
                rule.SpawnInterval <= 0f || rule.InitialDelay < 0f)
            {
                return Fail($"HuntSpawn ID {rule.ID}: 생성 수량·주기·첫 지연 범위 오류", out errorMessage);
            }
        }

        foreach (HuntDifficulty difficulty in Enum.GetValues(typeof(HuntDifficulty)))
        {
            foreach (HuntSpawnZone zone in Enum.GetValues(typeof(HuntSpawnZone)))
            {
                if (zone != HuntSpawnZone.None && !pairs.Contains((difficulty, zone)))
                {
                    return Fail($"HuntSpawn {difficulty}/{zone}: 배치 누락", out errorMessage);
                }
            }
        }

        errorMessage = string.Empty;
        return true;
    }

    private static bool TryValidateTable<T, TKey>(
        IReadOnlyList<T> entries,
        Func<T, TKey> keySelector,
        string tableName,
        out string errorMessage)
    {
        if (entries == null || entries.Count == 0)
        {
            return Fail($"{tableName} 데이터가 없습니다.", out errorMessage);
        }

        HashSet<TKey> keys = new HashSet<TKey>();

        foreach (T entry in entries)
        {
            if (entry is null)
            {
                return Fail($"{tableName} 데이터에 비어 있는 항목이 있습니다.", out errorMessage);
            }

            TKey key = keySelector(entry);

            if (!keys.Add(key))
            {
                return Fail($"{tableName} 데이터에 중복 식별자가 있습니다: {key}", out errorMessage);
            }
            if (!TryValidateNumbers(entry, $"{tableName} ID {key}", out errorMessage))
            {
                return false;
            }
        }

        errorMessage = string.Empty;
        return true;
    }

    private static bool Fail(string message, out string errorMessage)
    {
        errorMessage = message;
        return false;
    }

    private static bool TryValidateNumbers<T>(T data, string location, out string errorMessage)
    {
        foreach (FieldInfo field in typeof(T).GetFields(BindingFlags.Public | BindingFlags.Instance))
        {
            if (field.IsNotSerialized || field.FieldType != typeof(float)) { continue; }
            float value = (float)field.GetValue(data);
            if (float.IsNaN(value) || float.IsInfinity(value))
            {
                return Fail($"{location} 컬럼 {field.Name} 값 {value}: 유한한 실수 필요", out errorMessage);
            }
        }
        errorMessage = string.Empty;
        return true;
    }
}
