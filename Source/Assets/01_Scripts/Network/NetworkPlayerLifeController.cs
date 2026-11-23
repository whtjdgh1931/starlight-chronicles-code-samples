using System;
using System.Collections;
using Unity.Netcode;
using UnityEngine;

/// <summary>
/// 서버 플레이어의 사망 위치·부활 승인과 생명 상태 공유
/// 플레이어 동작은 Player 공개 메서드, 비석 표시는 연출 담당자에 위임
/// </summary>
public sealed class NetworkPlayerLifeController : NetworkBehaviour
{
    public enum LifeStage : byte { Alive, Dying, WaitingForRevive }

    public struct LifeSnapshot : INetworkSerializable, IEquatable<LifeSnapshot>
    {
        public LifeStage Stage;
        public Vector3 Position;
        public double FallStartedAt;
        public double ReviveReadyAt;

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref Stage);
            serializer.SerializeValue(ref Position);
            serializer.SerializeValue(ref FallStartedAt);
            serializer.SerializeValue(ref ReviveReadyAt);
        }

        public bool Equals(LifeSnapshot other)
        {
            return Stage == other.Stage && Position == other.Position &&
                FallStartedAt.Equals(other.FallStartedAt) && ReviveReadyAt.Equals(other.ReviveReadyAt);
        }
    }

    [SerializeField] private PlayerDeathPresentation _presentation;
    [SerializeField, Min(0.01f)] private float _deathAnimationDuration = 1f;
    [SerializeField, Min(0.01f)] private float _tombstoneFallDuration = 0.5f;
    [SerializeField] private LayerMask _groundMask;
    [SerializeField, Min(0.01f)] private float _groundSearchDistance = 30f;
    [SerializeField, Min(0f)] private float _groundSkin = 0.02f;

    private readonly NetworkVariable<LifeSnapshot> _snapshot = new();
    private NetworkBossBattleController _battle;
    private Player _player;

    public event Action Changed;
    public bool IsAlive => _snapshot.Value.Stage == LifeStage.Alive;
    public double ReviveReadyAt => _snapshot.Value.ReviveReadyAt;
    public bool CanAct => IsAlive && (_battle == null || _battle.IsFighting);
    public bool CanRevive => _battle != null && _battle.IsFighting &&
        _snapshot.Value.Stage == LifeStage.WaitingForRevive;

    /// <summary>
    /// 네트워크 생명 동작과 표시가 준비된 플레이어의 알림 연결
    /// </summary>
    public void Initialize(Player player)
    {
        if (_presentation == null || _groundMask.value == 0 ||
            _deathAnimationDuration <= 0f || _tombstoneFallDuration <= 0f)
        {
            throw new MissingReferenceException($"{name}: 사망 연출·지면·연출 시간 설정 필요");
        }
        StopAllCoroutines();
        _player = player;
        _presentation.ValidateConfiguration();
        _presentation.FallStarted -= HandleFallStarted;
        _presentation.FallStarted += HandleFallStarted;
        _snapshot.OnValueChanged -= HandleSnapshotChanged;
        if (IsServer)
        {
            _snapshot.Value = new LifeSnapshot { Stage = LifeStage.Alive, Position = player.transform.position };
        }
        _snapshot.OnValueChanged += HandleSnapshotChanged;
        if (IsServer)
        {
            _player.Died -= HandlePlayerDied;
            _player.Died += HandlePlayerDied;
        }
        HandleSnapshotChanged(_snapshot.Value, _snapshot.Value);
    }

    /// <summary>
    /// 준비된 참가자에게 보스전 상태 객체 연결
    /// </summary>
    [Rpc(SendTo.ClientsAndHost, InvokePermission = RpcInvokePermission.Server)]
    public void BindBattleRpc(NetworkObjectReference battleReference)
    {
        // 서버는 BeginBattle에서 먼저 연결, 호스트의 중복 연결 제외
        if (IsServer)
        {
            return;
        }
        if (!battleReference.TryGet(out NetworkObject battleObject, NetworkManager))
        {
            throw new InvalidOperationException("생성된 보스전 상태 객체 필요");
        }
        BindBattle(battleObject.GetComponent<NetworkBossBattleController>());
    }

    /// <summary>
    /// 서버 직접 연결과 클라이언트 수신 연결의 공통 초기화
    /// </summary>
    public void BindBattle(NetworkBossBattleController battle)
    {
        _battle = battle;
        _battle.RegisterPlayer(this);
        Changed?.Invoke();
    }

    /// <summary>
    /// 본인 부활 버튼에서 서버 승인 요청
    /// </summary>
    public void RequestRevive()
    {
        if (IsOwner && CanRevive)
        {
            RequestReviveRpc();
        }
    }

    /// <summary>
    /// 서버 착지 시각까지 대기한 뒤 부활 버튼 준비 상태 공유
    /// 연출 완료 여부와 독립적인 서버 승인 시각
    /// </summary>
    private IEnumerator PrepareReviveAt(double deadline)
    {
        while (IsSpawned && IsServer && _snapshot.Value.Stage == LifeStage.Dying)
        {
            double remaining = deadline - NetworkManager.ServerTime.Time;
            if (remaining <= 0d)
            {
                LifeSnapshot snapshot = _snapshot.Value;
                snapshot.Stage = LifeStage.WaitingForRevive;
                _snapshot.Value = snapshot;
                yield break;
            }
            yield return new WaitForSecondsRealtime((float)remaining);
        }
    }

    private void HandlePlayerDied(Player player)
    {
        if (_battle == null || !_battle.IsFighting || !IsAlive)
        {
            return;
        }
        Vector3 position = _player.FindGroundedPosition(
            _groundMask, _groundSearchDistance, _groundSkin, _battle.SafeRevivePosition);
        _player.SetNetworkLifeControl(false);
        _player.PlaceForNetworkLife(position);
        double fallStartedAt = NetworkManager.ServerTime.Time + _deathAnimationDuration;
        _snapshot.Value = new LifeSnapshot
        {
            Stage = LifeStage.Dying,
            Position = position,
            FallStartedAt = fallStartedAt,
            ReviveReadyAt = fallStartedAt + _tombstoneFallDuration
        };
        _battle.ConsumeDeath(this);
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)]
    private void RequestReviveRpc()
    {
        if (!CanRevive)
        {
            return;
        }
        Vector3 position = _snapshot.Value.Position;
        _player.PlaceForNetworkLife(position);
        _player.Revive(_battle.ReviveInvincibleDuration);
        _snapshot.Value = new LifeSnapshot { Stage = LifeStage.Alive, Position = position };
        _player.SetNetworkLifeControl(true);
    }

    private void HandleSnapshotChanged(LifeSnapshot previous, LifeSnapshot current)
    {
        if (current.Stage == LifeStage.Alive)
        {
            StopAllCoroutines();
            _presentation.Clear();
            _player.SetCharacterVisible(true);
            if (previous.Stage != LifeStage.Alive)
            {
                // 서버는 Revive에서 이미 캐릭터와 본인 UI 복구
                if (!IsServer)
                {
                    _player.RestoreAlivePresentation();
                }
                if (IsClient)
                {
                    _presentation.PlayRevive(current.Position);
                }
            }
        }
        else if (previous.Stage == LifeStage.Alive || previous.Equals(current))
        {
            if (IsServer && current.Stage == LifeStage.Dying)
            {
                StopAllCoroutines();
                StartCoroutine(PrepareReviveAt(current.ReviveReadyAt));
            }
            if (!IsServer)
            {
                _player.PlayDeathAnimation();
            }
            if (IsClient)
            {
                _player.SetCharacterVisible(true);
                _presentation.BeginDeath(current.Position, current.FallStartedAt,
                    current.ReviveReadyAt, NetworkManager.ServerTime.Time);
            }
        }
        Changed?.Invoke();
    }

    private void HandleFallStarted() => _player.SetCharacterVisible(false);

    /// <summary>
    /// 전투 결과 확정 시 생존 참가자의 조작도 중단
    /// </summary>
    public void StopForBattleEnd() => _player.SetNetworkLifeControl(false);

    public void DetachBattle()
    {
        StopAllCoroutines();
        _battle = null;
        _presentation.Clear();
        if (_player != null)
        {
            _player.SetCharacterVisible(true);
        }
        Changed?.Invoke();
    }

    public override void OnNetworkDespawn()
    {
        StopAllCoroutines();
        _snapshot.OnValueChanged -= HandleSnapshotChanged;
        _presentation.FallStarted -= HandleFallStarted;
        _presentation.Clear();
        if (_player != null)
        {
            _player.Died -= HandlePlayerDied;
            _player.SetCharacterVisible(true);
        }
        if (_battle != null)
        {
            _battle.UnregisterPlayer(this);
        }
        _battle = null;
        _player = null;
    }
}
