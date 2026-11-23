using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

/// <summary>
/// 서버의 보스 예고 표시 결과를 클라이언트에 전달
/// 피해 판정과 AI 변경 없이 사각 범위·투사체 경로 공유
/// </summary>
public sealed class NetworkBossTelegraph : NetworkBehaviour
{
    [SerializeField] private BossAttackTelegraph _telegraph;
    [SerializeField, Min(0.01f)] private float _positionInterval = 0.05f;
    private Vector2 _pendingPosition;
    private double _positionAt = double.PositiveInfinity;
    private double _nextPathsAt;

    public override void OnNetworkSpawn()
    {
        if (_telegraph == null)
        {
            throw new MissingReferenceException($"{name}: 보스 공격 예고 참조 필요");
        }
        if (IsServer)
        {
            _telegraph.Shown += HandleShown;
            _telegraph.PositionChanged += HandlePositionChanged;
            _telegraph.PathsShown += HandlePathsShown;
            _telegraph.Hidden += HandleHidden;
            _telegraph.BurstPlayed += HandleBurstPlayed;
        }
        else
        {
            _telegraph.Initialize();
        }
    }

    private void Update()
    {
        if (IsSpawned && IsServer && Time.unscaledTimeAsDouble >= _positionAt)
        {
            _positionAt = double.PositiveInfinity;
            MoveRpc(_pendingPosition);
        }
    }

    private void HandleShown(Vector2 position, Vector2 size, float duration, BossAttackTelegraph.WarningStyle style)
    {
        // 다른 컴포넌트의 제거 처리에서 남은 예고 알림 제외
        if (!IsSpawned)
        {
            return;
        }
        _positionAt = double.PositiveInfinity;
        _nextPathsAt = 0d;
        ShowRpc(position, size, duration, style);
    }

    private void HandlePositionChanged(Vector2 position)
    {
        _pendingPosition = position;
        if (double.IsPositiveInfinity(_positionAt))
        {
            _positionAt = Time.unscaledTimeAsDouble + _positionInterval;
        }
    }

    private void HandlePathsShown(Vector2 origin, IReadOnlyList<Vector2> directions, float length, float duration, bool isFinal)
    {
        double now = Time.unscaledTimeAsDouble;
        if (!IsSpawned || (!isFinal && now < _nextPathsAt))
        {
            return;
        }
        _nextPathsAt = now + _positionInterval;
        _positionAt = double.PositiveInfinity;
        var copy = new Vector2[directions.Count];
        for (int i = 0; i < copy.Length; i++)
        {
            copy[i] = directions[i];
        }
        ShowPathsRpc(origin, copy, length, duration);
    }

    private void HandleHidden()
    {
        // 다른 컴포넌트의 제거 처리에서 남은 예고 알림 제외
        if (!IsSpawned)
        {
            return;
        }
        _positionAt = double.PositiveInfinity;
        _nextPathsAt = 0d;
        HideRpc();
    }

    [Rpc(SendTo.ClientsAndHost, InvokePermission = RpcInvokePermission.Server)]
    private void ShowRpc(Vector2 position, Vector2 size, float duration, BossAttackTelegraph.WarningStyle style)
    {
        if (!IsServer)
        {
            _telegraph.Show(position, size, duration, style);
        }
    }

    [Rpc(SendTo.ClientsAndHost, InvokePermission = RpcInvokePermission.Server)]
    private void MoveRpc(Vector2 position)
    {
        if (!IsServer)
        {
            _telegraph.SetPosition(position);
        }
    }

    [Rpc(SendTo.ClientsAndHost, InvokePermission = RpcInvokePermission.Server)]
    private void ShowPathsRpc(Vector2 origin, Vector2[] directions, float length, float duration)
    {
        if (!IsServer)
        {
            _telegraph.ShowPaths(origin, directions, length, duration);
        }
    }

    [Rpc(SendTo.ClientsAndHost, InvokePermission = RpcInvokePermission.Server)]
    private void HideRpc()
    {
        if (!IsServer)
        {
            _telegraph.Hide();
        }
    }

    private void HandleBurstPlayed(BossAttackBurst.Kind kind, Vector2 position, Vector2 sizeOrDirection)
    {
        if (IsSpawned)
        {
            PlayBurstRpc(kind, position, sizeOrDirection);
        }
    }

    [Rpc(SendTo.ClientsAndHost, InvokePermission = RpcInvokePermission.Server)]
    private void PlayBurstRpc(BossAttackBurst.Kind kind, Vector2 position, Vector2 sizeOrDirection)
    {
        if (!IsServer)
        {
            _telegraph.PlayBurst(kind, position, sizeOrDirection);
        }
    }

    public override void OnNetworkDespawn()
    {
        _positionAt = double.PositiveInfinity;
        _nextPathsAt = 0d;
        _telegraph.Shown -= HandleShown;
        _telegraph.PositionChanged -= HandlePositionChanged;
        _telegraph.PathsShown -= HandlePathsShown;
        _telegraph.Hidden -= HandleHidden;
        _telegraph.BurstPlayed -= HandleBurstPlayed;
        _telegraph.Hide();
        _telegraph.ClearBursts();
    }
}
