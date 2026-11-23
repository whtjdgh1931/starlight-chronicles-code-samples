using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

/// <summary>
/// 플레이어의 능력치를 관리하는 클래스
/// </summary>
public class PlayerStat : MonoBehaviour, IAttackPowerProvider
{
    #region Field
    private PlayerClass _class;
    public PlayerClass Class => _class;
    public StatType MainStat => PlayerClassStatMap.GetMainStat(_class);

    private Dictionary<StatType, float> _baseStat;
    private float _initialMaxHp;
    private float _initialMaxMp;
    private float _initialAtk;
    private float _initialDef;

    private Dictionary<StatType, float> _bonusStat;
    public float MainStatCoefficient => _mainStatCoefficient;


    private int _level = 1;
    public int Level => _level;


    private float _currentExp;
    public float CurrentExp => _currentExp;
    [SerializeField] private float _initialMaxExp = 100f;
    private float _maxExp;
    public float MaxExp => _maxExp;


    private float _currentHp;
    public float CurrentHp => _currentHp;

    private float _currentMp;
    public float CurrentMp => _currentMp;


    private PlayerEquipment _playerEquipment;
    private DataManager _dataManager;

    private float _mainStatCoefficient;
    [Header("공격력 배율")]
    [Tooltip("버프 등을 반영하는 최종 공격력 배율")]
    [FormerlySerializedAs("_testAttackMultiplier")]
    [FormerlySerializedAs("_debugAttackMultiplier")]
    [SerializeField, Min(0f)] private float _attackMultiplier = 1f;

    public float EffectiveAttack => (GetTotalStat(StatType.Atk) + GetTotalStat(MainStat) * _mainStatCoefficient) * _attackMultiplier;

    [Header("방어력 설정")]
    [Tooltip("피해 감소율 50%에 필요한 방어력")]
    [SerializeField, Min(1f)] private float _defenseScale = 600f;


    [Header("레벨업 증가량")]
    [SerializeField] private float _hpPerLevel = 100f;
    [SerializeField] private float _mpPerLevel = 50f;
    [SerializeField] private float _atkPerLevel = 2f;
    [SerializeField] private float _defPerLevel = 1f;

    public bool IsInitialized => _baseStat != null && _bonusStat != null;

    /// <summary>
    /// 해당 캐릭터의 현재 체력과 최대 체력 변경 알림
    /// </summary>
    public event Action<float, float> HpChanged;

    /// <summary>
    /// 해당 캐릭터의 현재 마나와 최대 마나 변경 알림
    /// </summary>
    public event Action<float, float> MpChanged;

    /// <summary>
    /// 해당 캐릭터의 현재 경험치와 최대 경험치 변경 알림
    /// </summary>
    public event Action<float, float> ExpChanged;

    /// <summary>
    /// 해당 캐릭터의 레벨 변경 알림
    /// </summary>
    public event Action<int> LevelChanged;

    /// <summary>
    /// 해당 캐릭터의 최종 능력치 변경 알림
    /// </summary>
    public event Action<Dictionary<StatType, float>> StatsChanged;
    #endregion

    public void Initialize(PlayerStatData statData, PlayerClass playerClass, PlayerEquipment playerEquipment, DataManager dataManager)
    {
        if (playerClass == PlayerClass.None || !Enum.IsDefined(typeof(PlayerClass), playerClass))
        {
            throw new ArgumentOutOfRangeException(nameof(playerClass));
        }

        if (statData == null)
        {
            throw new ArgumentNullException(nameof(statData));
        }

        // 재사용 전 이전 장비 객체의 능력치 이벤트 연결 해제
        if (_playerEquipment != null)
        {
            _playerEquipment.EquipmentStatsChanged -= CalculateEquipStats;
        }

        _class = playerClass;
        _attackMultiplier = 1f;
        _playerEquipment = playerEquipment ?? throw new ArgumentNullException(nameof(playerEquipment));
        _dataManager = dataManager ?? throw new ArgumentNullException(nameof(dataManager));
        _initialMaxHp = statData.BaseHp;
        _initialMaxMp = statData.BaseMp;
        _initialAtk = statData.BaseAtk;
        _initialDef = statData.BaseDef;
        _baseStat = new Dictionary<StatType, float>
{
    { StatType.MaxHp, _initialMaxHp },
    { StatType.MaxMp, _initialMaxMp },
    { StatType.Atk, _initialAtk },
    { StatType.Def, _initialDef },
    { StatType.Str, 0f },
    { StatType.Dex, 0f },
    { StatType.Int, 0f }
};

        _baseStat[MainStat] = statData.BaseStat;

        _bonusStat = new Dictionary<StatType, float>();

        if (float.IsNaN(statData.MainStatCoefficient) || float.IsInfinity(statData.MainStatCoefficient) || statData.MainStatCoefficient <= 0f)
        {
            throw new ArgumentOutOfRangeException(nameof(statData.MainStatCoefficient), statData.MainStatCoefficient, "주 능력치 공격력 계수는 0보다 커야 합니다.");
        }
        _mainStatCoefficient = statData.MainStatCoefficient;

        _level = 1;
        _maxExp = _initialMaxExp;
        _currentHp = GetTotalStat(StatType.MaxHp);
        _currentMp = GetTotalStat(StatType.MaxMp);
        _currentExp = 0f;

        _playerEquipment.EquipmentStatsChanged -= CalculateEquipStats;

        _playerEquipment.EquipmentStatsChanged += CalculateEquipStats;

        PublishAllStats();
    }

    /// <summary>
    /// 최종 스탯 반환(BaseStat + bonusStat)
    /// </summary>
    /// <param Name="statType"></param>
    /// <returns></returns>
    public float GetTotalStat(StatType statType)
    {
        _baseStat.TryGetValue(statType, out float baseStat);
        _bonusStat.TryGetValue(statType, out float bonusStat);
        return baseStat + bonusStat;
    }

    /// <summary>
    /// 장비·레벨·강화 방어력을 반영한 피격 피해 계산
    /// </summary>
    public float CalculateReceivedDamage(float incomingDamage)
    {
        float defense = GetTotalStat(StatType.Def);
        return incomingDamage * _defenseScale / (_defenseScale + defense);
    }

    /// <summary>
    /// hp변경
    /// </summary>
    /// <param name="changeHp"></param>
    public void ChangeHp(float changeHp)
    {
        _currentHp = Mathf.Clamp(_currentHp + changeHp, 0f, GetTotalStat(StatType.MaxHp));
        HpChanged?.Invoke(_currentHp, GetTotalStat(StatType.MaxHp));
    }

    /// <summary>
    /// 서버 체력 수신값 반영과 캐릭터별 UI 알림
    /// 원격 캐릭터의 능력치 초기화 없이 사용 가능한 표시 경로
    /// </summary>
    public void ApplyReplicatedHp(float currentHp, float maxHp)
    {
        _currentHp = currentHp;
        HpChanged?.Invoke(currentHp, maxHp);
    }

    /// <summary>
    /// 소유자에게 전달된 서버 마나 반영과 본인 HUD 알림
    /// </summary>
    public void ApplyReplicatedMp(float currentMp, float maxMp)
    {
        _currentMp = currentMp;
        MpChanged?.Invoke(currentMp, maxMp);
    }

    public void ChangeMp(float changeMp)
    {
        _currentMp = Mathf.Clamp(_currentMp + changeMp, 0f, GetTotalStat(StatType.MaxMp));
        MpChanged?.Invoke(_currentMp, GetTotalStat(StatType.MaxMp));
    }

    /// <summary>경험치 획득과 99레벨까지의 성장</summary>
    public void AddExp(float expAmount)
    {
        if (float.IsNaN(expAmount) || float.IsInfinity(expAmount) || expAmount < 0f)
        {
            throw new ArgumentOutOfRangeException(nameof(expAmount), "경험치는 유한한 0 이상 값 필요");
        }
        if (_level >= Constants.MaxPlayerLevel)
        {
            return;
        }

        _currentExp = (float)Math.Min(float.MaxValue, (double)_currentExp + expAmount);

        bool hasLeveledUp = false;

        while (_level < Constants.MaxPlayerLevel && _currentExp >= _maxExp)
        {
            float overExp = _currentExp - _maxExp;

            LevelUp();

            _currentExp = overExp * Constants.FLOAT_ExpPenalty;

            hasLeveledUp = true;
        }

        if (_level == Constants.MaxPlayerLevel) { _currentExp = 0f; }

        if (hasLeveledUp)
        {
            PublishAllStats();
        }
        else
        {
            ExpChanged?.Invoke(_currentExp, _maxExp);
        }
    }

    /// <summary>
    /// 검증된 저장 데이터의 레벨·경험치·HP·MP 복원
    /// </summary>
    public void RestoreProgress(PlayerSaveData saveData)
    {
        if (saveData.Level < 1 || saveData.Level > Constants.MaxPlayerLevel)
        {
            throw new ArgumentOutOfRangeException(nameof(saveData), $"복원 레벨 범위는 1~{Constants.MaxPlayerLevel}");
        }
        _level = saveData.Level;
        _maxExp = CalculateMaxExp(_level);
        RecalculateLevelStats();

        _currentExp = _level == Constants.MaxPlayerLevel ? 0f : saveData.CurrentExp;
        _currentHp = saveData.CurrentHp;
        _currentMp = saveData.CurrentMp;
    }

    public void CompleteRestore()
    {
        _currentExp = Mathf.Clamp(_currentExp, 0f, _maxExp);
        _currentHp = Mathf.Clamp(_currentHp, 0f, GetTotalStat(StatType.MaxHp));
        _currentMp = Mathf.Clamp(_currentMp, 0f, GetTotalStat(StatType.MaxMp));

        PublishAllStats();
    }

    /// <summary>
    /// 스탯 재계산
    /// </summary>
    public void CalculateEquipStats()
    {
        RecalculateEquipStats();

        // 장비 해제·강화 하락·파괴 이후 최대 체력 초과 방지
        _currentHp = Mathf.Min(_currentHp, GetTotalStat(StatType.MaxHp));

        HpChanged?.Invoke(_currentHp, GetTotalStat(StatType.MaxHp));
        MpChanged?.Invoke(_currentMp, GetTotalStat(StatType.MaxMp));
        StatsChanged?.Invoke(GetAllCurrentStats());
    }

    public void PublishAllStats()
    {
        LevelChanged?.Invoke(_level);
        HpChanged?.Invoke(_currentHp, GetTotalStat(StatType.MaxHp));
        MpChanged?.Invoke(_currentMp, GetTotalStat(StatType.MaxMp));
        ExpChanged?.Invoke(_currentExp, _maxExp);
        StatsChanged?.Invoke(GetAllCurrentStats());
    }

    private void LevelUp()
    {
        if (_level >= Constants.MaxPlayerLevel) { return; }
        _level++;
        _maxExp = Mathf.Round(_maxExp * Constants.FLOAT_ExpMultiplier);

        RecalculateLevelStats();

        _currentHp = GetTotalStat(StatType.MaxHp);
        _currentMp = GetTotalStat(StatType.MaxMp);

    }

    /// <summary>
    /// 1레벨 기본값과 현재 레벨을 기준으로 성장 능력치 계산
    /// </summary>
    private void RecalculateLevelStats()
    {
        int levelUpCount = _level - 1;

        _baseStat[StatType.MaxHp] = _initialMaxHp + _hpPerLevel * levelUpCount;
        _baseStat[StatType.MaxMp] = _initialMaxMp + _mpPerLevel * levelUpCount;
        _baseStat[StatType.Atk] = _initialAtk + _atkPerLevel * levelUpCount;
        _baseStat[StatType.Def] = _initialDef + _defPerLevel * levelUpCount;
    }

    private float CalculateMaxExp(int level)
    {
        float maxExp = _initialMaxExp;

        for (int currentLevel = 1; currentLevel < level; currentLevel++)
        {
            maxExp = Mathf.Round(maxExp * Constants.FLOAT_ExpMultiplier);
        }

        return maxExp;
    }

    /// <summary>
    /// 장비에 의한 스탯 계산
    /// </summary>
    private void RecalculateEquipStats()
    {
        _bonusStat.Clear();

        foreach (InventoryItemData inventoryItem in _playerEquipment.EquippedItems)
        {
            GetEquipmentBonuses(inventoryItem, out var baseStats, out var enhancementStats);
            AddEquipmentBonuses(baseStats);
            AddEquipmentBonuses(enhancementStats);
        }
    }

    /// <summary>현재 직업 기준 장비 한 개의 기본·강화 능력치 조회</summary>
    internal void GetEquipmentBonuses(InventoryItemData equipment, out IReadOnlyDictionary<StatType, float> baseStats,
        out IReadOnlyDictionary<StatType, float> enhancementStats)
    {
        ItemData item = equipment.ItemData;
        baseStats = item.GetStatModifies(MainStat);
        EnhanceStatData enhancement = equipment.EnhanceLevel > 0 ? _dataManager.GetEnhanceStat(item.ReqLevel) : null;
        enhancementStats = enhancement == null
            ? EmptyEquipmentBonuses
            : enhancement.GetEnhanceBonusModifies(item.GetEquipSlot(), equipment.EnhanceLevel, MainStat);
    }

    private static readonly IReadOnlyDictionary<StatType, float> EmptyEquipmentBonuses = new Dictionary<StatType, float>();

    private void AddEquipmentBonuses(IReadOnlyDictionary<StatType, float> bonuses)
    {
        foreach (KeyValuePair<StatType, float> bonus in bonuses)
        {
            _bonusStat.TryGetValue(bonus.Key, out float current);
            _bonusStat[bonus.Key] = current + bonus.Value;
        }
    }

    private Dictionary<StatType, float> GetAllCurrentStats()
    {
        var statsDict = new Dictionary<StatType, float>();
        foreach (StatType type in Enum.GetValues(typeof(StatType)))
        {
            statsDict[type] = GetTotalStat(type);
        }
        return statsDict;
    }

    private void OnDestroy()
    {
        if (_playerEquipment != null)
        {
            _playerEquipment.EquipmentStatsChanged -= CalculateEquipStats;
        }
    }



    #region Test
    public void ForceLevelUp()
    {
        LevelUp();

        _currentExp = 0f;

        PublishAllStats();
    }
    #endregion
}

