using GoogleSheetsToUnity;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using UnityEngine;
#if UNITY_EDITOR
using GoogleSheetsToUnity.ThirdPary;
#endif

public enum GameDataSource
{
    None,
    GoogleSheets,
    LocalCache,
    Local
}

[Serializable]
public struct SheetConfig
{
    /// <summary>
    /// 시트 이름
    /// </summary>
    public string sheetName;

    /// <summary>
    /// 시트 주소
    /// </summary>
    public string spreadsheetID;
}

/// <summary>
/// 데이터 관리를 위한 매니저
/// </summary>
public class DataManager : MonoBehaviour
{
    [Header("Spreadsheets Setting")]
    [SerializeField] private List<SheetConfig> _sheetConfigs = new List<SheetConfig>();

    /// <summary>
    /// 데이터 로딩이 완료되었는지
    /// </summary>
    public bool IsDataLoaded { get; private set; }

    /// <summary>
    /// 시트 이름과 시트 처리를 연결하는 딕셔너리
    /// </summary>
    private Dictionary<string, Action<GstuSpreadSheet>> _sheetMapping;

    private Dictionary<int, EnhanceData> _enhanceProbTable = new();
    public IReadOnlyDictionary<int, EnhanceData> EnhanceProbTable => _enhanceProbTable;

    private List<EnhanceStatData> _enhanceStatList = new();

    /// <summary>
    /// sheet key값 모음
    /// </summary>
    private string[] _targetSheets;

    /// <summary>
    /// 플레이어 데이터
    /// </summary>
    public PlayerMoveData CachedPlayerMoveData { get; private set; } = new PlayerMoveData();
    public PlayerStatData CachedPlayerStatData { get; private set; } = new PlayerStatData();

    /// <summary>
    /// 몬스터 데이터 테이블
    /// </summary>
    private Dictionary<int, MonsterData> _monsterTable = new();
    public IReadOnlyDictionary<int, MonsterData> MonsterTable => _monsterTable;

    private Dictionary<int, MonsterAttackData> _monsterAttackTable = new();
    public IReadOnlyDictionary<int, MonsterAttackData> MonsterAttackTable => _monsterAttackTable;

    private Dictionary<int, BossData> _bossTable = new();
    public IReadOnlyDictionary<int, BossData> BossTable => _bossTable;

    private Dictionary<int, BossPhaseAttackData> _bossPhaseAttackTable = new();
    public IReadOnlyDictionary<int, BossPhaseAttackData> BossPhaseAttackTable => _bossPhaseAttackTable;

    /// <summary>
    /// 스킬 데이터 테이블
    /// </summary>
    private Dictionary<int, SkillData> _skillTable = new Dictionary<int, SkillData>();
    public IReadOnlyDictionary<int, SkillData> SkillTable => _skillTable;

    /// <summary>
    /// 아이템 데이터 테이블
    /// </summary>
    private Dictionary<int, ItemData> _itemTable = new();
    public IReadOnlyDictionary<int, ItemData> ItemTable => _itemTable;

    /// <summary>
    /// 퀘스트 런타임 정의, 전체 데이터 준비 이후 사용
    /// </summary>
    private Dictionary<int, QuestData> _questTable = new();
    public IReadOnlyDictionary<int, QuestData> QuestTable => _questTable;

    /// <summary>
    /// 포탈 데이터 테이블
    /// </summary>
    private Dictionary<int, PortalData> _portalTable = new();
    private Dictionary<int, HuntDifficultyData> _huntDifficultyTable = new();
    private Dictionary<int, HuntSpawnData> _huntSpawnTable = new();


    private int _currentLoadCount = 0;

    private Dictionary<string, string> _stringTable = new();

    private readonly LocalGameDataProvider _localGameDataProvider = new LocalGameDataProvider();
    private GameDataCache _gameDataCache;
    /// <summary>
    /// 모든 데이터 로드 시 발행할 이벤트
    /// </summary>
    public event Action OnAllDataLoaded;

    [SerializeField, Min(1f)]
    private float _googleSheetsTimeoutSeconds = 10f;

    private Coroutine _loadTimeoutCoroutine;
    private int _loadGeneration;
    private bool _hasSheetLoadFailure;
    private bool _isCacheOnlySynchronization;
    private Action<bool, string> _synchronizationCallback;

    public GameDataSource ActiveDataSource { get; private set; }

    /// <summary>
    /// 초기화
    /// </summary>
    public void Initialize()
    {
        ReadAllData();
    }

    private void InitializeDataServices()
    {
        InitializeSheetMapping();
        _gameDataCache ??= new GameDataCache(Application.persistentDataPath);
    }

    private void InitializeSheetMapping()
    {
        _sheetMapping = new Dictionary<string, Action<GstuSpreadSheet>>(StringComparer.OrdinalIgnoreCase);

        MethodInfo[] methods = GetType().GetMethods(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);

        foreach (MethodInfo method in methods)
        {
            SheetTargetAttribute attribute = method.GetCustomAttribute<SheetTargetAttribute>();

            if (attribute == null)
            {
                continue;
            }

            Action<GstuSpreadSheet> parser = (Action<GstuSpreadSheet>)Delegate.CreateDelegate(typeof(Action<GstuSpreadSheet>), this, method);

            if (!_sheetMapping.TryAdd(attribute.SheetName, parser))
            {
                throw new InvalidOperationException($"중복된 시트 파서: {attribute.SheetName}");
            }
        }

        _targetSheets = _sheetConfigs.Select(config => config.sheetName).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();

        foreach (string sheetName in _targetSheets)
        {
            if (!_sheetMapping.ContainsKey(sheetName))
            {
                throw new InvalidOperationException($"등록되지 않은 시트 파서: {sheetName}");
            }
        }
    }

    /// <summary>
    /// Data를 읽기 위한 메서드
    /// </summary>
    public void ReadAllData()
    {
        InitializeDataServices();
        BeginGoogleSheetsLoad(false, null);
    }

    /// <summary>
    /// Google Sheets 전체 데이터를 내려받아 마지막 정상 로컬 캐시 갱신
    /// </summary>
    public void SynchronizeGoogleSheetsToLocalCache(Action<bool, string> onComplete)
    {
        if (Application.isPlaying)
        {
            throw new InvalidOperationException("플레이 중에는 로컬 게임 데이터 캐시를 수동 갱신할 수 없습니다.");
        }

        InitializeDataServices();
        BeginGoogleSheetsLoad(true, onComplete);
    }

    private string _parsingSheetName;

    private bool TryParseSheet(string sheetName, GstuSpreadSheet spreadsheet)
    {
        // HTTP 실패로 반환된 빈 응답은 기존 로컬 데이터 전환 경로 사용
        if (spreadsheet == null)
        {
            Debug.LogWarning($"[DataManager] {sheetName} 응답 없음 / 로컬 데이터 전환 대상");
            return false;
        }

        try
        {
            if (!_sheetMapping.TryGetValue(sheetName, out Action<GstuSpreadSheet> parser))
            {
                Debug.LogError($"[DataManager] {sheetName} 파서 누락");

                return false;
            }

            _parsingSheetName = sheetName;
            parser.Invoke(spreadsheet);

            return HasValidSheetResult(sheetName);
        }
        catch (Exception exception)
        {
            Debug.LogError($"[DataManager] {sheetName} 파싱 실패\n" + exception);

            return false;
        }
        finally
        {
            _parsingSheetName = null;
        }
    }

    private bool HasValidSheetResult(string sheetName)
    {
        return sheetName switch
        {
            Constants.PlayerMoveSheet => CachedPlayerMoveData.MoveSpeed > 0f,

            Constants.Player_Stat => CachedPlayerStatData.BaseHp > 0f,

            Constants.MonsterSheet => _monsterTable.Count > 0,
            Constants.QuestSheet => _questTable.Count > 0,

            Constants.MonsterAttackSheet => _monsterAttackTable.Count > 0,

            Constants.BossSheet => _bossTable.Count > 0,

            Constants.BossPhaseAttackSheet => _bossPhaseAttackTable.Count > 0,

            Constants.SkillSheet => _skillTable.Count > 0,

            Constants.ItemSheet => _itemTable.Count > 0,

            Constants.PortalSheet => _portalTable.Count > 0,
            Constants.HuntDifficultySheet => _huntDifficultyTable.Count > 0,
            Constants.HuntSpawnSheet => _huntSpawnTable.Count > 0,

            "EnhanceData" => _enhanceProbTable.Count > 0,

            "EnhanceStatData" => _enhanceStatList.Count > 0,

            "StringData" => _stringTable.Count > 0,

            _ => false
        };
    }

    private void BeginGoogleSheetsLoad(bool cacheOnlySynchronization, Action<bool, string> onComplete)
    {
        int loadGeneration = ++_loadGeneration;

        if (!cacheOnlySynchronization)
        {
            IsDataLoaded = false;
            ActiveDataSource = GameDataSource.None;
        }

        _currentLoadCount = 0;
        _hasSheetLoadFailure = false;
        _isCacheOnlySynchronization = cacheOnlySynchronization;
        _synchronizationCallback = onComplete;

        ResetRuntimeData();

        if (!TryValidateGoogleSheetsConfiguration(out string configurationError))
        {
            HandleGoogleSheetsFailure(configurationError);
            return;
        }

        foreach (string sheetName in _targetSheets)
        {
            string spreadsheetId = GetSheetID(sheetName);

            if (string.IsNullOrWhiteSpace(spreadsheetId))
            {
                Debug.LogError($"[DataManager] {sheetName} Spreadsheet ID 누락");

                CheckLoadComplete(loadGeneration, false);
                continue;
            }

            SpreadsheetManager.ReadPublicSpreadsheet(new GSTU_Search(spreadsheetId, sheetName),
                spreadsheet =>
                {
                    if (loadGeneration != _loadGeneration ||
                        (!_isCacheOnlySynchronization && IsDataLoaded))
                    {
                        return;
                    }

                    bool succeeded = TryParseSheet(sheetName, spreadsheet);

                    CheckLoadComplete(loadGeneration, succeeded);
                });
        }

        StartGoogleSheetsTimeout(loadGeneration);
    }

    private bool TryValidateGoogleSheetsConfiguration(out string errorMessage)
    {
        if (SpreadsheetManager.Config == null)
        {
            errorMessage = "GSTU_Config 에셋 누락";
            return false;
        }

        if (string.IsNullOrWhiteSpace(SpreadsheetManager.Config.API_Key))
        {
            errorMessage = "Google Sheets API Key 누락";
            return false;
        }

        errorMessage = string.Empty;
        return true;
    }

    private void StartGoogleSheetsTimeout(int loadGeneration)
    {
        if (Application.isPlaying)
        {
            _loadTimeoutCoroutine = StartCoroutine(WaitForGoogleSheets(loadGeneration));
            return;
        }

#if UNITY_EDITOR
        EditorCoroutineRunner.StartCoroutine(WaitForGoogleSheets(loadGeneration));
#endif
    }

    private void CheckLoadComplete(int loadGeneration, bool succeeded)
    {
        if (loadGeneration != _loadGeneration)
        {
            return;
        }

        _currentLoadCount++;
        _hasSheetLoadFailure |= !succeeded;

        if (_currentLoadCount < _targetSheets.Length)
        {
            return;
        }

        if (_hasSheetLoadFailure)
        {
            HandleGoogleSheetsFailure("시트 응답 또는 필수 데이터 검증 실패");

            return;
        }

        GameDataSnapshot snapshot = CreateSnapshot();
        if (!new GameDataSnapshotValidator().TryValidate(snapshot, out string validationError))
        {
            HandleGoogleSheetsFailure($"게임 데이터 검증 실패: {validationError}");
            return;
        }

        if (!_gameDataCache.TrySave(snapshot, out string cacheError))
        {
            Debug.LogWarning($"[DataManager] Google Sheets 데이터 캐시 저장 실패: {cacheError}");

            if (_isCacheOnlySynchronization)
            {
                FinishCacheSynchronization(false, cacheError);
                return;
            }
        }

        if (_isCacheOnlySynchronization)
        {
            FinishCacheSynchronization(true, "Google Sheets 데이터를 로컬 캐시에 저장했습니다.");
            return;
        }

        CompleteDataLoad(GameDataSource.GoogleSheets);
    }

    private IEnumerator WaitForGoogleSheets(int loadGeneration)
    {
        yield return new WaitForSecondsRealtime(_googleSheetsTimeoutSeconds);

        if (loadGeneration != _loadGeneration || (!_isCacheOnlySynchronization && IsDataLoaded))
        {
            yield break;
        }

        HandleGoogleSheetsFailure($"Google Sheets 응답 시간 초과 ({_googleSheetsTimeoutSeconds}초)");
    }

    private void FinishCacheSynchronization(bool succeeded, string message)
    {
        _loadGeneration++;
        StopGoogleSheetsTimeout();

        Action<bool, string> callback = _synchronizationCallback;
        _synchronizationCallback = null;
        _isCacheOnlySynchronization = false;
        callback?.Invoke(succeeded, message);
    }

    private void CompleteDataLoad(GameDataSource dataSource)
    {
        if (IsDataLoaded)
        {
            return;
        }

        StopGoogleSheetsTimeout();

        _localGameDataProvider.AppendForestCreatureData(_monsterTable, _monsterAttackTable);
        _localGameDataProvider.AppendMissingItemData(_itemTable, dataSource == GameDataSource.LocalCache);

        // 구형 캐시에서 누락된 표시 문자열만 기본값으로 보완
        foreach (KeyValuePair<string, string> entry in _localGameDataProvider.CreateStringTable())
        {
            _stringTable.TryAdd(entry.Key, entry.Value);
        }

        // 구형 캐시와 에디터 검증 정의만 보완, 시트의 실제 의뢰 값 우선
        foreach (var entry in _localGameDataProvider.CreateQuestTable())
        {
            _questTable.TryAdd(entry.Key, entry.Value);
        }

        if (!new GameDataSnapshotValidator().TryValidate(CreateSnapshot(), out string validationError))
        {
            throw new InvalidOperationException($"{dataSource} 게임 데이터 최종 검증 실패: {validationError}");
        }

#if UNITY_EDITOR
        if (VideoCaptureBalance.IsEnabled)
        {
            VideoCaptureBalance.Apply(_monsterTable, _huntSpawnTable.Values);
        }
#endif

        ActiveDataSource = dataSource;
        IsDataLoaded = true;

        Debug.Log($"[DataManager] 데이터 로드 완료: {dataSource}");

        OnAllDataLoaded?.Invoke();
    }

    /// <summary>
    /// 자동 파싱
    /// </summary>
    /// <typeparam Name="T"></typeparam>
    /// <param Name="targetObject"></param>
    /// <param Name="ss"></param>
    /// <param Name="rowId"></param>
    private void AutoParseRowData<T>(T targetObject, GstuSpreadSheet ss, string rowId)
    {
        var fields = typeof(T).GetFields(BindingFlags.Public | BindingFlags.Instance);

        //GSTU의 컬럼명 리스트 추출
        var columnKeys = ss.columns.secondaryKeyLink.Keys;

        foreach (var field in fields)
        {
            if (field.IsNotSerialized) { continue; }
            bool required = typeof(T) == typeof(PlayerStatData) || typeof(T) == typeof(PlayerMoveData) ||
                typeof(T) == typeof(HuntSpawnData);
            var attr = field.GetCustomAttribute<SheetColumnAttribute>();
            string targetColumnName = null;

            //Attribute가 있으면 사용
            if (attr != null)
            {
                targetColumnName = attr.ColumnName;
            }
            else
            {
                //없으면 매칭
                string cleanFiledName = field.Name.Replace("_", "").ToLower();
                targetColumnName = columnKeys.FirstOrDefault(
                                                col =>
                                                col.Replace("_", "").ToLower().Equals(cleanFiledName, StringComparison.OrdinalIgnoreCase));
            }

            //매칭되는 컬럼이 없으면 생략
            if (string.IsNullOrEmpty(targetColumnName) || !ss.columns.secondaryKeyLink.ContainsKey(targetColumnName))
            {
                if (required)
                {
                    throw new FormatException($"{_parsingSheetName} 행 {rowId} 컬럼 {field.Name}: 필수 컬럼 누락");
                }
                continue;
            }

            string cellValue = null;
            string colIdx = ss.columns.secondaryKeyLink[targetColumnName];
            int rowIdx = ss.rows.secondaryKeyLink[rowId];

            if (ss.Cells.ContainsKey(colIdx + rowIdx))
            {
                cellValue = ss.Cells[colIdx + rowIdx]?.value;
            }

            if (string.IsNullOrEmpty(cellValue))
            {
                if (required)
                {
                    throw new FormatException($"{_parsingSheetName} 행 {rowId} 컬럼 {targetColumnName}: 필수 값 누락");
                }
                continue;
            }

            string location = $"{_parsingSheetName} 행 {rowId} 컬럼 {targetColumnName} 값 '{cellValue}'";
            //필드 타입에 맞게 파싱
            if (field.FieldType == typeof(float))
            {
                if (!float.TryParse(cellValue, NumberStyles.Float, CultureInfo.InvariantCulture, out float value) || float.IsNaN(value) || float.IsInfinity(value))
                {
                    throw new FormatException($"{location}: 유한한 실수 필요");
                }
                field.SetValue(targetObject, value);
            }
            else if (field.FieldType == typeof(int))
            {
                if (!int.TryParse(cellValue, NumberStyles.Integer, CultureInfo.InvariantCulture, out int value))
                {
                    throw new FormatException($"{location}: 정수 필요");
                }
                field.SetValue(targetObject, value);
            }
            else if (field.FieldType == typeof(bool))
            {
                if (!bool.TryParse(cellValue, out bool value))
                {
                    throw new FormatException($"{location}: true 또는 false 필요");
                }
                field.SetValue(targetObject, value);
            }
            else if (field.FieldType.IsEnum)
            {
                if (!Enum.TryParse(field.FieldType, cellValue, true, out object value) || !Enum.IsDefined(field.FieldType, value))
                {
                    throw new FormatException($"{location}: 지원하는 {field.FieldType.Name} 값 필요");
                }
                field.SetValue(targetObject, value);
            }
            else if (field.FieldType == typeof(string))
            {
                field.SetValue(targetObject, cellValue);
            }
        }
    }

    private void HandleGoogleSheetsFailure(string reason)
    {
        if (_isCacheOnlySynchronization)
        {
            FinishCacheSynchronization(false, reason);
            return;
        }

        LoadCachedOrDefaultData(reason);
    }

    private void StopGoogleSheetsTimeout()
    {
        if (_loadTimeoutCoroutine == null)
        {
            return;
        }

        StopCoroutine(_loadTimeoutCoroutine);
        _loadTimeoutCoroutine = null;
    }

    private void ResetRuntimeData()
    {
        CachedPlayerMoveData = new PlayerMoveData();
        CachedPlayerStatData = new PlayerStatData();

        _monsterTable.Clear();
        _questTable.Clear();
        _monsterAttackTable.Clear();
        _bossTable.Clear();
        _bossPhaseAttackTable.Clear();
        _skillTable.Clear();
        _itemTable.Clear();
        _portalTable.Clear();
        _huntDifficultyTable.Clear();
        _huntSpawnTable.Clear();
        _enhanceProbTable.Clear();
        _enhanceStatList.Clear();
        _stringTable.Clear();
    }

    #region ParseData
    [SheetTarget(Constants.QuestSheet)]
    private void ParseQuestData(GstuSpreadSheet ss)
        => ParseIntKeyedTable(ss, Constants.QuestSheet, data => data.ID, _questTable);

    [SheetTarget(Constants.PlayerMoveSheet)]
    private void ParsePlayerData(GstuSpreadSheet ss)
    => AutoParseRowData(CachedPlayerMoveData, ss, Constants.FistRow);

    private void InitializeSkillRuntimeValues()
    {
        foreach (SkillData skillData in _skillTable.Values)
        {
            skillData.InitializeRuntimeValues();
        }
    }

    public SkillData GetSkillData(int skillId)
    {
        return _skillTable.TryGetValue(skillId, out SkillData data) ? data : null;
    }

    [SheetTarget(Constants.Player_Stat)]
    private void ParsePlayerStatData(GstuSpreadSheet ss)
                    => AutoParseRowData(CachedPlayerStatData, ss, Constants.FistRow);

    [SheetTarget(Constants.MonsterSheet)]
    private void ParseMonsterData(GstuSpreadSheet ss)
    {
        var columnKeys = ss.columns.secondaryKeyLink.Keys;
        string idColumn = columnKeys.FirstOrDefault(col => col.Equals(Constants.ID, StringComparison.OrdinalIgnoreCase));

        if (string.IsNullOrEmpty(idColumn))
        {
            Debug.LogError("MonsterSheet ID 칼럼 오류");
            return;
        }

        foreach (var cell in ss.columns[idColumn])
        {
            if (cell.value.Equals(Constants.ID, StringComparison.OrdinalIgnoreCase) || string.IsNullOrEmpty(cell.value))
            {
                continue;
            }

            MonsterData monsterData = new MonsterData();
            AutoParseRowData(monsterData, ss, cell.rowId);
            if (!monsterData.TryValidateDrops(out string dropError))
            {
                throw new FormatException($"{_parsingSheetName} 행 {cell.rowId}: {dropError}");
            }
            if (!MonsterTable.ContainsKey(monsterData.ID))
            {
                _monsterTable.Add(monsterData.ID, monsterData);
            }
        }
    }

    [SheetTarget(Constants.MonsterAttackSheet)]
    private void ParseMonsterAttackData(GstuSpreadSheet ss)
    {
        var columnKeys = ss.columns.secondaryKeyLink.Keys;
        string idColumn = columnKeys.FirstOrDefault(col => col.Equals(Constants.ID, StringComparison.OrdinalIgnoreCase));

        if (string.IsNullOrEmpty(idColumn))
        {
            Debug.LogError("MonsterAttack ID 칼럼 오류");
            return;
        }

        foreach (var cell in ss.columns[idColumn])
        {
            if (cell.value.Equals(Constants.ID, StringComparison.OrdinalIgnoreCase) || string.IsNullOrEmpty(cell.value))
            {
                continue;
            }

            MonsterAttackData attackData = new MonsterAttackData();
            AutoParseRowData(attackData, ss, cell.rowId);

            if (!_monsterAttackTable.TryAdd(attackData.PatternId, attackData))
            {
                throw new InvalidOperationException($"중복된 몬스터 공격 패턴 ID: {attackData.PatternId}");
            }
        }
    }

    [SheetTarget(Constants.SkillSheet)]
    private void ParseSkillData(GstuSpreadSheet ss)
    {
        ParseIntKeyedTable(ss, Constants.SkillSheet, data => data.ID, _skillTable);
        InitializeSkillRuntimeValues();
    }

    [SheetTarget(Constants.BossSheet)]
    private void ParseBossData(GstuSpreadSheet ss) => ParseIntKeyedTable(ss, Constants.BossSheet, data => data.BossId, _bossTable);

    [SheetTarget(Constants.HuntDifficultySheet)]
    private void ParseHuntDifficultyData(GstuSpreadSheet sheet) =>
        ParseIntKeyedTable(sheet, Constants.HuntDifficultySheet, entry => (int)entry.Difficulty, _huntDifficultyTable);

    [SheetTarget(Constants.HuntSpawnSheet)]
    private void ParseHuntSpawnData(GstuSpreadSheet sheet) =>
        ParseIntKeyedTable(sheet, Constants.HuntSpawnSheet, entry => entry.ID, _huntSpawnTable);

    [SheetTarget(Constants.BossPhaseAttackSheet)]
    private void ParseBossPhaseAttackData(GstuSpreadSheet ss) => ParseIntKeyedTable(ss, Constants.BossPhaseAttackSheet, data => data.PhaseAttackId, _bossPhaseAttackTable);

    /// <summary>
    /// ID 컬럼 기반 정수 키 데이터 테이블 파싱
    /// </summary>
    private void ParseIntKeyedTable<T>(GstuSpreadSheet ss, string sheetName, Func<T, int> keySelector, IDictionary<int, T> table) where T : new()
    {
        var columnKeys = ss.columns.secondaryKeyLink.Keys;
        string idColumn = columnKeys.FirstOrDefault(column => column.Equals(Constants.ID, StringComparison.OrdinalIgnoreCase));

        if (string.IsNullOrEmpty(idColumn))
        {
            throw new InvalidOperationException($"{sheetName} ID 칼럼 오류");
        }

        foreach (var cell in ss.columns[idColumn])
        {
            if (cell.value.Equals(Constants.ID, StringComparison.OrdinalIgnoreCase) || string.IsNullOrEmpty(cell.value))
            {
                continue;
            }

            T data = new T();
            AutoParseRowData(data, ss, cell.rowId);
            int id = keySelector(data);

            if (!table.TryAdd(id, data))
            {
                throw new InvalidOperationException($"{sheetName} 중복 ID: {id}");
            }
        }
    }

    [SheetTarget(Constants.ItemSheet)]
    private void ParseItemData(GstuSpreadSheet ss)
    {
        var columnKeys = ss.columns.secondaryKeyLink.Keys;
        string idColumn = columnKeys.FirstOrDefault(col => col.Equals(Constants.ID, StringComparison.OrdinalIgnoreCase));

        if (string.IsNullOrEmpty(idColumn))
        {
            Debug.LogError("ItemSheet ID 칼럼 오류");
            return;
        }

        foreach (var cell in ss.columns[idColumn])
        {
            if (cell.value.Equals(Constants.ID, StringComparison.OrdinalIgnoreCase) || string.IsNullOrEmpty(cell.value))
            {
                continue;
            }

            ItemData itemData = new ItemData();
            AutoParseRowData(itemData, ss, cell.rowId);
            if (!ItemTable.ContainsKey(itemData.ID))
            {
                _itemTable.Add(itemData.ID, itemData);
            }
        }
    }

    [SheetTarget("EnhanceData")]
    private void ParseEnhanceData(GstuSpreadSheet ss)       
    {
        var columnKeys = ss.columns.secondaryKeyLink.Keys;
        string idColum = columnKeys.FirstOrDefault(col =>
        col.Equals(Constants.Str_EnhanceLevel, StringComparison.OrdinalIgnoreCase));

        if (string.IsNullOrEmpty(idColum))
        {
            return;
        }

        foreach (var cell in ss.columns[idColum])
        {
            if (cell.value.Equals(Constants.Str_EnhanceLevel, StringComparison.OrdinalIgnoreCase) || string.IsNullOrEmpty(cell.value))
            {
                continue;
            }

            EnhanceData data = new EnhanceData();
            AutoParseRowData(data, ss, cell.rowId);
            if (!EnhanceProbTable.ContainsKey(data.EnhanceLevel))
            {
                _enhanceProbTable.Add(data.EnhanceLevel, data);
            }
        }
    }

    [SheetTarget("EnhanceStatData")]
    private void ParseEnhanceStatData(GstuSpreadSheet ss)
    {
        var columnKeys = ss.columns.secondaryKeyLink.Keys;
        string idColumn = columnKeys.FirstOrDefault(col =>
        col.Equals(Constants.Str_ReqLevel, StringComparison.OrdinalIgnoreCase));

        if (string.IsNullOrEmpty(idColumn))
        {
            return;
        }

        foreach (var cell in ss.columns[idColumn])
        {
            if (cell.value.Equals(Constants.Str_ReqLevel, StringComparison.OrdinalIgnoreCase) || string.IsNullOrEmpty(cell.value))
            {
                continue;
            }
            EnhanceStatData data = new EnhanceStatData();
            AutoParseRowData(data, ss, cell.rowId);
            _enhanceStatList.Add(data);
        }

        //레벨 기준 내림차순 정렬
        _enhanceStatList = _enhanceStatList.OrderByDescending(x => x.ReqLevel).ToList();
    }

    [SheetTarget("StringData")]
    private void ParseStringData(GstuSpreadSheet ss)
    {
        var columnKeys = ss.columns.secondaryKeyLink.Keys;
        string idColumn = columnKeys.FirstOrDefault(col =>
        col.Equals(Constants.Str_Key, StringComparison.OrdinalIgnoreCase));

        if (string.IsNullOrEmpty(idColumn))
        {
            return;
        }

        foreach (var cell in ss.columns[idColumn])
        {
            if (cell.value.Equals(Constants.Str_Key, StringComparison.OrdinalIgnoreCase) || string.IsNullOrEmpty(cell.value))
            {
                continue;
            }

            StringData data = new StringData();
            AutoParseRowData(data, ss, cell.rowId);

            if (!_stringTable.ContainsKey(data.Key))
            {
                _stringTable.Add(data.Key, data.Value);
            }
        }
    }

    [SheetTarget(Constants.PortalSheet)]
    private void ParsePortalData(GstuSpreadSheet ss)
    {
        var columnKeys = ss.columns.secondaryKeyLink.Keys;
        string idColumn = columnKeys.FirstOrDefault(col => col.Equals(Constants.ID, StringComparison.OrdinalIgnoreCase));

        if (string.IsNullOrEmpty(idColumn))
        {
            Debug.LogError("PortalSheet ID 칼럼 오류");
            return;
        }

        foreach (var cell in ss.columns[idColumn])
        {
            if (cell.value.Equals(Constants.ID, StringComparison.OrdinalIgnoreCase) || string.IsNullOrEmpty(cell.value))
            {
                continue;
            }

            PortalData portalData = new PortalData();
            AutoParseRowData(portalData, ss, cell.rowId);
            if (!_portalTable.ContainsKey(portalData.ID))
            {
                _portalTable.Add(portalData.ID, portalData);
            }
        }
    }

    #endregion

    #region ParseType
    public MonsterData GetMonsterData(int monsterId) => MonsterTable.TryGetValue(monsterId, out MonsterData data) ? data : null;
    public MonsterAttackData GetMonsterAttackData(int patternId) => MonsterAttackTable.TryGetValue(patternId, out MonsterAttackData data) ? data : null;

    public BossData GetBossData(int bossId) => BossTable.TryGetValue(bossId, out BossData data) ? data : null;

    /// <summary>
    /// 지정 보스와 페이즈의 공격 선택 데이터 조회
    /// </summary>
    public List<BossPhaseAttackData> GetBossPhaseAttacks(int bossId, int phase)
    {
        if (phase <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(phase), "보스 페이즈는 1 이상 필요");
        }

        List<BossPhaseAttackData> result = new List<BossPhaseAttackData>();

        foreach (BossPhaseAttackData phaseAttack in BossPhaseAttackTable.Values)
        {
            if (phaseAttack.BossID == bossId && phaseAttack.Phase == phase)
            {
                result.Add(phaseAttack);
            }
        }

        return result;
    }

    public EnhanceData GetEnhanceProb(int currentEnhaceLevel)
    {
        return EnhanceProbTable.TryGetValue(currentEnhaceLevel, out EnhanceData data) ? data : null;
    }

    public EnhanceStatData GetEnhanceStat(int itemReqLevel)
    {
        return _enhanceStatList.FirstOrDefault(stat => itemReqLevel >= stat.ReqLevel);
    }

    public string GetString(string key, params object[] args)
    {
        if (_stringTable.TryGetValue(key, out var text))
        {
            if (args != null && args.Length > 0)
            {
                return string.Format(text, args);
            }
            return text;
        }
        return $"Missing Text : {key}";
    }

    public PortalData GetPortalData(int portalId) => _portalTable.TryGetValue(portalId, out PortalData data) ? data : null;

    /// <summary>초급부터 상급 순서의 준비된 사냥터 입장 규칙 조회</summary>
    public IReadOnlyList<HuntDifficultyData> GetHuntDifficultyOptions()
    {
        if (!IsDataLoaded)
        {
            throw new InvalidOperationException("사냥터 난이도 데이터를 불러오는 중입니다.");
        }

        return _huntDifficultyTable.Values.OrderBy(entry => entry.Difficulty).ToArray();
    }

    /// <summary>준비된 사냥터 난이도와 구역의 단일 생성 규칙 조회</summary>
    public HuntSpawnData GetHuntSpawn(HuntDifficulty difficulty, HuntSpawnZone zone)
    {
        if (!IsDataLoaded)
        {
            throw new InvalidOperationException("사냥터 배치 데이터를 불러오는 중입니다.");
        }

        foreach (HuntSpawnData rule in _huntSpawnTable.Values)
        {
            if (rule.Difficulty == difficulty && rule.ZoneId == zone) { return rule; }
        }
        throw new InvalidOperationException($"HuntSpawn {difficulty}/{zone}: 생성 규칙 누락");
    }

    private string GetSheetID(string sheetName)
    {
        SheetConfig config = _sheetConfigs.Find(
            candidate => string.Equals(
                candidate.sheetName,
                sheetName,
                StringComparison.OrdinalIgnoreCase));

        return config.spreadsheetID;
    }

    #endregion

    #region LocalData
    private void LoadCachedOrDefaultData(string reason)
    {
        // 늦게 도착한 Google Sheets 콜백 무효화
        _loadGeneration++;
        StopGoogleSheetsTimeout();

        if (_gameDataCache.TryLoad(out GameDataSnapshot snapshot, out string cacheError))
        {
            try
            {
                ApplySnapshot(snapshot);
                Debug.LogWarning($"[DataManager] Google Sheets 대신 로컬 캐시 사용: {reason}");
                CompleteDataLoad(GameDataSource.LocalCache);
                return;
            }
            catch (Exception exception)
            {
                cacheError = $"캐시 적용 실패: {exception.Message}";
            }
        }

        Debug.LogWarning($"[DataManager] 기본 로컬 데이터로 전환: {reason}. 캐시: {cacheError}");

        LoadDefaultLocalData();
    }

    private void LoadDefaultLocalData()
    {
        ResetRuntimeData();

        CachedPlayerMoveData = _localGameDataProvider.CreatePlayerMoveData();

        CachedPlayerStatData = _localGameDataProvider.CreatePlayerStatData();

        _monsterTable = _localGameDataProvider.CreateMonsterTable();

        _monsterAttackTable = _localGameDataProvider.CreateMonsterAttackTable();

        _bossTable = _localGameDataProvider.CreateBossTable();

        _bossPhaseAttackTable = _localGameDataProvider.CreateBossPhaseAttackTable();

        _skillTable = _localGameDataProvider.CreateSkillTable();
        InitializeSkillRuntimeValues();

        _itemTable = _localGameDataProvider.CreateItemTable();

        _enhanceProbTable = _localGameDataProvider.CreateEnhanceProbabilityTable();

        _enhanceStatList = _localGameDataProvider.CreateEnhanceStatList();

        _stringTable = _localGameDataProvider.CreateStringTable();

        _portalTable = _localGameDataProvider.CreatePortalTable();
        _huntDifficultyTable = _localGameDataProvider.CreateHuntDifficultyTable();
        _huntSpawnTable = _localGameDataProvider.CreateHuntSpawnRows().ToDictionary(data => data.ID);

        CompleteDataLoad(GameDataSource.Local);
    }

    private GameDataSnapshot CreateSnapshot()
    {
        return new GameDataSnapshot
        {
            DownloadedAtUtc = DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture),
            PlayerMoveData = CachedPlayerMoveData,
            PlayerStatData = CachedPlayerStatData,
            Monsters = _monsterTable.Values.OrderBy(data => data.ID).ToList(),
            Quests = _questTable.Values.Where(data => data.ID < 91000).OrderBy(data => data.ID).ToList(),
            MonsterAttacks = _monsterAttackTable.Values.OrderBy(data => data.PatternId).ToList(),
            Bosses = _bossTable.Values.OrderBy(data => data.BossId).ToList(),
            BossPhaseAttacks = _bossPhaseAttackTable.Values.OrderBy(data => data.PhaseAttackId).ToList(),
            Skills = _skillTable.Values.OrderBy(data => data.ID).ToList(),
            Items = _itemTable.Values.OrderBy(data => data.ID).ToList(),
            EnhanceProbabilities = _enhanceProbTable.Values.OrderBy(data => data.EnhanceLevel).ToList(),
            EnhanceStats = _enhanceStatList.OrderByDescending(data => data.ReqLevel).ToList(),
            Strings = _stringTable
                .OrderBy(pair => pair.Key, StringComparer.Ordinal)
                .Select(pair => new GameDataStringEntry(pair.Key, pair.Value))
                .ToList(),
            Portals = _portalTable.Values.OrderBy(data => data.ID).ToList(),
            HuntDifficulties = _huntDifficultyTable.Values.OrderBy(data => data.Difficulty).ToList(),
            HuntSpawns = _huntSpawnTable.Values.OrderBy(data => data.ID).ToList()
        };
    }

    private void ApplySnapshot(GameDataSnapshot snapshot)
    {
        ResetRuntimeData();

        CachedPlayerMoveData = snapshot.PlayerMoveData;
        CachedPlayerStatData = snapshot.PlayerStatData;
        _monsterTable = snapshot.Monsters.ToDictionary(data => data.ID);
        _questTable = (snapshot.Quests ?? new List<QuestData>()).ToDictionary(data => data.ID);
        _monsterAttackTable = snapshot.MonsterAttacks.ToDictionary(data => data.PatternId);
        _bossTable = snapshot.Bosses.ToDictionary(data => data.BossId);
        _bossPhaseAttackTable = snapshot.BossPhaseAttacks.ToDictionary(data => data.PhaseAttackId);
        _skillTable = snapshot.Skills.ToDictionary(data => data.ID);
        InitializeSkillRuntimeValues();
        _itemTable = snapshot.Items.ToDictionary(data => data.ID);
        _enhanceProbTable = snapshot.EnhanceProbabilities.ToDictionary(data => data.EnhanceLevel);
        _enhanceStatList = snapshot.EnhanceStats.OrderByDescending(data => data.ReqLevel).ToList();
        _stringTable = snapshot.Strings.ToDictionary(data => data.Key, data => data.Value);
        _portalTable = snapshot.Portals.ToDictionary(data => data.ID);
        _huntDifficultyTable = snapshot.HuntDifficulties.ToDictionary(data => (int)data.Difficulty);
        _huntSpawnTable = snapshot.HuntSpawns.ToDictionary(data => data.ID);
    }
    #endregion
}
