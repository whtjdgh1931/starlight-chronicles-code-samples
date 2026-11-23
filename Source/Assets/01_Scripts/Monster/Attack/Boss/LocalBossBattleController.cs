using System.Collections;
using UnityEngine;

/// <summary>오프라인 보스전 데스카운터와 부활·패배 처리</summary>
public sealed class LocalBossBattleController : MonoBehaviour
{
    private enum Outcome { Fighting, Victory, Defeat }
    private enum LifeState { Alive, Dying, Dead }

    [SerializeField] private LocalBossBattleView _view;
    [SerializeField] private PlayerDeathPresentation _presentation;
    [SerializeField] private Transform _safeRevivePoint;
    [SerializeField] private LayerMask _groundMask;
    [SerializeField, Min(0.01f)] private float _deathAnimationDuration = 0.5f;
    [SerializeField, Min(0.01f)] private float _tombstoneFallDuration = 0.5f;
    [SerializeField, Min(0.01f)] private float _groundSearchDistance = 30f;
    [SerializeField, Min(0f)] private float _groundSkin = 0.02f;

    private Player _player;
    private BossMonster _boss;
    private GameSceneManager _sceneManager;
    private Coroutine _deathWait;
    private Outcome _outcome;
    private LifeState _life;
    private Vector3 _revivePosition;
    private int _remainingDeaths;
    private float _reviveInvincibleDuration;

    /// <summary>현재 로컬 보스와 플레이어의 전투 연결</summary>
    public void Initialize(Player player, BossMonster boss, ObjectPool pool, GameSceneManager sceneManager, DataManager dataManager)
    {
        if (player == null || boss == null || pool == null || sceneManager == null ||
            _view == null || _presentation == null || _safeRevivePoint == null ||
            _groundMask.value == 0 ||
            _deathAnimationDuration <= 0f || _tombstoneFallDuration <= 0f)
        {
            throw new MissingReferenceException($"{name}: 로컬 보스전 초기화 참조와 설정 필요");
        }

        BossData rules = boss.BossData ??
            throw new System.InvalidOperationException($"{name}: 초기화된 BossData 필요");

        Shutdown();
        _presentation.ValidateConfiguration();
        _presentation.SetObjectPool(pool);
        _view.Initialize(dataManager);

        _player = player;
        _boss = boss;
        _sceneManager = sceneManager;
        _outcome = Outcome.Fighting;
        _life = LifeState.Alive;
        _remainingDeaths = rules.InitialDeathCount;
        _reviveInvincibleDuration = rules.ReviveInvincibleDuration;

        _player.Died += HandlePlayerDied;
        _presentation.FallStarted += HandleFallStarted;
        _view.ReviveRequested += HandleReviveRequested;
        _view.ReturnRequested += HandleReturnRequested;
        RefreshView();
    }

    /// <summary>전투 중인 해당 보스의 승리 단일 확정</summary>
    public bool TryCompleteVictory(BossMonster boss)
    {
        if (!ReferenceEquals(boss, _boss) || _outcome != Outcome.Fighting) { return false; }

        _outcome = Outcome.Victory;
        _player.SetSoloBossResultDamageBlocked(true);
        return true;
    }

    private void HandlePlayerDied(Player player)
    {
        if (player != _player || _outcome == Outcome.Defeat || _life != LifeState.Alive) { return; }

        _life = LifeState.Dying;
        if (_outcome == Outcome.Fighting && --_remainingDeaths == 0)
        {
            _outcome = Outcome.Defeat;
            _boss.StopCombat();
        }

        _revivePosition = player.FindGroundedPosition(
            _groundMask, _groundSearchDistance, _groundSkin, _safeRevivePoint.position);
        player.PlaceAtMapSpawn(_revivePosition);

        double now = Time.realtimeSinceStartupAsDouble;
        double fallAt = now + _deathAnimationDuration;
        double readyAt = fallAt + _tombstoneFallDuration;
        _presentation.BeginDeath(_revivePosition, fallAt, readyAt, now);
        _deathWait = StartCoroutine(WaitForTombstone(readyAt));
        RefreshView();
    }

    private void HandleFallStarted() => _player.SetCharacterVisible(false);

    private IEnumerator WaitForTombstone(double readyAt)
    {
        while (Time.realtimeSinceStartupAsDouble < readyAt) { yield return null; }

        _deathWait = null;
        _life = LifeState.Dead;
        RefreshView();
    }

    private void HandleReviveRequested()
    {
        if (_life != LifeState.Dead || _outcome == Outcome.Defeat || !_player.IsDead) { return; }

        _life = LifeState.Alive;
        _presentation.Clear();
        _player.PlaceAtMapSpawn(_revivePosition);
        _player.Revive(_reviveInvincibleDuration);
        _presentation.PlayRevive(_revivePosition);
        _player.SetLocalInputOwner(true);
        RefreshView();
    }

    private void HandleReturnRequested()
    {
        if (_life != LifeState.Dead || _outcome != Outcome.Defeat) { return; }

        if (!_sceneManager.TryReturnToLobbyAfterSoloBossDefeat(_player, out string error))
        {
            _view.ShowReturnError(error);
        }
    }

    private void RefreshView()
    {
        LocalBossBattleView.Prompt prompt = _life switch
        {
            LifeState.Dying => LocalBossBattleView.Prompt.Preparing,
            LifeState.Dead when _outcome == Outcome.Defeat => LocalBossBattleView.Prompt.Defeat,
            LifeState.Dead => LocalBossBattleView.Prompt.Revive,
            _ => LocalBossBattleView.Prompt.None
        };
        _view.Render(_remainingDeaths, prompt);
    }

    /// <summary>씬 종료 시 플레이어 구독과 비석 연출 정리</summary>
    public void Shutdown()
    {
        if (_player is null) { return; }
        if (_deathWait != null)
        {
            StopCoroutine(_deathWait);
            _deathWait = null;
        }

        _player.Died -= HandlePlayerDied;
        if (_player != null) { _player.SetSoloBossResultDamageBlocked(false); }

        if (_view != null)
        {
            _view.ReviveRequested -= HandleReviveRequested;
            _view.ReturnRequested -= HandleReturnRequested;
            _view.Hide();
        }

        if (_presentation != null)
        {
            _presentation.FallStarted -= HandleFallStarted;
            _presentation.Clear();
        }
        _player = null;
        _boss = null;
        _sceneManager = null;
    }

    private void OnDisable() => Shutdown();
}
