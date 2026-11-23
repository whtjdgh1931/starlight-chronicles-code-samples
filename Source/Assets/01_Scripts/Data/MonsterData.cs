using System;
using System.Collections.Generic;
using System.Globalization;

/// <summary>
/// 몬스터 데이터 형식
/// </summary>
[Serializable]
public class MonsterData
{
    public int ID;
    public string Name;
    public float Hp;
    public float Atk;
    public float Speed;
    public float Level;
    public float Exp;


    public float IdleDuration;
    public float PatrolDuration;
    public float ChaseDuration;
    public float TurnDelay;

    public float DetectionRangeX;
    public float DetectionRangeY;
    public float LoseTargetRangeX;
    public float LoseTargetRangeY;

    public bool ContactDamageEnabled;

    public int MinGold;
    public int MaxGold;
    public float GoldDropChance;

    //쉼표로 구분하는 아이템 데이터 묶음
    public string DropItemIDs;
    public string DropItemChances;

    private IReadOnlyList<int> _cachedItemIDs;
    private IReadOnlyList<float> _cachedItemChances;
    private string _parsedDropItemIDs;
    private string _parsedDropItemChances;

    /// <summary>짝 검증을 마친 읽기 전용 드롭 아이템 ID 조회</summary>
    public IReadOnlyList<int> GetDropItemIDs()
    {
        EnsureDropDataParsed();
        return _cachedItemIDs;
    }

    /// <summary>아이템 ID와 같은 순서의 읽기 전용 드롭 확률 조회</summary>
    public IReadOnlyList<float> GetDropItemChances()
    {
        EnsureDropDataParsed();
        return _cachedItemChances;
    }

    /// <summary>드롭 ID·확률의 짝과 수치 검증, 전체 성공 시에만 결과 캐싱</summary>
    public bool TryValidateDrops(out string errorMessage)
    {
        errorMessage = string.Empty;
        if (_cachedItemIDs != null && _parsedDropItemIDs == DropItemIDs && _parsedDropItemChances == DropItemChances) { return true; }

        bool idsEmpty = string.IsNullOrWhiteSpace(DropItemIDs);
        bool chancesEmpty = string.IsNullOrWhiteSpace(DropItemChances);
        if (idsEmpty != chancesEmpty)
        {
            errorMessage = $"몬스터 {ID}: DropItemIDs와 DropItemChances는 함께 비우거나 같은 개수로 지정 필요";
            return false;
        }

        string[] ids = idsEmpty ? Array.Empty<string>() : DropItemIDs.Split(',');
        string[] chances = chancesEmpty ? Array.Empty<string>() : DropItemChances.Split(',');
        if (ids.Length != chances.Length)
        {
            errorMessage = $"몬스터 {ID}: DropItemIDs {ids.Length}개 / DropItemChances {chances.Length}개, 개수 일치 필요";
            return false;
        }

        var parsedIds = new List<int>(ids.Length);
        var parsedChances = new List<float>(chances.Length);
        for (int i = 0; i < ids.Length; i++)
        {
            string idToken = ids[i].Trim();
            string chanceToken = chances[i].Trim();
            if (!int.TryParse(idToken, NumberStyles.Integer, CultureInfo.InvariantCulture, out int id) || id <= 0)
            {
                errorMessage = $"몬스터 {ID} 컬럼 DropItemIDs 항목 {i + 1} 값 '{idToken}': 양의 정수 필요";
                return false;
            }
            if (!float.TryParse(chanceToken, NumberStyles.Float, CultureInfo.InvariantCulture, out float chance) || float.IsNaN(chance) || float.IsInfinity(chance) || chance < 0f || chance > 1f)
            {
                errorMessage = $"몬스터 {ID} 컬럼 DropItemChances 항목 {i + 1} 값 '{chanceToken}': 0~1의 유한한 확률 필요";
                return false;
            }
            parsedIds.Add(id);
            parsedChances.Add(chance);
        }

        _cachedItemIDs = parsedIds.AsReadOnly();
        _cachedItemChances = parsedChances.AsReadOnly();
        _parsedDropItemIDs = DropItemIDs;
        _parsedDropItemChances = DropItemChances;
        return true;
    }

    private void EnsureDropDataParsed()
    {
        if (!TryValidateDrops(out string errorMessage)) { throw new FormatException(errorMessage); }
    }
}
