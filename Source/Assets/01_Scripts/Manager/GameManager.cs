using System;
using UnityEngine;


/// <summary>
/// 전체 매니저 및 플레이어 관리
/// </summary>
public class GameManager : MonoBehaviour
{
    private static GameManager _instance;
    public static GameManager Instance
    {
        get
        {
            if (_instance == null)
            {
                _instance = FindAnyObjectByType<GameManager>();
            }

            if (_instance == null)
            {
                throw new InvalidOperationException(
                    "GameManager가 씬에 존재하지 않습니다.");
            }

            return _instance;
        }
    }

    private void OnDestroy()
    {
        if (_instance == this)
        {
            _instance = null;
        }
    }

    /// <summary>
    /// 입력 관련 매니저
    /// </summary>
    [SerializeField]
    private InputManager _inputManager;
    public InputManager InputManager => _inputManager;

    /// <summary>
    /// 데이터 관리 매니저
    /// </summary>
    [SerializeField]
    private DataManager _dataManager;
    public DataManager DataManager => _dataManager;

    /// <summary>
    /// 씬 매니저
    /// </summary>
    [SerializeField]
    private GameSceneManager _gameSceneManager;
    public GameSceneManager GameSceneManager => _gameSceneManager;

    [SerializeField] private BgmPlayer _bgmPlayer;
    [SerializeField] private AudioSettingsController _audioSettings;
    [SerializeField] private CombatSoundPlayer _combatSoundPlayer;
    public CombatSoundPlayer CombatSoundPlayer => _combatSoundPlayer;
    public int NextSpawnPortalID { get; set; } = 0;
    /// <summary>
    /// 캐릭터 슬롯 개수 규약
    /// </summary>
    [SerializeField, Min(1)] private int _characterSlotCount = 3;

    /// <summary>
    /// 파일 저장 위치
    /// </summary>
    private CharacterSaveLocator _characterSaveLocator;

    public CharacterSaveLocator CharacterSaveLocator
    {
        get
        {
            if (_characterSaveLocator == null)
            {
                throw new InvalidOperationException(
                    "CharacterSaveLocator가 초기화되지 않았습니다.");
            }

            return _characterSaveLocator;
        }
    }

    /// <summary>
    /// UI관련 매니저
    /// </summary>
    [SerializeField]
    private UIManager _uiManager;
    public UIManager UIManager => _uiManager;



    public LocalBattleResult LocalBattleResult { get; } = new();

    private CharacterSession _characterSession;
    public CharacterSession CharacterSession
    {
        get
        {
            if (_characterSession == null)
            {
                throw new InvalidOperationException("CharacterSession이 초기화되지 않았습니다.");
            }

            return _characterSession;
        }
    }

    private PlaySessionController _playSessionController;
    public PlaySessionController PlaySessionController
    {
        get
        {
            if (_playSessionController == null)
            {
                throw new InvalidOperationException("PlaySessionController가 초기화되지 않았습니다.");
            }

            return _playSessionController;
        }
    }


    /// <summary>
    /// 초기화 여부
    /// </summary>
    private bool _isInit = false;



    private void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }
        _instance = this;
        if (_isInit)
        {
            return;
        }
        ManagerCheck();
        Initialize();
    }

    /// <summary>
    /// 초기화용 매서드
    /// </summary>
    public void Initialize()
    {
        // 이미 초기화 되었다면 리턴
        if (_isInit)
        {
            return;
        }

        //싱글톤 객체 유지
        DontDestroyOnLoad(this.gameObject);

        _characterSession = new CharacterSession();

        string characterSaveRoot = Application.persistentDataPath;
#if UNITY_EDITOR
        if (VideoCaptureBalance.IsEnabled)
        {
            characterSaveRoot = VideoCaptureBalance.GetCharacterSaveRoot();
        }
#endif
        _characterSaveLocator = new CharacterSaveLocator(characterSaveRoot, _characterSlotCount);
        _playSessionController = new PlaySessionController(_characterSession, _gameSceneManager);


        //인풋 매니저 초기화
        _inputManager.Initialize(_playSessionController);

        // 사용자 오디오 설정과 UI 초기화
        _audioSettings.Initialize();
        _combatSoundPlayer.Initialize();
        _uiManager.Initialize();

        //데이터 매니저 초기화
        _dataManager.Initialize();

        //씬 매니저 초기화
        _gameSceneManager.Initialize();
        _bgmPlayer.Initialize();

        _isInit = true;
    }

    /// <summary>
    /// 비어있는 매니저가 있는지 검사
    /// </summary>
    public void ManagerCheck()
    {


        //인풋 매니저 검사
        if (_inputManager == null)
        {
            _inputManager = GetComponent<InputManager>();
            if (_inputManager == null)
            {
                _inputManager = _instance.gameObject.AddComponent<InputManager>();
            }
        }

        //데이터 매니저 검사
        if (_dataManager == null)
        {
            _dataManager = GetComponent<DataManager>();
            if (_dataManager == null)
            {
                _dataManager = _instance.gameObject.AddComponent<DataManager>();
            }
        }


        //UI매니저 검사
        if (_uiManager == null)
        {
            _uiManager = GetComponent<UIManager>();
        }

        if (_uiManager == null)
        {
            throw new MissingComponentException(
                $"{name}: UIManager가 필요합니다.");
        }

        if (_gameSceneManager == null)
        {
            _gameSceneManager = GetComponent<GameSceneManager>();
            if (_gameSceneManager == null)
            {
                _gameSceneManager = _instance.gameObject.AddComponent<GameSceneManager>();
            }
        }


    }

}
