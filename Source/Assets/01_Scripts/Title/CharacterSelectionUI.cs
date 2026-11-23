using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 고정된 캐릭터 슬롯 UI와 캐릭터 선택 기능 연결
/// </summary>
public sealed class CharacterSelectionUI : MonoBehaviour
{
    private const int SlotCount = 3;

    [SerializeField] private CharacterSelectionController _controller;

    [Header("캐릭터 슬롯")]
    [SerializeField] private CharacterSlotUI _slot1;
    [SerializeField] private CharacterSlotUI _slot2;
    [SerializeField] private CharacterSlotUI _slot3;

    [Header("직업 이미지")]
    [SerializeField] private Sprite _warriorImage;
    [SerializeField] private Sprite _archerImage;
    [SerializeField] private Sprite _magicianImage;

    [Header("직업 선택")]
    [SerializeField] private GameObject _classSelectionPanel;

    [Header("공통 팝업")]
    [SerializeField] private GameObject _deleteConfirmationPanel;
    [SerializeField] private Button _popupConfirmButton;
    [SerializeField] private TMP_Text _popupConfirmText;
    [SerializeField] private GameObject _popupCancelButton;
    [SerializeField] private string _unsupportedClassTitle = "준비 중인 직업";
    [SerializeField] private string _noticeConfirmLabel = "확인";

    [Header("안내 메시지")]
    [SerializeField] private TMP_Text _messageText;

    [Header("미저장 보스전 결과")]
    [SerializeField] private GameObject _pendingResultRoot;
    [SerializeField] private TMP_Text _pendingResultText;
    [SerializeField] private Button _retryResultButton;
    [SerializeField] private Button _discardResultButton;

    private enum PopupMode
    {
        Closed,
        DeleteConfirmation,
        Notice,
        DiscardResult
    }

    private LocalBattleResult _localResult;
    private PopupMode _popupMode;
    private readonly PopupSelection _popupSelection = new();
    private Button _cancelButton;
    private RectTransform _popupConfirmRect;
    private Vector2 _deleteConfirmPosition;
    private string _deleteConfirmLabel;
    private int _selectedSlotId;
    private int _pendingDeleteSlotId = -1;

    private void Awake()
    {
        ValidateReferences();
        if (!_popupCancelButton.TryGetComponent(out _cancelButton))
        {
            throw new MissingComponentException($"{name}: 공통 팝업 취소 Button 필요");
        }

        _popupConfirmRect = (RectTransform)_popupConfirmButton.transform;
        _deleteConfirmPosition = _popupConfirmRect.anchoredPosition;
        _deleteConfirmLabel = _popupConfirmText.text;
        _cancelButton.navigation = new Navigation { mode = Navigation.Mode.Explicit, selectOnLeft = _popupConfirmButton };
        ButtonFocusOutline.Attach(_popupConfirmButton);
        ButtonFocusOutline.Attach(_cancelButton);
        _classSelectionPanel.SetActive(false);
        _deleteConfirmationPanel.SetActive(false);
    }

    private void OnEnable()
    {
        _localResult = GameManager.Instance.LocalBattleResult;
        _localResult.Changed += RefreshPendingResult;
        _retryResultButton.onClick.AddListener(_controller.RetryBattleResultSave);
        _discardResultButton.onClick.AddListener(RequestDiscardResult);
        RefreshPendingResult();
        _controller.OnSlotsChanged += RenderSlots;
        _controller.OnOperationFailed += ShowMessage;
        _controller.OnUnsupportedClassSelected += ShowUnsupportedClass;
        _controller.OnCharacterCreated += HandleCharacterCreated;

        if (_controller.IsReady)
        {
            RenderSlots();
        }
        else
        {
            ShowMessage("게임 데이터를 불러오는 중입니다.");
        }
    }

    private void ValidateReferences()
    {


        if (_popupConfirmButton == null || _popupConfirmText == null || _popupCancelButton == null)
        {
            throw new MissingReferenceException($"{name}: 공통 팝업 버튼 참조 필요");
        }

        if (_classSelectionPanel == null || _deleteConfirmationPanel == null || _messageText == null)
        {
            throw new MissingReferenceException($"{name}: 직업 선택 또는 삭제 확인 UI 참조 필요");
        }
    }

    /// <summary>
    /// 첫 번째 슬롯 선택
    /// </summary>
    public void SelectSlot1() => SelectSlot(0);

    /// <summary>
    /// 두 번째 슬롯 선택
    /// </summary>
    public void SelectSlot2() => SelectSlot(1);

    /// <summary>
    /// 세 번째 슬롯 선택
    /// </summary>
    public void SelectSlot3() => SelectSlot(2);

    /// <summary>
    /// 전사 생성
    /// </summary>
    public void SelectWarrior() => CreateCharacter(PlayerClass.Warrior);

    /// <summary>
    /// 궁수 생성
    /// </summary>
    public void SelectArcher() => CreateCharacter(PlayerClass.Archer);

    /// <summary>
    /// 마법사 생성
    /// </summary>
    public void SelectMagician() => CreateCharacter(PlayerClass.Magician);

    /// <summary>
    /// 첫 번째 슬롯 삭제 확인
    /// </summary>
    public void RequestDeleteSlot1() => RequestDelete(0);

    /// <summary>
    /// 두 번째 슬롯 삭제 확인
    /// </summary>
    public void RequestDeleteSlot2() => RequestDelete(1);

    /// <summary>
    /// 세 번째 슬롯 삭제 확인
    /// </summary>
    public void RequestDeleteSlot3() => RequestDelete(2);

    /// <summary>
    /// 공통 팝업의 안내 종료 또는 선택한 슬롯 삭제 확정
    /// </summary>
    public void ConfirmDelete()
    {
        if (_popupMode == PopupMode.DiscardResult)
        {
            _controller.DiscardBattleResult();
            CloseDeleteConfirmation();
            return;
        }
        if (_popupMode == PopupMode.Notice)
        {
            CloseDeleteConfirmation();
            return;
        }

        if (_popupMode != PopupMode.DeleteConfirmation)
        {
            return;
        }

        if (!_controller.TryDeleteCharacter(_pendingDeleteSlotId, out string errorMessage))
        {
            ShowMessage($"캐릭터 삭제에 실패했습니다. {errorMessage}");
            return;
        }

        CloseDeleteConfirmation();
    }

    private void SelectSlot(int slotIndex)
    {
        if (_popupMode != PopupMode.Closed)
        {
            return;
        }

        if (!_controller.IsReady)
        {
            ShowMessage("게임 데이터가 아직 준비되지 않았습니다.");
            return;
        }

        CharacterSlotEntry entry = _controller.Slots[slotIndex];

        switch (entry.State)
        {
            case CharacterSlotState.Empty:
                _selectedSlotId = entry.SlotId;
                _classSelectionPanel.SetActive(true);
                break;

            case CharacterSlotState.Available:
            case CharacterSlotState.BackupAvailable:
                _controller.PlayCharacter(entry.SlotId);
                break;

            case CharacterSlotState.Corrupted:
                ShowMessage(entry.ErrorMessage);
                break;

            default:
                throw new ArgumentOutOfRangeException(nameof(entry.State), entry.State, "지원하지 않는 슬롯 상태");
        }
    }

    private void RequestDelete(int slotIndex)
    {
        if (_popupMode != PopupMode.Closed)
        {
            return;
        }

        if (!_controller.IsReady)
        {
            ShowMessage("게임 데이터가 아직 준비되지 않았습니다.");
            return;
        }

        CharacterSlotEntry entry = _controller.Slots[slotIndex];

        if (entry.State == CharacterSlotState.Empty)
        {
            return;
        }

        _pendingDeleteSlotId = entry.SlotId;
        ShowMessage($"{entry.SlotId + 1}번 슬롯의 저장 데이터를 정말 삭제하시겠습니까?");
        OpenPopup(PopupMode.DeleteConfirmation);
    }

    /// <summary>
    /// 동일 팝업의 안내 또는 삭제 확인 구성과 표시
    /// </summary>
    private void OpenPopup(PopupMode mode)
    {
        _popupMode = mode;
        bool isDeleteConfirmation = mode is PopupMode.DeleteConfirmation or PopupMode.DiscardResult;
        _popupCancelButton.SetActive(isDeleteConfirmation);
        _popupConfirmText.text = isDeleteConfirmation ? _deleteConfirmLabel : _noticeConfirmLabel;
        _popupConfirmRect.anchoredPosition = isDeleteConfirmation
            ? _deleteConfirmPosition : new Vector2(0f, _deleteConfirmPosition.y);
        _popupConfirmButton.navigation = isDeleteConfirmation
            ? new Navigation { mode = Navigation.Mode.Explicit, selectOnRight = _cancelButton }
            : new Navigation { mode = Navigation.Mode.None };
        _deleteConfirmationPanel.transform.SetAsLastSibling();
        _deleteConfirmationPanel.SetActive(true);
        _popupSelection.Open(_deleteConfirmationPanel, _popupConfirmButton);
    }

    private void CloseDeleteConfirmation()
    {
        _pendingDeleteSlotId = -1;
        _popupMode = PopupMode.Closed;
        _deleteConfirmationPanel.SetActive(false);
        ShowMessage(string.Empty);
        _popupSelection.Close(_deleteConfirmationPanel);
    }

    private void CreateCharacter(PlayerClass playerClass)
    {
        if (_popupMode != PopupMode.Closed)
        {
            return;
        }

        _controller.CreateCharacter(_selectedSlotId, playerClass);
    }

    private void HandleCharacterCreated()
    {
        _classSelectionPanel.SetActive(false);
    }

    private void ShowUnsupportedClass(string message)
    {
        ShowMessage($"{_unsupportedClassTitle}\n\n{message}");
        OpenPopup(PopupMode.Notice);
    }

    private void RenderSlots()
    {
        if (_controller.Slots.Count != SlotCount)
        {
            throw new InvalidOperationException($"{name}: 캐릭터 슬롯은 정확히 {SlotCount}개여야 합니다.");
        }

        RenderSlot(_slot1, _controller.Slots[0]);
        RenderSlot(_slot2, _controller.Slots[1]);
        RenderSlot(_slot3, _controller.Slots[2]);

        ShowMessage(string.Empty);
    }

    private void RenderSlot(CharacterSlotUI slotUI, CharacterSlotEntry entry)
    {
        Sprite classImage = entry.Summary == null ? null : GetClassImage(entry.Summary.Class);
        slotUI.Render(entry, classImage);
    }

    private Sprite GetClassImage(PlayerClass playerClass)
    {
        return playerClass switch
        {
            PlayerClass.Warrior => _warriorImage,
            PlayerClass.Archer => _archerImage,
            PlayerClass.Magician => _magicianImage,
            _ => null
        };
    }

    private void RefreshPendingResult()
    {
        LocalBattleResult result = _localResult;
        _pendingResultRoot.SetActive(result.HasPending);
        if (result.HasPending)
        {
            _pendingResultText.text = $"{result.SlotId + 1}번 캐릭터 보스전 결과 미저장\n선택 화면에서 저장 재시도 가능 / 프로그램 종료 시 유실\n{result.Error}";
        }
    }

    private void RequestDiscardResult()
    {
        if (_popupMode != PopupMode.Closed) { return; }
        ShowMessage("미저장 보스전 결과를 포기하시겠습니까? 기존 저장 파일은 유지됩니다.");
        OpenPopup(PopupMode.DiscardResult);
    }

    private void ShowMessage(string message) => _messageText.text = message;

    private void OnDisable()
    {
        _localResult.Changed -= RefreshPendingResult;
        _retryResultButton.onClick.RemoveListener(_controller.RetryBattleResultSave);
        _discardResultButton.onClick.RemoveListener(RequestDiscardResult);
        _controller.OnSlotsChanged -= RenderSlots;
        _controller.OnOperationFailed -= ShowMessage;
        _controller.OnUnsupportedClassSelected -= ShowUnsupportedClass;
        _controller.OnCharacterCreated -= HandleCharacterCreated;
        // 씬 종료 시 팝업이 먼저 파괴되는 순서 허용
        if (_deleteConfirmationPanel != null) { _deleteConfirmationPanel.SetActive(false); }
        _popupSelection.Close(_deleteConfirmationPanel);
        _popupMode = PopupMode.Closed;
        _pendingDeleteSlotId = -1;
    }

    /// <summary>
    /// 직업 선택 취소
    /// </summary>
    public void CancelClassSelection()
    {
        if (_popupMode == PopupMode.Closed)
        {
            _classSelectionPanel.SetActive(false);
        }
    }

    /// <summary>
    /// 슬롯 삭제 취소
    /// </summary>
    public void CancelDelete() => CloseDeleteConfirmation();
}
