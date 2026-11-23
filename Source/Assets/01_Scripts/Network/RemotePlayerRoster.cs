using System;
using System.Collections.Generic;

/// <summary>
/// 접속 클라이언트와 원격 플레이어의 대응 관리
/// </summary>
public sealed class RemotePlayerRoster
{
    private readonly Dictionary<ulong, Player> _playersByClientId = new();

    /// <summary>
    /// 원격 플레이어 등록
    /// </summary>
    public void Register(ulong clientId, Player player)
    {
        if (!_playersByClientId.TryAdd(clientId, player))
        {
            throw new InvalidOperationException($"이미 등록된 클라이언트: {clientId}");
        }
    }

    /// <summary>
    /// 클라이언트의 원격 플레이어 조회
    /// </summary>
    public bool TryGet(ulong clientId, out Player player)
    {
        return _playersByClientId.TryGetValue(clientId, out player);
    }

    /// <summary>
    /// 접속 해제된 원격 플레이어의 등록 제거
    /// </summary>
    public bool Remove(ulong clientId, out Player player)
    {
        return _playersByClientId.Remove(clientId, out player);
    }

    /// <summary>
    /// 세션 종료 시 등록 정보 초기화
    /// </summary>
    public void Clear()
    {
        _playersByClientId.Clear();
    }
}