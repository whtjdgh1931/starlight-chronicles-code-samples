using System;
using System.Collections.Generic;
using System.Text;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

/// <summary>
/// 서버 확정 결과의 소유자 전송·수신 확인·제한 시간 관리
/// 보상 계산과 전투 상태 변경 제외
/// </summary>
public sealed class NetworkBattleResultSession : IDisposable
{
    [Serializable]
    public sealed class Result
    {
        public string Id;
        public bool Cleared;
        public float Experience;
        public int Gold;
        public int ItemCount;
        public SaveData State;
    }

    private const string ResultMessage = "BossBattleResult";
    private const string ReceivedMessage = "BossBattleResultReceived";
    private const string RequestMessage = "BossBattleResultRequest";
    private const string FailedMessage = "BossBattleResultFailed";

    public enum DeliveryStage { NotStarted, Waiting, Received, TimedOut, Failed }
    private static readonly UTF8Encoding Utf8 = new(false, true);
    private readonly NetworkManager _manager;
    private readonly SaveFileStorage _storage;
    private readonly DataManager _data;
    private readonly InventorySettings _inventorySettings;
    private readonly int _maxBytes;
    private readonly Action _returnFromBattle;
    private readonly Func<QuickSlotSaveData[]> _captureQuickSlots;
    private readonly Dictionary<ulong, string> _pendingReceipts = new();
    private CustomMessagingManager _messaging;
    private readonly Dictionary<ulong, string> _payloads = new();
    private readonly Dictionary<ulong, double> _nextRequestAt = new();
    private readonly LocalBattleResult _local;
    private readonly int _slotId;
    private readonly float _timeout;
    private double _deadline;
    private string _deliveryError = string.Empty;

    public event Action Changed;
    public Result Current => _local.Current;
    public DeliveryStage Stage { get; private set; }
    public string Error => _deliveryError.Length > 0 ? _deliveryError : Current != null ? _local.Error : string.Empty;
    public bool IsSaved => Current != null && _local.IsSaved;
    public bool CanReturn => Stage is DeliveryStage.TimedOut or DeliveryStage.Failed ||
        Stage == DeliveryStage.Received && IsSaved && (!_manager.IsServer || _pendingReceipts.Count == 0);
    public int WaitingReceiptCount => _pendingReceipts.Count;
    internal IReadOnlyList<LocalBattleResult.Reward> Rewards => _local.Rewards;
    internal int UnclaimedRewardCount => _local.UnclaimedRewardCount;

    internal string GetRewardName(LocalBattleResult.Reward reward) => _data.ItemTable[reward.Item.ItemId].Name;

    internal InventoryResult ClaimReward(LocalBattleResult.Reward reward) =>
        _local.ClaimReward(reward, PlayerInventory.GetTabForItem(_data.ItemTable[reward.Item.ItemId]));

    internal void ForfeitRemainingRewards() => _local.ForfeitRemainingRewards();

    public NetworkBattleResultSession(NetworkManager manager, int slotId, CharacterSaveLocator locator,
        DataManager data, InventorySettings inventorySettings, int maxBytes, Action returnFromBattle,
        Func<QuickSlotSaveData[]> captureQuickSlots, LocalBattleResult local, float timeout)
    {
        _manager = manager;
        _storage = locator.CreateStorage(slotId);
        _data = data;
        _inventorySettings = inventorySettings;
        _maxBytes = maxBytes;
        _returnFromBattle = returnFromBattle;
        _captureQuickSlots = captureQuickSlots;
        _local = local;
        _slotId = slotId;
        _timeout = timeout;
        _local.Changed += HandleLocalChanged;
    }

    /// <summary>
    /// 연결 이후 결과 수신과 서버 수신 응답 처리 등록
    /// </summary>
    public void Register()
    {
        if (_messaging != null)
        {
            return;
        }
        _messaging = _manager.CustomMessagingManager;
        _messaging.RegisterNamedMessageHandler(ResultMessage, HandleResult);
        _messaging.RegisterNamedMessageHandler(ReceivedMessage, HandleReceived);
        _messaging.RegisterNamedMessageHandler(RequestMessage, HandleRequest);
        _messaging.RegisterNamedMessageHandler(FailedMessage, HandleFailed);
    }

    /// <summary>
    /// 전체 결과 크기 확인과 대기 명단 확정 후 소유자별 전송
    /// 호스트의 즉시 수신 응답보다 전원 대기 등록 우선
    /// </summary>
    public void Publish(IReadOnlyDictionary<ulong, Result> results)
    {
        if (!_manager.IsServer)
        {
            throw new InvalidOperationException("서버만 보스전 결과 전송 가능");
        }
        if (_payloads.Count > 0) { throw new InvalidOperationException("결과 재계산 없이 기존 결과 재전송 필요"); }
        BeginReceiving();
        var payloads = new Dictionary<ulong, string>();
        foreach (KeyValuePair<ulong, Result> entry in results)
        {
            string json = JsonUtility.ToJson(entry.Value);
            if (Utf8.GetByteCount(json) > _maxBytes)
            {
                throw new InvalidOperationException("보스전 결과 메시지 크기 제한 초과");
            }
            payloads.Add(entry.Key, json);
        }
        foreach (KeyValuePair<ulong, string> payload in payloads) { _payloads.Add(payload.Key, payload.Value); }
        foreach (KeyValuePair<ulong, Result> entry in results)
        {
            _pendingReceipts.Add(entry.Key, entry.Value.Id);
        }
        foreach (KeyValuePair<ulong, string> entry in payloads)
        {
            if (entry.Key == _manager.LocalClientId)
            {
                Receive(entry.Value);
            }
            else
            {
                Send(ResultMessage, entry.Key, entry.Value);
            }
        }
    }

    /// <summary>
    /// 전투 종료 알림 이후 실제 결과 수신 대기 시작
    /// 결과가 먼저 도착한 경우 기존 수신 상태 유지
    /// </summary>
    public void BeginReceiving()
    {
        if (Stage != DeliveryStage.NotStarted) { return; }
        Stage = DeliveryStage.Waiting;
        _deadline = Time.realtimeSinceStartupAsDouble + _timeout;
    }

    public void Tick(double now)
    {
        bool waiting = Stage == DeliveryStage.Waiting ||
            Stage == DeliveryStage.Received && _manager.IsServer && _pendingReceipts.Count > 0;
        if (!waiting || now < _deadline) { return; }
        Stage = DeliveryStage.TimedOut;
        _deliveryError = "결과 수신 확인 시간 초과 / 재요청 또는 귀환 가능 / 미수신 보상 유실 가능";
        Changed?.Invoke();
    }

    /// <summary>
    /// 개인 저장 실패와 무관한 전송 실패 종료
    /// 보상을 다시 계산하지 않고 종료 경로 제공
    /// </summary>
    public void FailDelivery(string error)
    {
        Stage = DeliveryStage.Failed;
        _deliveryError = error;
        Changed?.Invoke();
        if (_manager.IsServer)
        {
            foreach (ulong clientId in _manager.ConnectedClientsIds)
            {
                if (clientId != _manager.LocalClientId) { Send(FailedMessage, clientId, error); }
            }
        }
    }

    public bool TrySave() => Current != null && _local.TrySave();

    /// <summary>
    /// 확정 JSON만 재전송, 보상 생성 제외
    /// 재요청 제한 간격은 반복 전송 부하 방지용
    /// </summary>
    public void RequestAgain()
    {
        if (_manager.IsServer)
        {
            foreach (ulong clientId in new List<ulong>(_pendingReceipts.Keys)) { Resend(clientId); }
        }
        else { Send(RequestMessage, NetworkManager.ServerClientId, string.Empty); }
    }

    private void Resend(ulong clientId)
    {
        double now = Time.realtimeSinceStartupAsDouble;
        if (_nextRequestAt.TryGetValue(clientId, out double next) && now < next) { return; }
        _nextRequestAt[clientId] = now + 1d;
        if (!_payloads.TryGetValue(clientId, out string json)) { return; }
        if (clientId == _manager.LocalClientId) { Receive(json); }
        else { Send(ResultMessage, clientId, json); }
    }

    private void HandleLocalChanged() => Changed?.Invoke();

    public void RemoveParticipant(ulong clientId)
    {
        _payloads.Remove(clientId);
        _nextRequestAt.Remove(clientId);
        if (_pendingReceipts.Remove(clientId))
        {
            Changed?.Invoke();
        }
    }

    public bool TryReturn()
    {
        if (!CanReturn || UnclaimedRewardCount > 0)
        {
            return false;
        }
        _returnFromBattle();
        return true;
    }

    private void Send(string name, ulong clientId, string text)
    {
        byte[] payload = Utf8.GetBytes(text);
        using var writer = new FastBufferWriter(payload.Length, Allocator.Temp);
        writer.WriteBytesSafe(payload);
        _messaging.SendNamedMessage(name, clientId, writer, NetworkDelivery.ReliableFragmentedSequenced);
    }

    private bool TryRead(FastBufferReader reader, out string text)
    {
        text = string.Empty;
        int length = reader.Length - reader.Position;
        if (length <= 0 || length > _maxBytes)
        {
            return false;
        }
        byte[] bytes = new byte[length];
        reader.ReadBytesSafe(ref bytes, length);
        try
        {
            text = Utf8.GetString(bytes);
            return true;
        }
        catch (DecoderFallbackException)
        {
            return false;
        }
    }

    private void Receive(string json)
    {
        Result result;
        try
        {
            result = JsonUtility.FromJson<Result>(json);
        }
        catch (ArgumentException)
        {
            _deliveryError = "보스전 결과 형식 오류";
            Changed?.Invoke();
            return;
        }
        var checker = new SaveDataChecker();
        if (result == null || !Guid.TryParseExact(result.Id, "N", out _) ||
            !checker.TryCheck(result.State, _inventorySettings, _data, out string error))
        {
            _deliveryError = "보스전 결과 데이터 검증 실패";
            Changed?.Invoke();
            return;
        }
        if (Current != null && Current.Id != result.Id)
        {
            return;
        }
        try
        {
            if (Current == null)
            {
                result.State.QuickSlots = _captureQuickSlots();
                _local.Accept(result, _slotId, _storage);
            }
        }
        catch (Exception exception)
        {
            _deliveryError = $"결과 보관 실패: {exception.Message}";
            Changed?.Invoke();
            return;
        }
        Stage = DeliveryStage.Received;
        _deliveryError = string.Empty;
        // 메모리 보관 확인을 먼저 전송하여 파일 저장 실패와 호스트 귀환 분리
        if (_manager.IsServer) { ConfirmReceived(_manager.LocalClientId, result.Id); }
        else { Send(ReceivedMessage, NetworkManager.ServerClientId, result.Id); }
        TrySave();
        Changed?.Invoke();
    }

    private void ConfirmReceived(ulong clientId, string id)
    {
        if (_pendingReceipts.TryGetValue(clientId, out string expected) && expected == id)
        {
            _pendingReceipts.Remove(clientId);
            Changed?.Invoke();
        }
    }

    private void HandleResult(ulong sender, FastBufferReader reader)
    {
        if (sender == NetworkManager.ServerClientId && TryRead(reader, out string json))
        {
            Receive(json);
        }
    }

    private void HandleReceived(ulong sender, FastBufferReader reader)
    {
        if (_manager.IsServer && TryRead(reader, out string id))
        {
            ConfirmReceived(sender, id);
        }
    }

    private void HandleRequest(ulong sender, FastBufferReader reader)
    {
        if (_manager.IsServer && reader.Length == reader.Position) { Resend(sender); }
    }

    private void HandleFailed(ulong sender, FastBufferReader reader)
    {
        if (sender != NetworkManager.ServerClientId || !TryRead(reader, out string error)) { return; }
        Stage = DeliveryStage.Failed;
        _deliveryError = error;
        Changed?.Invoke();
    }

    public void Dispose()
    {
        _local.Changed -= HandleLocalChanged;
        if (_messaging != null)
        {
            _messaging.UnregisterNamedMessageHandler(ResultMessage);
            _messaging.UnregisterNamedMessageHandler(ReceivedMessage);
            _messaging.UnregisterNamedMessageHandler(RequestMessage);
            _messaging.UnregisterNamedMessageHandler(FailedMessage);
            _messaging = null;
        }
        _pendingReceipts.Clear();
        _payloads.Clear();
        _nextRequestAt.Clear();
    }
}
