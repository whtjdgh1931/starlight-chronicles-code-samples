using System;
using UnityEngine;

/// <summary>
/// 서버 참가자별 경험치·골드 지급과 개인 아이템 보상 보관
/// 결과 저장 데이터의 HP·MP 최대치 적용
/// </summary>
public sealed class BossBattleRewardService
{
    public NetworkBattleResultSession.Result CreateResult(string id, Player player, MonsterData boss,
        DataManager data, bool cleared)
    {
        int itemCount = 0;
        int gold = 0;
        if (cleared)
        {
            player.QuestProgress.RecordMonsterKill(boss.ID, data.QuestTable);
            player.PlayerStat.AddExp(boss.Exp);

            // 참가자별 골드 추첨과 즉시 지급
            if (boss.GoldDropChance >= 1f || UnityEngine.Random.value < boss.GoldDropChance)
            {
                int rolledGold = UnityEngine.Random.Range(boss.MinGold, boss.MaxGold + 1);
                gold = Math.Min(rolledGold, player.GoldWallet.RemainingCapacity);
                player.GoldWallet.Add(gold);
            }
            var ids = boss.GetDropItemIDs();
            var chances = boss.GetDropItemChances();
            // 이전 보관 보상 뒤에 이번 전투 보상을 추가해 결과창의 처리 범위 고정
            for (int i = 0; i < ids.Count; i++)
            {
                if (chances[i] >= 1f || UnityEngine.Random.value < chances[i])
                {
                    player.PlayerInventory.StoreBossReward(new InventoryItemData(data.ItemTable[ids[i]], 1));
                    itemCount++;
                }
            }
        }
        SaveData state = player.CaptureCharacterState();
        PlayerStat stats = player.PlayerStat;
        state.Player.CurrentHp = stats.GetTotalStat(StatType.MaxHp);
        state.Player.CurrentMp = stats.GetTotalStat(StatType.MaxMp);
        return new NetworkBattleResultSession.Result
        {
            Id = id,
            Cleared = cleared,
            Experience = cleared ? boss.Exp : 0f,
            Gold = gold,
            ItemCount = itemCount,
            State = state
        };
    }

    /// <summary>
    /// 전투 시작 전 보상 데이터와 드롭 확률 검사
    /// </summary>
    public void Validate(MonsterData boss, DataManager data)
    {
        var ids = boss.GetDropItemIDs();
        var chances = boss.GetDropItemChances();
        if (float.IsNaN(boss.Exp) || float.IsInfinity(boss.Exp) || boss.Exp < 0f || ids.Count != chances.Count)
        {
            throw new InvalidOperationException("보스 경험치·드롭 항목 구성 오류");
        }
        if (boss.MinGold < 0 || boss.MaxGold < boss.MinGold || boss.MaxGold == int.MaxValue ||
            float.IsNaN(boss.GoldDropChance) || boss.GoldDropChance < 0f || boss.GoldDropChance > 1f)
        {
            throw new InvalidOperationException("보스 골드 금액·드롭 확률 구성 오류");
        }
        for (int i = 0; i < ids.Count; i++)
        {
            if (!data.ItemTable.TryGetValue(ids[i], out ItemData item) || item.GetItemType() == GameItemType.None ||
                item.GetItemType() == GameItemType.Gold || float.IsNaN(chances[i]) || chances[i] < 0f || chances[i] > 1f)
            {
                throw new InvalidOperationException($"보스 드롭 항목 오류: {ids[i]}");
            }
        }
    }
}
