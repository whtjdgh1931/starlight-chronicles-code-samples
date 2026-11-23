using System;
using System.Collections.Generic;

/// <summary>
/// 사냥터 선택창의 수명·입장 조건·선택 확정과 씬 이동 연결
/// </summary>
public sealed class HuntEntryController
{
    private readonly HuntEntryView _view;
    private readonly PlaySessionController _session;
    private readonly GameSceneManager _sceneManager;
    private readonly DataManager _dataManager;
    private IReadOnlyList<HuntDifficultyData> _options = Array.Empty<HuntDifficultyData>();
    private Portal _source;
    private Player _player;
    private int _targetPortalId;

    public HuntEntryController(HuntEntryView view, PlaySessionController session, GameSceneManager sceneManager, DataManager dataManager)
    {
        _view = view;
        _session = session;
        _sceneManager = sceneManager;
        _dataManager = dataManager;
    }

    /// <summary>
    /// 공통 UI 준비 이후 선택·취소 이벤트 일회 연결
    /// </summary>
    public void Initialize()
    {
        _view.Initialize();
        _view.DifficultySelected += HandleSelection;
        _view.CloseRequested += Close;
    }

    /// <summary>
    /// 현재 포탈의 로컬 캐릭터만 대기 상태로 연결
    /// 반복 입력은 기존 선택창 유지
    /// </summary>
    public void Open(Portal source, Player player, int targetPortalId)
    {
        if (_view.IsOpen || !source.IsPlayerInside || player != _session.ActivePlayer ||
            !player.CanTalk || !_dataManager.IsDataLoaded)
        {
            return;
        }
        _options = _dataManager.GetHuntDifficultyOptions();
        _source = source;
        _player = player;
        _targetPortalId = targetPortalId;
        _source.PlayerExited += HandlePortalExited;
        _player.Died += HandlePlayerDied;
        _player.PlayerStat.LevelChanged += HandleLevelChanged;
        _player.SetDialogueOpen(true);
        _view.Show(_player.PlayerStat.Level, _options);
    }

    private void HandleSelection(HuntDifficulty difficulty)
    {
        // 닫힌 화면의 지연 클릭과 연속 입장 요청 제외
        if (!_view.IsOpen) { return; }
        if (_source == null || !_source.isActiveAndEnabled || !_source.IsPlayerInside ||
            _player == null || _player != _session.ActivePlayer || _player.IsDead ||
            _player.IsNetworkControlled || _player.IsNetworkJoinLocked || !_player.CanCaptureCharacterState)
        {
            Close();
            return;
        }
        foreach (HuntDifficultyData option in _options)
        {
            if (option.Difficulty != difficulty) { continue; }
            // 버튼 표시와 별개로 확정 시점의 실제 레벨 재확인
            if (!option.AllowsEntry(_player.PlayerStat.Level))
            {
                _view.ShowMessage($"{option.Name}은 {option.RequiredLevel}레벨부터 입장할 수 있어요.");
                return;
            }
            int targetPortalId = _targetPortalId;
            _session.SelectHuntDifficulty(difficulty);
            _source.CompleteEntry();
            Close();
            _sceneManager.ChangeMapScene(GameSceneId.HuntScene, targetPortalId);
            return;
        }
        _view.ShowMessage("입장할 난이도를 다시 선택해 주세요.");
    }

    /// <summary>
    /// 취소·이탈·사망·씬 전환 시 선택 대기와 조작 제한 해제
    /// 마지막 확정 난이도와 현재 맵은 유지
    /// </summary>
    public void Close()
    {
        // 제거된 Unity 객체도 관리 이벤트는 대응 해제
        if (_source is not null) { _source.PlayerExited -= HandlePortalExited; }
        if (_player is not null)
        {
            _player.Died -= HandlePlayerDied;
            _player.PlayerStat.LevelChanged -= HandleLevelChanged;
            if (_player != null) { _player.SetDialogueOpen(false); }
        }
        if (_view != null) { _view.Close(); }
        _source = null;
        _player = null;
        _options = Array.Empty<HuntDifficultyData>();
    }

    public void Shutdown()
    {
        Close();
        _view.DifficultySelected -= HandleSelection;
        _view.CloseRequested -= Close;
    }

    private void HandlePortalExited(Portal portal) => Close();
    private void HandlePlayerDied(Player player) => Close();
    private void HandleLevelChanged(int level) => _view.Show(level, _options);
}
