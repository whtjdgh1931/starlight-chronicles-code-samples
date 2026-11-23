using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

/// <summary>
/// 보스전의 공용 데스카운터와 서버 전투 결과 관리
/// </summary>
public sealed class NetworkBossBattleController : NetworkBehaviour
{
    public enum BattleStage : byte
    {
        Preparing,
        Fighting,
        DefeatPending,
        GameOver,
        Cleared
    }

    [SerializeField] private Transform _safeRevivePoint;
    [SerializeField] private BossBattleView _view;

    private readonly NetworkVariable<int> _remainingDeaths = new();
    private readonly NetworkVariable<BattleStage> _stage = new(BattleStage.Preparing);
    private readonly List<NetworkPlayerLifeController> _players = new();
    private BossMonster _boss;
    private readonly BossBattleRewardService _rewards = new();
    private NetworkSessionController _session;

    public event Action Changed;
    public int RemainingDeaths => _remainingDeaths.Value;
    public BattleStage Stage => _stage.Value;
    public bool IsFighting => Stage == BattleStage.Fighting;
    public Vector3 SafeRevivePosition => _safeRevivePoint.position;
    internal float ReviveInvincibleDuration { get; private set; }

    public override void OnNetworkSpawn()
    {
        if (_safeRevivePoint == null || _view == null)
        {
            throw new MissingReferenceException($"{name}: 보스전 안전 위치·UI 참조 필요");
        }
        _session = NetworkManager.GetComponent<NetworkSessionController>();
        _remainingDeaths.OnValueChanged += HandleCountChanged;
        _stage.OnValueChanged += HandleStageChanged;
        if (IsClient)
        {
            _view.Initialize(this, GameManager.Instance.DataManager);
        }
    }

    /// <summary>
    /// 참가자의 생명 상태 연결과 본인 부활 UI 지정
    /// </summary>
    public void RegisterPlayer(NetworkPlayerLifeController life)
    {
        if (!_players.Contains(life))
        {
            _players.Add(life);
        }
        if (IsClient && life.IsOwner)
        {
            _view.BindPlayer(life);
        }
    }

    /// <summary>
    /// 공동 맵 준비와 보스 생성 완료 후 서버 전투 시작
    /// </summary>
    public void BeginBattle(BossMonster boss)
    {
        if (!IsServer || Stage != BattleStage.Preparing)
        {
            return;
        }
        BossData rules = boss.BossData ??
            throw new InvalidOperationException($"{name}: 초기화된 BossData 필요");
        _rewards.Validate(boss.Data, GameManager.Instance.DataManager);
        _boss = boss;
        _boss.HealthChanged += HandleBossHealthChanged;
        ReviveInvincibleDuration = rules.ReviveInvincibleDuration;
        _remainingDeaths.Value = rules.InitialDeathCount;
        _stage.Value = BattleStage.Fighting;
        foreach (NetworkClient client in NetworkManager.ConnectedClientsList)
        {
            NetworkPlayerLifeController life = client.PlayerObject.GetComponent<NetworkPlayerLifeController>();
            life.BindBattle(this);
            life.BindBattleRpc(new NetworkObjectReference(NetworkObject));
        }
    }

    /// <summary>
    /// 사망 확정 후 공용 기회 차감과 마지막 사망의 패배 예약
    /// </summary>
    public void ConsumeDeath(NetworkPlayerLifeController life)
    {
        if (!IsServer || !IsFighting || !_players.Contains(life))
        {
            return;
        }
        _remainingDeaths.Value--;
        if (RemainingDeaths > 0)
        {
            return;
        }
        _stage.Value = BattleStage.DefeatPending;
        _boss.StopCombat();
        StopPlayers();
        StartCoroutine(CompleteGameOverAt(life.ReviveReadyAt));
    }

    /// <summary>
    /// 마지막 비석 착지의 서버 시각까지 일회 대기
    /// 사망 플레이어 접속 종료와 별개로 패배 확정
    /// </summary>
    private IEnumerator CompleteGameOverAt(double deadline)
    {
        while (IsSpawned && IsServer && Stage == BattleStage.DefeatPending)
        {
            double remaining = deadline - NetworkManager.ServerTime.Time;
            if (remaining <= 0d)
            {
                _stage.Value = BattleStage.GameOver;
                PublishResults(false);
                yield break;
            }
            yield return new WaitForSecondsRealtime((float)remaining);
        }
    }

    private void HandleBossHealthChanged(float currentHp, float maxHp)
    {
        if (IsFighting && currentHp <= 0f)
        {
            _stage.Value = BattleStage.Cleared;
            StopPlayers();
            PublishResults(true);
        }
    }

    /// <summary>
    /// 결과 확정 시 현재 접속 참가자의 개인 보상과 최종 저장 상태 전송
    /// 승패 상태 전환 분기에서 한 번 호출
    /// </summary>
    private void PublishResults(bool cleared)
    {
        try
        {
            string id = Guid.NewGuid().ToString("N");
            var results = new Dictionary<ulong, NetworkBattleResultSession.Result>();
            DataManager data = GameManager.Instance.DataManager;
            foreach (NetworkClient client in NetworkManager.ConnectedClientsList)
            {
                if (_session.Roster.TryGet(client.ClientId, out Player player))
                {
                    results.Add(client.ClientId, _rewards.CreateResult(id, player, _boss.Data, data, cleared));
                }
            }
            _session.BattleResults.Publish(results);
        }
        catch (Exception exception)
        {
            Debug.LogException(exception, this);
            _session.BattleResults.FailDelivery("보스전 결과 생성·전송 실패 / 보상 재계산 없이 귀환 가능");
        }
    }

    private void HandleCountChanged(int previous, int current) => Changed?.Invoke();

    private void HandleStageChanged(BattleStage previous, BattleStage current) => Changed?.Invoke();

    public void UnregisterPlayer(NetworkPlayerLifeController life)
    {
        _players.Remove(life);
        if (IsClient && life.IsOwner)
        {
            _view.UnbindPlayer(life);
        }
    }

    private void StopPlayers()
    {
        foreach (NetworkPlayerLifeController life in _players)
        {
            if (life != null && life.IsSpawned)
            {
                life.StopForBattleEnd();
            }
        }
    }

    public override void OnNetworkDespawn()
    {
        StopAllCoroutines();
        _remainingDeaths.OnValueChanged -= HandleCountChanged;
        _stage.OnValueChanged -= HandleStageChanged;
        if (_boss != null)
        {
            _boss.HealthChanged -= HandleBossHealthChanged;
        }
        foreach (NetworkPlayerLifeController life in _players)
        {
            if (life != null)
            {
                life.DetachBattle();
            }
        }
        _players.Clear();
        if (IsClient)
        {
            _view.Shutdown();
        }
    }
}
