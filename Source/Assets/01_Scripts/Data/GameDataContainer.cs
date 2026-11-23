using System;
using System.Collections.Generic;
using UnityEngine;

public class StringData
{
    //텍스트를 찾을 키
    public string Key;

    //실제 출력될 대사
    public string Value;
}

/// <summary>
/// 아이템 종류
/// </summary>
public enum GameItemType
{
    None,
    Equipment,
    Consumable,
    Etc,
    Gold,

}

/// <summary>
/// 장비템 장착 위치
/// </summary>
public enum EquipSlot
{
    None,
    Weapon,
    Helmet,
    Armor,
    Accessory
}

/// <summary>
/// 플레이어 스탯 종류
/// </summary>
public enum StatType
{
    MaxHp,
    MaxMp,
    Atk,
    Def,
    Str,
    Dex,
    Int
}

public enum PlayerClass
{
    None = 0,
    Warrior = 1,
    Archer = 2,
    Magician = 3
}

/// <summary>
/// 캐릭터 슬롯 상태
/// </summary>
public enum CharacterSlotState
{
    Empty = 0,
    Available = 1,
    BackupAvailable = 2,
    Corrupted = 3
}

public enum CharacterSaveReadResult
{
    Success = 0,
    BackupSuccess = 1,
    NotFound = 2,
    Failed = 3
}





/// <summary>
/// 임시 아이템 데이터 형식
/// </summary>
[Serializable]
public class ItemData
{
    public int ID;
    public string Name;
    public string Description;

    //장비 시스템 필드
    public string ItemType;
    public string EquipSlot;

    public float AddHp;
    public float AddAtk;
    public float AddDef;
    public float AddStat;

    public string SpriteId;
    public string IconId;
    public int Price;
    public int ReqLevel;

    // 소비 아이템 회복 수치, 장비의 최대 체력 증가와 분리
    public float RestoreHp;
    public float RestoreMp;

    // 최대 체력·마나 기준 회복 비율, 0~1 범위
    public float RestoreHpRatio;
    public float RestoreMpRatio;

    // 재사용 대기 초, 구형 시트·캐시의 누락 값은 로컬 기본값으로 보완
    public float Cooldown = -1f;

    public GameItemType GetItemType() => Enum.TryParse(ItemType, true, out GameItemType result) ? result : GameItemType.None;

    public EquipSlot GetEquipSlot() => Enum.TryParse(EquipSlot, true, out EquipSlot result) ? result : global::EquipSlot.None;

    public Dictionary<StatType, float> GetStatModifies(StatType mainStat)
    {
        if (!PlayerClassStatMap.IsMainStatType(mainStat))
        {
            throw new ArgumentOutOfRangeException(nameof(mainStat), mainStat, "주 능력치가 아닙니다.");
        }

        return new Dictionary<StatType, float>
    {
        { StatType.MaxHp, AddHp },
        { StatType.Atk, AddAtk },
        { StatType.Def, AddDef },
        { mainStat, AddStat }
    };
    }
}

/// <summary>
/// 인벤토리 한 칸의 아이템 정보를 담는 클래스
/// </summary>
[Serializable]
public class InventoryItemData
{
    public ItemData ItemData;
    public int Count;

    public int EnhanceLevel;
    public bool IsDestroyed;

    public InventoryItemData(ItemData itemData, int count = 1)
    {
        ItemData = itemData;
        Count = count;

        EnhanceLevel = 0;
        IsDestroyed = false;
    }

    public bool IsEmpty => ItemData == null || Count <= 0;
}

[Serializable]
public class EnhanceData
{
    public int EnhanceLevel;
    public float SuccessRate;
    public float DropRate;
    public float DestroyRate;
    public int Cost;
}

[Serializable]
public class EnhanceStatData
{
    public int ReqLevel;
    public float BaseBonusAtk;
    public float BaseBonusDef;
    public float BaseBonusHp;
    public float BaseBonusStat;

    /// <summary>
    /// 장비 부위별 강화 보너스 계산
    /// 무기는 공격력, 방어구는 체력·방어력, 투구는 주 능력치 추가
    /// </summary>
    public Dictionary<StatType, float> GetEnhanceBonusModifies(EquipSlot slot, int enhanceLevel, StatType mainStat)
    {
        Dictionary<StatType, float> bonuses = new();

        switch (slot)
        {
            case EquipSlot.Weapon:
                bonuses[StatType.Atk] = BaseBonusAtk * enhanceLevel;
                break;

            case EquipSlot.Armor:
            case EquipSlot.Helmet:
                bonuses[StatType.MaxHp] = BaseBonusHp * enhanceLevel;
                bonuses[StatType.Def] = BaseBonusDef * enhanceLevel;

                if (slot == EquipSlot.Helmet)
                {
                    bonuses[mainStat] = BaseBonusStat * enhanceLevel;
                }
                break;
        }

        return bonuses;
    }
}

[Serializable]
public class PortalData
{
    public int ID;
    public string TargetScene;
    public int TargetPortalID;
}



[Serializable]
public class PlayerStatData
{
    public float BaseHp;
    public float BaseMp;
    public float BaseAtk;
    public float BaseDef;
    public float BaseStat;
    public float MainStatCoefficient;
}

/// <summary>
/// 플레이어 이동 관련 데이터
/// </summary>
[Serializable]
public class PlayerMoveData
{
    #region Move

    /// <summary>
    /// 이동 속도
    /// </summary>
    public float MoveSpeed;

    #endregion

    #region Jump

    /// <summary>
    /// 최대 점프 횟수
    /// </summary>
    public int JumpCnt;

    /// <summary>
    /// 일반 점프 높이
    /// </summary>
    public float NormalJumpPower;

    /// <summary>
    /// 위 점프 높이
    /// </summary>
    public float HighJumpPower;

    /// <summary>
    /// 더블 점프 x축 파워
    /// </summary>
    public float DoubleJumpForwardPower;

    /// <summary>
    /// 더블 점프 y축 파워
    /// </summary>
    public float DoubleJumpUpPower;

    /// <summary>
    /// 밑점프 시작 속도
    /// </summary>
    public float DownJumpPower;
    #endregion
}

/// <summary>
/// 순간이동이나 강제이동 관련 데이터
/// </summary>
[Serializable]
public class TeleportData
{
    /// <summary>
    /// 텔레포트 거리
    /// </summary>
    public float TeleportRange;

    /// <summary>
    /// 텔레포트 각도
    /// </summary>
    public float TeleportAngle;


}

/// <summary>
/// 스킬 원본 데이터와 파싱된 타격 데이터
/// </summary>
[Serializable]
public sealed class SkillData
{
    public int ID;
    public string SkillName;
    public string PlayerClass;
    public string ExecutionType;
    public string DamageFormat;
    public string TargetFormat;
    public string RangeFormat;
    public string OffsetFormat;
    public float HitInterval;
    public float Cooldown;
    public float MpCost;
    public float ProjectileSpeed;
    public float ProjectileLifetime;
    public float MoveSpeed;
    public float MoveDuration;

    [NonSerialized] public int[] MaxTargets = Array.Empty<int>();
    [NonSerialized] public float[] DamagePercents = Array.Empty<float>();
    [NonSerialized] public Vector2[] AttackRanges = Array.Empty<Vector2>();
    [NonSerialized] public Vector2[] AttackOffsets = Array.Empty<Vector2>();

    public SkillData()
    {
    }

    public SkillData(int id, string name, int[] targets, float[] percents, Vector2[] ranges, Vector2[] offsets)
    {
        ID = id;
        SkillName = name;
        MaxTargets = targets;
        DamagePercents = percents;
        AttackRanges = ranges;
        AttackOffsets = offsets;
    }

    /// <summary>
    /// 시트 문자열의 런타임 타격 데이터 변환
    /// </summary>
    public void InitializeRuntimeValues()
    {
        MaxTargets = SkillParser.ParseIntArray(TargetFormat);
        DamagePercents = SkillParser.ParseFloatArray(DamageFormat);
        AttackRanges = SkillParser.ParseVector2Array(RangeFormat);
        AttackOffsets = SkillParser.ParseVector2Array(OffsetFormat);

        int hitCount = DamagePercents.Length;

        if (MaxTargets.Length != hitCount || AttackRanges.Length != hitCount || AttackOffsets.Length != hitCount)
        {
            throw new InvalidOperationException($"{SkillName}: 스킬 타격 데이터 배열 길이 불일치");
        }
    }
}

