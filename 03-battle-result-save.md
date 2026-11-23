# 보스전 결과 저장

전투 결과를 받으면 먼저 저장할 스냅샷과 슬롯을 확정하고 수신 확인을 보냅니다. 수신 확인과 파일 저장을 분리해 참가자의 저장 오류가 호스트의 귀환을 막지 않도록 했습니다.

같은 ID의 결과가 다시 들어와도 처음에 보관한 스냅샷을 유지합니다. 파일 쓰기에 실패하면 플레이 중 같은 JSON으로 재시도할 수 있습니다. 이미 저장한 결과는 중복 쓰기를 건너뜁니다. 미저장 결과의 보관 범위는 현재 프로그램 실행 중 메모리입니다.

## LocalBattleResult 발췌

```csharp
public void Accept(NetworkBattleResultSession.Result result, int slotId, ISaveWriter writer)
{
    if (Current != null && Current.Id == result.Id) { return; }
    if (HasPending) { throw new InvalidOperationException("이전 보스전 결과 저장 또는 포기 필요"); }
    List<ItemSaveData> stored = result.State.Inventory.PendingBossRewards;
    if (result.ItemCount < 0 || result.ItemCount > stored.Count)
    {
        throw new InvalidOperationException("이번 전투의 개인 보상 개수가 올바르지 않습니다.");
    }

    string json = new SaveDataSerializer().Serialize(result.State, false);
    _rewards.Clear();
    // 이번 전투에서 보관 목록 끝에 추가된 보상만 결과창 대상으로 등록
    for (int i = stored.Count - result.ItemCount; i < stored.Count; i++)
    {
        _rewards.Add(new Reward(stored[i]));
    }

    UnclaimedRewardCount = result.ItemCount;
    Current = result;
    SlotId = slotId;
    _writer = writer;
    _json = json;
    _savedId = string.Empty;
    Error = string.Empty;
    Changed?.Invoke();
}

public bool TrySave()
{
    if (Current == null) { return false; }
    if (IsSaved) { return true; }
    if (!_writer.TryWrite(_json, out string error))
    {
        Error = error;
        Changed?.Invoke();
        return false;
    }
    _savedId = Current.Id;
    Error = string.Empty;
    Changed?.Invoke();
    return true;
}
```
