using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Unity.Networking.Transport.Relay;
using Unity.Services.Authentication;
using Unity.Services.Core;
using Unity.Services.Lobbies;
using Unity.Services.Lobbies.Models;
using Unity.Services.Relay;
using Unity.Services.Relay.Models;
using UnityEngine;

/// <summary>
/// 인터넷 공개 방 조회·등록·퇴장과 Relay 연결 정보 준비
/// NGO 시작과 캐릭터 상태 변경은 호출자에서 처리
/// </summary>
public sealed class InternetRoomService
{
    public const int MaximumRoomNameLength = 40;
    private const string CompatibilityKey = "Compatibility";
    private const string RelayCodeKey = "RelayCode";
    private const string RelayProtocol = "dtls";
    private const int HeartbeatFailureLimit = 3;
    // 같은 컴퓨터의 독립 실행·가상 플레이어를 서로 다른 익명 참가자로 분리
    private static readonly string AuthenticationProfile = "net-" + Guid.NewGuid().ToString("N").Substring(0, 24);
    private readonly SemaphoreSlim _operations = new(1, 1);
    private Lobby _lobby;

    public bool HasRoom => _lobby != null;
    public bool IsHost => HasRoom && _lobby.HostId == AuthenticationService.Instance.PlayerId;
    public string RoomName => HasRoom ? _lobby.Name : string.Empty;

    private static string Compatibility => $"starlight-ngo-{NetworkSessionController.ProtocolVersion}:{Application.version}";

    private static async Task AuthenticateAsync(CancellationToken cancellation)
    {
        cancellation.ThrowIfCancellationRequested();
        if (string.IsNullOrEmpty(Application.cloudProjectId))
        {
            throw new InvalidOperationException("Unity Cloud 프로젝트 연결 필요: Project Settings > Services");
        }
        if (UnityServices.State != ServicesInitializationState.Initialized)
        {
            var options = new InitializationOptions().SetProfile(AuthenticationProfile);
            await UnityServices.InitializeAsync(options);
        }
        cancellation.ThrowIfCancellationRequested();
        if (!AuthenticationService.Instance.IsSignedIn)
        {
            await AuthenticationService.Instance.SignInAnonymouslyAsync();
        }
        cancellation.ThrowIfCancellationRequested();
    }

    /// <summary>
    /// 같은 게임 버전의 참가 가능한 공개 방 조회
    /// </summary>
    public async Task<IReadOnlyList<InternetRoomInfo>> QueryAsync(int limit, CancellationToken cancellation)
    {
        await _operations.WaitAsync(cancellation);
        try
        {
            await AuthenticateAsync(cancellation);
            var response = await LobbyService.Instance.QueryLobbiesAsync(new QueryLobbiesOptions
            {
                Count = limit,
                Filters = new List<QueryFilter>
                {
                    new(field: QueryFilter.FieldOptions.AvailableSlots, op: QueryFilter.OpOptions.GT, value: "0"),
                    new(field: QueryFilter.FieldOptions.IsLocked, op: QueryFilter.OpOptions.EQ, value: "0"),
                    new(field: QueryFilter.FieldOptions.S1, op: QueryFilter.OpOptions.EQ, value: Compatibility)
                },
                Order = new List<QueryOrder> { new(false, QueryOrder.FieldOptions.Created) }
            });
            cancellation.ThrowIfCancellationRequested();
            var rooms = new List<InternetRoomInfo>(response.Results.Count);
            foreach (Lobby lobby in response.Results)
            {
                rooms.Add(new InternetRoomInfo(lobby.Id, lobby.Name, lobby.MaxPlayers - lobby.AvailableSlots, lobby.MaxPlayers));
            }
            return rooms;
        }
        finally { _operations.Release(); }
    }

    /// <summary>
    /// Relay 할당과 잠긴 공개 방 생성, NGO 준비 후 잠금 해제 필요
    /// </summary>
    public async Task<RelayServerData> CreateAsync(string name, int maxPlayers, CancellationToken cancellation)
    {
        await _operations.WaitAsync(cancellation);
        try
        {
            await AuthenticateAsync(cancellation);
            if (HasRoom) { throw new InvalidOperationException("이전 인터넷 방 퇴장 완료 필요"); }
            var allocation = await RelayService.Instance.CreateAllocationAsync(maxPlayers - 1);
            cancellation.ThrowIfCancellationRequested();
            string code = await RelayService.Instance.GetJoinCodeAsync(allocation.AllocationId);
            cancellation.ThrowIfCancellationRequested();
            Lobby created = await LobbyService.Instance.CreateLobbyAsync(name, maxPlayers, new CreateLobbyOptions
            {
                IsPrivate = false,
                IsLocked = true,
                Player = new Unity.Services.Lobbies.Models.Player(AuthenticationService.Instance.PlayerId),
                Data = new Dictionary<string, DataObject>
                {
                    { CompatibilityKey, new DataObject(DataObject.VisibilityOptions.Public, Compatibility, DataObject.IndexOptions.S1) },
                    { RelayCodeKey, new DataObject(DataObject.VisibilityOptions.Member, code) }
                }
            });
            if (cancellation.IsCancellationRequested)
            {
                await RemoveLobbyAsync(created, true);
                cancellation.ThrowIfCancellationRequested();
            }
            _lobby = created;
            return AllocationUtils.ToRelayServerData(allocation, RelayProtocol);
        }
        finally { _operations.Release(); }
    }

    /// <summary>
    /// 선택한 방 참가와 멤버 전용 Relay 코드에 따른 연결 준비
    /// </summary>
    public async Task<RelayServerData> JoinAsync(string roomId, CancellationToken cancellation)
    {
        await _operations.WaitAsync(cancellation);
        try
        {
            await AuthenticateAsync(cancellation);
            if (HasRoom) { throw new InvalidOperationException("이전 인터넷 방 퇴장 완료 필요"); }
            Lobby joined = await LobbyService.Instance.JoinLobbyByIdAsync(roomId, new JoinLobbyByIdOptions
            {
                Player = new Unity.Services.Lobbies.Models.Player(AuthenticationService.Instance.PlayerId)
            });
            try
            {
                cancellation.ThrowIfCancellationRequested();
                if (joined.Data == null || !joined.Data.TryGetValue(CompatibilityKey, out DataObject version) || version.Value != Compatibility ||
                    !joined.Data.TryGetValue(RelayCodeKey, out DataObject relayCode))
                {
                    throw new InvalidOperationException("참가한 방의 게임 버전 또는 연결 정보 불일치");
                }
                var allocation = await RelayService.Instance.JoinAllocationAsync(relayCode.Value);
                cancellation.ThrowIfCancellationRequested();
                _lobby = joined;
                return AllocationUtils.ToRelayServerData(allocation, RelayProtocol);
            }
            catch
            {
                await RemoveLobbyAsync(joined, false);
                throw;
            }
        }
        finally { _operations.Release(); }
    }

    /// <summary>
    /// 호스트의 신규 참가 허용 상태 변경
    /// </summary>
    public async Task SetLockedAsync(bool locked, CancellationToken cancellation)
    {
        await _operations.WaitAsync(cancellation);
        try
        {
            cancellation.ThrowIfCancellationRequested();
            if (!IsHost) { throw new InvalidOperationException("방 잠금 변경에는 호스트 권한 필요"); }
            Lobby updated = await LobbyService.Instance.UpdateLobbyAsync(_lobby.Id, new UpdateLobbyOptions { IsLocked = locked });
            cancellation.ThrowIfCancellationRequested();
            _lobby = updated;
        }
        finally { _operations.Release(); }
    }

    /// <summary>
    /// 호스트 방 유지 신호, 연속 실패 시 호출자에게 종료 사유 전달
    /// </summary>
    public async Task KeepAliveAsync(TimeSpan interval, CancellationToken cancellation)
    {
        int failures = 0;
        while (true)
        {
            await Task.Delay(interval, cancellation);
            await _operations.WaitAsync(cancellation);
            try
            {
                cancellation.ThrowIfCancellationRequested();
                if (!HasRoom) { return; }
                if (IsHost)
                {
                    await LobbyService.Instance.SendHeartbeatPingAsync(_lobby.Id);
                    failures = 0;
                }
            }
            catch (LobbyServiceException exception)
            {
                failures++;
                if (exception.Reason == LobbyExceptionReason.LobbyNotFound || failures >= HeartbeatFailureLimit) { throw; }
                Debug.LogWarning($"인터넷 방 유지 신호 실패 ({failures}/{HeartbeatFailureLimit}): {exception.Message}");
            }
            finally { _operations.Release(); }
        }
    }

    /// <summary>
    /// 지연 응답이 만든 방의 ID만 정리
    /// 현재 방 참조와 다른 요청의 방 변경 제외
    /// </summary>
    private static async Task RemoveLobbyAsync(Lobby lobby, bool host)
    {
        try
        {
            if (host) { await LobbyService.Instance.DeleteLobbyAsync(lobby.Id); }
            else { await LobbyService.Instance.RemovePlayerAsync(lobby.Id, AuthenticationService.Instance.PlayerId); }
        }
        catch (LobbyServiceException exception) when (exception.Reason == LobbyExceptionReason.LobbyNotFound) { }
    }

    /// <summary>
    /// 진행 중인 서비스 요청 이후 호스트 방 삭제 또는 참가자 퇴장
    /// 실패 시 방 정보를 유지하여 재시도 허용
    /// </summary>
    public async Task LeaveAsync()
    {
        await _operations.WaitAsync();
        try
        {
            if (!HasRoom) { return; }
            try
            {
                if (IsHost) { await LobbyService.Instance.DeleteLobbyAsync(_lobby.Id); }
                else { await LobbyService.Instance.RemovePlayerAsync(_lobby.Id, AuthenticationService.Instance.PlayerId); }
            }
            catch (LobbyServiceException exception) when (exception.Reason == LobbyExceptionReason.LobbyNotFound)
            {
                _lobby = null;
                return;
            }
            _lobby = null;
        }
        finally { _operations.Release(); }
    }
}
