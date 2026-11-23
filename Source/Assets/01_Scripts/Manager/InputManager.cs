using UnityEngine;
using UnityEngine.InputSystem;


/// <summary>
/// 키보드 마우스 입력 관련 매니저
/// </summary>
public class InputManager : MonoBehaviour
{
    [SerializeField] private PlayerInput _inputManager;
    private PlaySessionController _playSession;

    private bool BlocksGameplayInput => _playSession?.ActivePlayer is Player player && player != null && player.IsDead;
    /// <summary>
    ///  초기화용 메서드
    /// </summary>
    public void Initialize(PlaySessionController playSession)
    {
        _playSession = playSession ?? throw new System.ArgumentNullException(nameof(playSession));
        //인풋매니저가 비어있다면 설정
        if (_inputManager == null)
        {
            _inputManager = GetComponent<PlayerInput>();
        }

    }

    public void OnMove(InputValue inputValue)
    {
        if (BlocksGameplayInput) { return; }
        Vector2 input = inputValue.Get<Vector2>();

        //이동 입력 이벤트 실행
        EventBus.PublishMoveInput(input);
    }



    public void OnJump()
    {
        if (BlocksGameplayInput) { return; }
        #region teleport
        //Vector2 dirVector = _player.PlayerMove.MoveVector;
        //Vector2Int dirVectorInt = Vector2Int.zero;
        //if (dirVector.x != 0f)
        //{
        //		dirVectorInt.x = dirVector.x > 0f ? 1 : -1;
        //}

        //if (dirVector.y != 0f)
        //{
        //		dirVectorInt.y = dirVector.y > 0f ? 1 : -1;
        //}

        //_player.PlayerTeleport.Teleport(dirVectorInt, 10f, 45f, 2f);
        #endregion
        EventBus.PublishJumpInput();
    }

    public void OnAttack()
    {
        if (BlocksGameplayInput) { return; }
        EventBus.PublishBasicAttackInput();
    }

    public void OnInventoryTab()

    {
        if (BlocksGameplayInput) { return; }
        EventBus.PublishInventoryInput();
    }

    public void OnEnhanceWindowTab()
    {
        if (BlocksGameplayInput) { return; }
        EventBus.PublishEnhanceWindowInput();
    }

    public void OnStatWindowTab()
    {
        if (BlocksGameplayInput) { return; }
        EventBus.PublishStatWindowInput();
    }

    public void OnInventoryWindowTab()
    {
        if (BlocksGameplayInput) { return; }
        EventBus.PublishInventoryWindowInput();
    }

    public void OnPickup()
    {
        if (BlocksGameplayInput) { return; }
        EventBus.PublishPickupInput();
    }

    /// <summary>
    /// 옵션 창 입력 발행
    /// </summary>
    public void OnOptions(InputValue inputValue)
    {
        if (inputValue.isPressed) { EventBus.PublishOptionsInput(); }
    }

    /// <summary>
    /// NPC 입력을 누른 순간에만 상호작용 요청 발행
    /// </summary>
    public void OnNpcInteract(InputValue inputValue)
    {
        if (inputValue.isPressed && !BlocksGameplayInput) { EventBus.PublishNpcInteractInput(); }
    }

    /// <summary>
    /// 누른 순간에만 의뢰 진행 창 토글 요청
    /// </summary>
    public void OnQuestTracker(InputValue inputValue)
    {
        if (inputValue.isPressed && !BlocksGameplayInput) { EventBus.PublishQuestTrackerInput(); }
    }

    public void OnQuickSlot1() => PublishQuickSlot(0);

    public void OnQuickSlot2() => PublishQuickSlot(1);

    public void OnQuickSlot3() => PublishQuickSlot(2);

    public void OnQuickSlot4() => PublishQuickSlot(3);

    public void OnQuickSlot5() => PublishQuickSlot(4);

    public void OnQuickSlot6() => PublishQuickSlot(5);

    private void PublishQuickSlot(int index)
    {
        if (!BlocksGameplayInput) { EventBus.PublishQuickSlotInput(index); }
    }
}
