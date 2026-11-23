using System;
using UnityEngine.SceneManagement;

/// <summary>로비 포탈의 오프라인 입장 확인과 취소</summary>
internal sealed class SoloBossEntryController
{
    private readonly SoloBossEntryView _view;
    private readonly PlaySessionController _session;
    private readonly GameSceneManager _sceneManager;
    private readonly DataManager _dataManager;
    private readonly Func<bool> _hasNetworkActivity;
    private Portal _portal;
    private Player _player;
    private int _arrivalPortalId;

    public bool IsOpen => _view.IsOpen;

    public SoloBossEntryController(SoloBossEntryView view, PlaySessionController session,
        GameSceneManager sceneManager, DataManager dataManager, Func<bool> hasNetworkActivity)
    {
        _view = view;
        _session = session;
        _sceneManager = sceneManager;
        _dataManager = dataManager;
        _hasNetworkActivity = hasNetworkActivity;
        _view.Initialize();
        _view.Confirmed += HandleConfirm;
        _view.Cancelled += Close;
    }

    public void Open(Portal portal, Player player, int arrivalPortalId)
    {
        if (IsOpen || !_dataManager.IsDataLoaded || player == null || !player.CanTalk || !CanEnter(portal, player)) { return; }

        _portal = portal;
        _player = player;
        _arrivalPortalId = arrivalPortalId;
        _portal.PlayerExited += HandlePortalExited;
        _player.Died += HandlePlayerDied;
        _player.SetDialogueOpen(true);
        _view.Show(_dataManager);
    }

    private bool CanEnter(Portal portal, Player player)
    {
        return portal != null && portal.isActiveAndEnabled && portal.IsPlayerInside &&
            portal.gameObject.scene == SceneManager.GetActiveScene() &&
            portal.gameObject.scene.name == nameof(GameSceneId.HostScene) &&
            player != null && player == _session.ActivePlayer && !player.IsDead &&
            !player.IsNetworkControlled && !player.IsNetworkJoinLocked &&
            player.CanCaptureCharacterState && !_hasNetworkActivity();
    }

    private void HandleConfirm()
    {
        if (!IsOpen) { return; }
        if (!CanEnter(_portal, _player) || !_sceneManager.TryEnterSoloBoss(_arrivalPortalId))
        {
            Close();
            return;
        }

        Portal portal = _portal;
        Close();
        portal.CompleteEntry();
    }

    private void HandlePortalExited(Portal portal)
    {
        if (ReferenceEquals(portal, _portal)) { Close(); }
    }

    private void HandlePlayerDied(Player player)
    {
        if (ReferenceEquals(player, _player)) { Close(); }
    }

    public void Close()
    {
        if (_portal is not null) { _portal.PlayerExited -= HandlePortalExited; }
        if (_player is not null)
        {
            _player.Died -= HandlePlayerDied;
            if (_player != null) { _player.SetDialogueOpen(false); }
        }

        _portal = null;
        _player = null;
        if (_view != null) { _view.Hide(); }
    }

    public void Shutdown()
    {
        Close();
        if (_view is not null)
        {
            _view.Confirmed -= HandleConfirm;
            _view.Cancelled -= Close;
        }
    }
}
