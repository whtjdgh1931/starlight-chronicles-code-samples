using System;
using UnityEngine;

/// <summary>
/// 현재 오프라인 캐릭터의 수동 저장과 명시적 게임 종료 조율
/// </summary>
public sealed class GameOptionsController
{
    public enum OperationState { Idle, Saving, Saved, Failed, Quitting }

    private readonly PlaySessionController _session;
    private readonly CharacterSession _characters;
    private readonly Func<bool> _hasNetworkSession;
    private readonly Action _quit;
    private Player _boundPlayer;
    private PlayerSaveController _saveController;
    private Player _lastSavedPlayer;
    private int _lastSavedSlot;

    public event Action Changed;
    public OperationState State { get; private set; }
    public bool CanSave { get; private set; }
    public bool CanQuit => State != OperationState.Saving && State != OperationState.Quitting;
    public string Context { get; private set; } = "모험 기록을 준비하고 있어요.";
    public string LastResult { get; private set; } = string.Empty;

    /// <summary>
    /// 플레이 세션·선택 슬롯·온라인 제한과 종료 동작 연결
    /// </summary>
    public GameOptionsController(PlaySessionController session, CharacterSession characters,
        Func<bool> hasNetworkSession, Action quit)
    {
        _session = session ?? throw new ArgumentNullException(nameof(session));
        _characters = characters ?? throw new ArgumentNullException(nameof(characters));
        _hasNetworkSession = hasNetworkSession ?? throw new ArgumentNullException(nameof(hasNetworkSession));
        _quit = quit ?? throw new ArgumentNullException(nameof(quit));
    }

    /// <summary>
    /// 창 표시용 저장 가능 상태 갱신, 파일 쓰기 제외
    /// </summary>
    public void Refresh()
    {
        if (RefreshState()) { Changed?.Invoke(); }
    }

    private bool RefreshState()
    {
        bool available = TryGetSaveTarget(out Player player, out string reason);
        available &= CanQuit;
        string context = State == OperationState.Saving ? "모험 기록을 저장하고 있어요."
            : State == OperationState.Quitting ? "모험을 마무리하고 있어요."
            : available ? $"{_saveController.SlotId + 1}번 캐릭터의 모험 기록을 저장할 수 있어요." : reason;
        if (CanSave == available && Context == context) { return false; }
        CanSave = available;
        Context = context;
        return true;
    }

    private bool TryGetSaveTarget(out Player player, out string reason)
    {
        player = _session.ActivePlayer;
        reason = string.Empty;
        if (_hasNetworkSession()) { reason = "온라인 접속 중에는 여기에서 저장할 수 없어요."; return false; }
        if (!_characters.HasSelectedCharacter || player == null)
        {
            reason = "캐릭터를 선택하면 모험 기록을 저장할 수 있어요.";
            return false;
        }
        if (!player.CanCaptureCharacterState || !player.gameObject.activeInHierarchy)
        {
            reason = "캐릭터가 준비되면 저장할 수 있어요.";
            return false;
        }
        if (player.IsDead)
        {
            reason = "사망 상태에서는 저장할 수 없어요. 부활한 뒤 다시 시도해 주세요.";
            return false;
        }
        if (player.IsNetworkControlled || player.IsNetworkJoinLocked)
        {
            reason = "파티 접속을 준비하는 동안에는 저장할 수 없어요.";
            return false;
        }
        if (_boundPlayer != player)
        {
            _boundPlayer = player;
            player.TryGetComponent(out _saveController);
        }
        if (_saveController == null || !_saveController.IsInitialized)
        {
            reason = "저장 준비를 마치지 못했어요. 잠시 후 다시 시도해 주세요.";
            return false;
        }
        if (_saveController.SlotId != _characters.SelectedSlotId)
        {
            reason = "캐릭터를 전환하고 있어요. 잠시 후 다시 저장해 주세요.";
            return false;
        }
        return true;
    }

    /// <summary>
    /// 클릭 시 대상 재검사 후 기존 Player.TrySave로 전체 캐릭터 저장
    /// 저장 실패·예외는 결과로 표시하고 종료 제외
    /// </summary>
    public bool Save()
    {
        if (!CanQuit) { return false; }
        if (!TryGetSaveTarget(out Player player, out string reason))
        {
            SetResult(OperationState.Failed, "저장하지 못했습니다.\n" + reason);
            return false;
        }
        int slot = _saveController.SlotId;
        string target = $"{slot + 1}번 캐릭터";
        State = OperationState.Saving;
        CanSave = false;
        Context = "모험 기록을 저장하고 있어요.";
        Changed?.Invoke();
        try
        {
            if (!player.TrySave(out string error))
            {
                SetResult(OperationState.Failed, $"저장 실패\n{target}\n{error}\n저장 버튼을 눌러 재시도할 수 있습니다.");
                return false;
            }
            _lastSavedPlayer = player;
            _lastSavedSlot = slot;
            SetResult(OperationState.Saved, $"모험 기록을 저장했어요.\n{target} / {DateTime.Now:HH:mm:ss}");
            return true;
        }
        catch (Exception exception)
        {
            Debug.LogException(exception, player);
            SetResult(OperationState.Failed, $"저장 오류\n{target}\n{exception.Message}\n게임을 종료하지 않았습니다.");
            return false;
        }
    }

    /// <summary>
    /// 저장 성공과 동일 대상 유지 확인 이후 게임 종료
    /// </summary>
    public void SaveAndQuit()
    {
        if (!Save()) { return; }
        if (!TryGetSaveTarget(out Player player, out string reason) || player != _lastSavedPlayer ||
            _saveController.SlotId != _lastSavedSlot)
        {
            SetResult(OperationState.Failed, LastResult + "\n현재 캐릭터 상태가 바뀌어 종료를 보류했습니다. " + reason);
            return;
        }
        QuitWithoutSaving();
    }

    /// <summary>
    /// 캐릭터 추가 저장 없이 종료 요청
    /// </summary>
    public void QuitWithoutSaving()
    {
        if (!CanQuit) { return; }
        State = OperationState.Quitting;
        CanSave = false;
        Context = "모험을 마무리하고 있어요.";
        Changed?.Invoke();
        _quit();
    }

    private void SetResult(OperationState state, string result)
    {
        State = state;
        LastResult = result;
        RefreshState();
        Changed?.Invoke();
    }
}
