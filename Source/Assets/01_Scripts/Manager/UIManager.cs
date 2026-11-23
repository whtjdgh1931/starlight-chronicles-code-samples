using System;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;

public class UIManager : MonoBehaviour
{
    [Header("Player UI 비활성화 씬")]
    [SerializeField]
    private GameSceneId[] _playerUiHiddenScenes =
  {
    GameSceneId.TitleScene,
    GameSceneId.SelectScene,
};

    [SerializeField] EnhanceUI _enhanceUi;
    public EnhanceUI EnhanceUI => _enhanceUi;

    [SerializeField] private InventoryUI _inventoryUI;
    public InventoryUI InventoryUI => _inventoryUI;

    [SerializeField] private QuestTrackerView _questTracker;
    public void BindQuestTracker(PlayerQuestProgress progress) => _questTracker.Bind(progress);
    public void UnbindQuestTracker() => _questTracker.Unbind();

    [SerializeField] private QuickSlotBar _quickSlotBar;
    public QuickSlotBar QuickSlotBar => _quickSlotBar;

    [SerializeField] EquipUI _equipUi;
    public EquipUI EquipUI => _equipUi;

    [SerializeField] StatUI _statUi;
    public StatUI StatUI => _statUi;

    [SerializeField] PlayerUI _playerUI;
    public PlayerUI PlayerUI => _playerUI;

    [SerializeField] private AudioSettingsView _audioSettingsView;
    [SerializeField] private GameOptionsView _gameOptionsView;

    [SerializeField] private MerchantView _merchantView;
    public MerchantView MerchantView => _merchantView;

    [SerializeField] private HuntEntryView _huntEntryView;
    private HuntEntryController _huntEntryController;
    [SerializeField] private SoloBossEntryView _soloBossEntryView;
    private SoloBossEntryController _soloBossEntryController;

    [Header("싱글 사망 안내")]
    [SerializeField] private GameObject _deathRoot;
    [SerializeField] private TMPro.TMP_Text _deathMessage;
    [SerializeField] private UnityEngine.UI.Button _returnToTownButton;
    private TMPro.TMP_Text _returnToTownButtonText;
    private readonly PopupSelection _deathSelection = new();

    private bool _isInitialized;
    private NetworkSessionController _optionsNetworkSession;
    private ItemTooltip _itemTooltip;

    internal void BindItemTooltip(PlayerStat stat, PlayerEquipment equipment) => _itemTooltip.Bind(stat, equipment);

    internal void UnbindItemTooltip() => _itemTooltip.Bind(null, null);

    public void Initialize()
    {
        if (_isInitialized)
        {
            return;
        }

        EnsureEventSystem();
        ValidateReferences();
        _returnToTownButtonText = _returnToTownButton.GetComponentInChildren<TMPro.TMP_Text>(true);
        if (_returnToTownButtonText == null)
        {
            throw new MissingReferenceException($"{name}: 마을 복귀 버튼의 TMP 텍스트 필요");
        }
        _deathRoot.SetActive(false);
        _returnToTownButton.onClick.AddListener(HandleReturnToTown);
        _returnToTownButton.navigation = new UnityEngine.UI.Navigation { mode = UnityEngine.UI.Navigation.Mode.None };

        _questTracker.Initialize();
        _inventoryUI.InitializeWindow();
        Canvas itemCanvas = _inventoryUI.GetComponentInParent<Canvas>(true);
        if (itemCanvas == null)
        {
            throw new MissingReferenceException($"{name}: 아이템 툴팁용 인벤토리 Canvas 필요");
        }

        _itemTooltip = new ItemTooltip(itemCanvas.rootCanvas, _returnToTownButtonText.font);
        _inventoryUI.BindTooltip(_itemTooltip);
        _equipUi.BindTooltip(_itemTooltip);
        _quickSlotBar.Initialize();
        _statUi.Initialize();
        _equipUi.Initialize();
        _enhanceUi.Initialize();
        _audioSettingsView.Initialize();
        InitializeGameOptions();
        _merchantView.Close();
        GameManager manager = GameManager.Instance;
        _huntEntryController = new HuntEntryController(_huntEntryView, manager.PlaySessionController, manager.GameSceneManager, manager.DataManager);
        _huntEntryController.Initialize();
        if (_soloBossEntryView == null)
        {
            throw new MissingReferenceException($"{name}: 혼자 도전 입장 팝업 참조 필요");
        }
        _soloBossEntryController = new SoloBossEntryController(
            _soloBossEntryView, manager.PlaySessionController, manager.GameSceneManager, manager.DataManager, HasNetworkActivity);

        // 게임 시작 직후 현재 씬에 맞게 적용
        Scene activeScene = SceneManager.GetActiveScene();
        UpdateOptionsButtonVisibility(activeScene);
        ApplyPlayerUiVisibility(activeScene);

        SceneManager.sceneLoaded += HandleSceneLoaded;
        EventBus.OnOptionsInput += HandleOptionsInput;
        _isInitialized = true;
    }

    /// <summary>
    /// 씬에 배치된 옵션 창의 저장·종료 동작 연결
    /// </summary>
    private void InitializeGameOptions()
    {
        _optionsNetworkSession = FindAnyObjectByType<NetworkSessionController>();
        GameManager manager = GameManager.Instance;
        var controller = new GameOptionsController(manager.PlaySessionController, manager.CharacterSession,
            HasNetworkActivity, QuitGame);
        _gameOptionsView.Initialize(controller);
    }

    private bool HasNetworkActivity()
    {
        NetworkManager network = NetworkManager.Singleton;
        return _optionsNetworkSession != null && (_optionsNetworkSession.HasSession || _optionsNetworkSession.IsInternetBusy) ||
            network != null && (network.IsListening || network.IsClient || network.IsServer || network.ShutdownInProgress);
    }

    /// <summary>
    /// 사냥터 포탈의 요청을 공통 난이도 선택 화면에 연결
    /// </summary>
    public void OpenHuntEntry(Portal source, Player player, int targetPortalId)
    {
        if (_merchantView.IsOpen) { return; }
        _gameOptionsView.Close();
        _huntEntryController.Open(source, player, targetPortalId);
    }

    /// <summary>로비 보스 포탈의 혼자 도전 확인 요청</summary>
    public void OpenSoloBossEntry(Portal source, Player player, int targetPortalId)
    {
        if (_merchantView.IsOpen || _huntEntryView.IsOpen || HasNetworkActivity()) { return; }

        _gameOptionsView.Close();
        _soloBossEntryController.Open(source, player, targetPortalId);
    }

    /// <summary>
    /// 마을·사냥터에서 사망한 로컬 플레이어의 복귀 안내 표시
    /// </summary>
    public void ShowSinglePlayerDeath(Player player)
    {
        string sceneName = SceneManager.GetActiveScene().name;
        if (!GameSceneManager.SupportsTownReturnAfterDeath(sceneName) || player == null ||
            player != GameManager.Instance.PlaySessionController.ActivePlayer || !player.IsDead ||
            player.IsNetworkControlled || player.IsNetworkJoinLocked || HasNetworkActivity())
        {
            return;
        }

        bool diedInTown = sceneName == nameof(GameSceneId.HomeScene);
        _huntEntryController.Close();
        _enhanceUi.CloseWindowImmediately();
        _merchantView.RequestClose();
        _merchantView.Close();
        _gameOptionsView.Close();
        _deathMessage.text = diedInTown
            ? "쓰러졌어요. 마을에서 다시 일어날 수 있어요."
            : "쓰러졌어요. 마을로 돌아가 다시 모험을 시작해 보세요.";
        _returnToTownButtonText.text = diedInTown ? "부활하기" : "마을로 돌아가기";
        _returnToTownButton.interactable = true;
        _deathRoot.SetActive(true);
        _deathRoot.transform.SetAsLastSibling();
        _deathSelection.Open(_deathRoot, _returnToTownButton);
    }

    private void HandleReturnToTown()
    {
        if (!_deathRoot.activeInHierarchy || !_returnToTownButton.IsInteractable() ||
            _gameOptionsView.gameObject.activeInHierarchy || _audioSettingsView.IsOpen) { return; }
        if (HasNetworkActivity())
        {
            _deathMessage.text = "온라인 접속 중에는 마을로 돌아갈 수 없어요.";
            return;
        }
        if (!GameManager.Instance.GameSceneManager.TryReturnToTownAfterDeath(out string errorMessage))
        {
            _deathMessage.text = errorMessage;
            return;
        }

        _returnToTownButton.interactable = false;
        _deathMessage.text = "부활을 준비하고 있어요.";
    }

    public void OpenGameOptions() => _gameOptionsView.Open();

    public void QuitFromMenu() => _gameOptionsView.QuitWithoutSaving();

    private void HandleOptionsInput()
    {
        if (_audioSettingsView.IsOpen)
        {
            _audioSettingsView.Close();
            return;
        }

        if (_deathRoot.activeSelf)
        {
            if (_returnToTownButton.interactable)
            {
                _gameOptionsView.Toggle();
            }

            return;
        }
        if (_soloBossEntryController.IsOpen)
        {
            _soloBossEntryController.Close();
            return;
        }
        if (_huntEntryView.IsOpen)
        {
            _huntEntryController.Close();
            return;
        }
        if (_merchantView.IsOpen)
        {
            _merchantView.RequestClose();
            return;
        }
        _gameOptionsView.Toggle();
    }

    private static void QuitGame()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    /// <summary>
    /// UI 입력 시스템 확보와 공통 확인 키 연결
    /// </summary>
    private void EnsureEventSystem()
    {
        EventSystem eventSystem = EventSystem.current;
        if (eventSystem == null)
        {
            eventSystem = FindAnyObjectByType<EventSystem>();
        }

        if (eventSystem == null)
        {
            var root = new GameObject(nameof(EventSystem), typeof(EventSystem), typeof(InputSystemUIInputModule));
            root.transform.SetParent(transform, false);
            eventSystem = root.GetComponent<EventSystem>();
        }

        InputSystemUIInputModule module = eventSystem.GetComponent<InputSystemUIInputModule>();
        if (module == null)
        {
            throw new MissingComponentException($"{eventSystem.name}: InputSystemUIInputModule 필요");
        }
        if (module.submit?.action == null)
        {
            module.AssignDefaultActions();
        }

        AddKeyboardSubmitBindings(module.submit.action);
    }

    private static void AddKeyboardSubmitBindings(InputAction submit)
    {
        const string EnterPath = "<Keyboard>/enter";
        const string NumpadEnterPath = "<Keyboard>/numpadEnter";
        bool hasEnter = false;
        bool hasNumpadEnter = false;
        foreach (InputBinding binding in submit.bindings)
        {
            hasEnter |= string.Equals(binding.effectivePath, EnterPath, StringComparison.OrdinalIgnoreCase);
            hasNumpadEnter |= string.Equals(binding.effectivePath, NumpadEnterPath, StringComparison.OrdinalIgnoreCase);
        }
        if (hasEnter && hasNumpadEnter)
        {
            return;
        }

        bool wasEnabled = submit.enabled;
        if (wasEnabled) { submit.Disable(); }
        if (!hasEnter) { submit.AddBinding(EnterPath); }
        if (!hasNumpadEnter) { submit.AddBinding(NumpadEnterPath); }
        if (wasEnabled) { submit.Enable(); }
    }

    private void ValidateReferences()
    {
        if (_deathRoot == null || _deathMessage == null || _returnToTownButton == null)
        {
            throw new MissingReferenceException($"{name}: 사망 안내와 마을 복귀 버튼 참조 필요");
        }
        if (_inventoryUI == null || _quickSlotBar == null || _questTracker == null)
        {
            throw new MissingReferenceException($"{name}: InventoryUI 참조 필요");
        }
        if (_gameOptionsView == null)
        {
            throw new MissingReferenceException($"{name}: GameOptionsView 참조 필요");
        }
        if (_huntEntryView == null)
        {
            throw new MissingReferenceException($"{name}: HuntEntryView 참조 필요");
        }
        if (_merchantView == null)
        {
            throw new MissingReferenceException($"{name}: MerchantView 참조 필요");
        }

        if (_audioSettingsView == null)
        {
            throw new MissingReferenceException($"{name}: AudioSettingsView 참조 필요");
        }

        if (_enhanceUi == null)
        {
            throw new MissingReferenceException(
                $"{name}: EnhanceUI 참조 필요");
        }

        if (_equipUi == null)
        {
            throw new MissingReferenceException(
                $"{name}: EquipUI 참조 필요");
        }

        if (_statUi == null)
        {
            throw new MissingReferenceException(
                $"{name}: StatUI 참조 필요");
        }

        if (_playerUI == null)
        {
            throw new MissingReferenceException(
                $"{name}: PlayerUI 참조 필요");
        }
    }

    private void ApplyPlayerUiVisibility(Scene scene)
    {
        if (!Enum.TryParse(scene.name, true, out GameSceneId currentScene))
        {
            Debug.LogWarning($"[UIManager] 등록되지 않은 씬: {scene.name}");

            _playerUI.gameObject.SetActive(true);
            _quickSlotBar.gameObject.SetActive(true);
            _questTracker.SetSceneVisible(true);
            return;
        }

        bool shouldHide = Array.IndexOf(_playerUiHiddenScenes, currentScene) >= 0;

        _playerUI.gameObject.SetActive(!shouldHide);
        _quickSlotBar.gameObject.SetActive(!shouldHide);
        _questTracker.SetSceneVisible(!shouldHide);
    }

    private void UpdateOptionsButtonVisibility(Scene scene)
    {
        _gameOptionsView.SetOpenButtonVisible(scene.name != nameof(GameSceneId.TitleScene));
    }

    private void HandleSceneLoaded(Scene scene, LoadSceneMode loadSceneMode)
    {
        _itemTooltip.Hide();
        _deathRoot.SetActive(false);
        _enhanceUi.CloseWindowImmediately();
        _optionsNetworkSession = FindAnyObjectByType<NetworkSessionController>();
        if (_audioSettingsView.IsOpen) { _audioSettingsView.Close(); }
        _huntEntryController.Close();
        _soloBossEntryController.Close();
        _gameOptionsView.Close();
        _deathSelection.Close(_deathRoot);
        _merchantView.RequestClose();
        _merchantView.Close();
        EnsureEventSystem();
        UpdateOptionsButtonVisibility(scene);
        ApplyPlayerUiVisibility(scene);
    }

    private void OnDestroy()
    {
        _deathSelection.Close(_deathRoot);
        if (_returnToTownButton != null) { _returnToTownButton.onClick.RemoveListener(HandleReturnToTown); }
        _huntEntryController?.Shutdown();
        _soloBossEntryController?.Shutdown();
        if (_questTracker != null) { _questTracker.Unbind(); }
        SceneManager.sceneLoaded -= HandleSceneLoaded;
        EventBus.OnOptionsInput -= HandleOptionsInput;
    }
}
