using System.Collections.Generic;

/// <summary>
/// 퀘스트 목표 확인과 수집품 전달·보상 지급
/// 캐릭터 파일 저장과 화면 제어 제외
/// </summary>
public sealed class QuestRewardService
{
    private readonly PlayerQuestProgress _progress;
    private readonly PlayerInventory _inventory;
    private readonly GoldWallet _wallet;
    private readonly PlayerStat _stat;

    public QuestRewardService(
        PlayerQuestProgress progress, PlayerInventory inventory, GoldWallet wallet, PlayerStat stat)
    {
        _progress = progress;
        _inventory = inventory;
        _wallet = wallet;
        _stat = stat;
    }

    /// <summary>
    /// 미수령 퀘스트의 목표 확인 후 보상 지급
    /// 이미 수령한 퀘스트는 재지급 없이 성공 반환
    /// Player 요청 진입점에서 중복 실행 차단 후 호출
    /// </summary>
    public bool TryClaim(QuestData quest, IReadOnlyDictionary<int, ItemData> items, out string errorMessage)
    {
        QuestRequestResult result = Claim(quest, items);
        errorMessage = result.Succeeded ? string.Empty : result.Message;
        return result.Succeeded;
    }

    /// <summary>
    /// 실제 보상 판정의 사유와 실행 당시 목표·수량 반환
    /// </summary>
    public QuestRequestResult Claim(QuestData quest, IReadOnlyDictionary<int, ItemData> items)
    {
        if (!_progress.TryGetProgress(quest.ID, out int progressCount, out bool rewardClaimed))
        {
            return new QuestRequestResult(QuestRequestResult.Code.NotAccepted, "현재 상태: 미수락\n필요 조건: 퀘스트 수락");
        }
        if (rewardClaimed)
        {
            return new QuestRequestResult(QuestRequestResult.Code.AlreadyClaimed, "이미 수령 완료 · 추가 지급 없음");
        }
        switch (quest.ObjectiveType)
        {
            case QuestObjectiveType.KillMonster:
                if (progressCount < quest.TargetCount)
                {
                    return new QuestRequestResult(QuestRequestResult.Code.KillTarget,
                        $"목표 몬스터 ID: {quest.TargetID}\n처치 {progressCount} / 목표 {quest.TargetCount}마리\n" +
                        $"추가 필요: {quest.TargetCount - progressCount}마리", progressCount, quest.TargetCount, quest.TargetID);
                }
                break;
            case QuestObjectiveType.CollectItem:
                break;
            default:
                return new QuestRequestResult(QuestRequestResult.Code.InvalidData,
                    $"지원하지 않는 목표 유형: {quest.ObjectiveType}");
        }
        if (!items.TryGetValue(quest.RewardItemID, out ItemData rewardItem))
        {
            return new QuestRequestResult(QuestRequestResult.Code.InvalidData,
                $"보상 아이템 ID {quest.RewardItemID}의 정의가 없습니다.", relatedId: quest.RewardItemID);
        }
        if ((quest.ObjectiveType == QuestObjectiveType.KillMonster && quest.TargetCount <= 0) || quest.RewardGold < 0 || quest.RewardExp < 0)
        {
            return new QuestRequestResult(QuestRequestResult.Code.InvalidData,
                $"목표 수량 {quest.TargetCount}, 보상 골드 {quest.RewardGold}, 경험치 {quest.RewardExp}: 정의 수치 확인 필요");
        }
        if (!_wallet.CanAdd(quest.RewardGold))
        {
            return new QuestRequestResult(QuestRequestResult.Code.InvalidData,
                $"현재 골드 {_wallet.CurrentGold} + 보상 {quest.RewardGold}: 정수 상한 {int.MaxValue} 초과",
                _wallet.CurrentGold, int.MaxValue - quest.RewardGold);
        }
        ItemExchangeResult exchange = _inventory.ExchangeItems(quest.GetRequiredItems(), items, rewardItem, quest.RewardItemCount);
        if (!exchange.Succeeded)
        {
            QuestRequestResult.Code code;
            switch (exchange.Reason)
            {
                case ItemExchangeResult.Code.InsufficientItems: code = QuestRequestResult.Code.ItemTarget; break;
                case ItemExchangeResult.Code.NoCapacity: code = QuestRequestResult.Code.RewardCapacity; break;
                default: code = QuestRequestResult.Code.InvalidData; break;
            }
            return new QuestRequestResult(code, exchange.Message, exchange.Current, exchange.Required, exchange.ItemId, exchange.ItemProgress);
        }
        _wallet.Add(quest.RewardGold);
        _stat.AddExp(quest.RewardExp);
        _progress.CompleteReward(quest.ID);
        return new QuestRequestResult(QuestRequestResult.Code.Success, "보상과 수령 기록을 반영했습니다. 자동 저장하지 않습니다.", itemProgress: exchange.ItemProgress);
    }
}
