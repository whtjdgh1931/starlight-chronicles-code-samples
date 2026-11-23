using System.Text;

/// <summary>
/// 실제 요청 결과 코드의 표시 문구와 다음 조치 구성
/// 게임 판정과 오류 문자열 해석 제외
/// </summary>
internal static class QuestValidationFeedback
{
    internal static QuestValidationResult.Outcome Apply(QuestValidationResult.Outcome outcome,
        QuestRequestResult request, QuestData quest, DataManager data, bool claim)
    {
        string reason;
        string next;
        switch (request.Reason)
        {
            case QuestRequestResult.Code.Success:
                reason = claim ? "보상 수령 완료" : "퀘스트 수락 완료";
                next = "메모리에만 반영했습니다. 파일 반영은 별도 저장 버튼을 사용하세요.";
                break;
            case QuestRequestResult.Code.AlreadyAccepted:
                reason = "이미 수락 완료 · 추가 변경 없음";
                next = "목표를 진행하거나 반복 검증을 위해 퀘스트를 초기화하세요.";
                break;
            case QuestRequestResult.Code.AlreadyClaimed:
                reason = "이미 수령 완료 · 추가 지급 없음";
                next = "다시 보상을 검증하려면 퀘스트 초기화를 사용하세요. 기존 보상은 유지됩니다.";
                break;
            case QuestRequestResult.Code.NotAccepted:
                reason = "퀘스트 미수락";
                next = "선택 퀘스트 수락 버튼을 먼저 실행하세요.";
                break;
            case QuestRequestResult.Code.Prerequisite:
                reason = "선행 퀘스트 보상 미수령";
                next = "표시된 선행 퀘스트를 선택해 목표 달성 후 보상을 수령하세요.";
                break;
            case QuestRequestResult.Code.KillTarget:
                reason = "처치 목표 미달";
                next = "목표 몬스터를 추가로 처치하세요. 도구의 목표 몬스터 찾기·실제 처치 버튼을 사용할 수 있습니다.";
                break;
            case QuestRequestResult.Code.ItemTarget:
                reason = "수집품 부족";
                next = "부족한 수집품을 확보하세요. 검증용 지급 ID 설정·아이템 지급 버튼을 사용할 수 있습니다.";
                break;
            case QuestRequestResult.Code.RewardCapacity:
                reason = "보상 인벤토리 공간 부족";
                next = "표시된 보상 탭에서 공간을 확보한 뒤 다시 수령하세요. 수집품은 차감하지 않았습니다.";
                break;
            case QuestRequestResult.Code.Dead:
                reason = "캐릭터 사망";
                next = "부활 후 다시 실행하세요.";
                break;
            case QuestRequestResult.Code.Online:
            case QuestRequestResult.Code.Joining:
                reason = "온라인 또는 접속 준비 상태";
                next = "오프라인 캐릭터로 돌아와 검증하세요.";
                break;
            case QuestRequestResult.Code.NotReady:
                reason = "캐릭터 준비 미완료";
                next = "캐릭터 초기화를 마친 뒤 현재 로컬 캐릭터를 연결하세요.";
                break;
            case QuestRequestResult.Code.Busy:
                reason = "다른 퀘스트 요청 처리 중";
                next = "현재 요청이 완료된 뒤 다시 실행하세요.";
                break;
            case QuestRequestResult.Code.QuestMissing:
                reason = "퀘스트 정의 없음";
                next = "실제 데이터에 존재하는 퀘스트를 목록에서 선택하세요.";
                break;
            default:
                reason = "퀘스트·아이템 정의 수치 오류";
                next = "표시된 ID와 수량·목표·보상 정의를 확인하세요.";
                break;
        }
        string related = string.Empty;
        if (request.Reason == QuestRequestResult.Code.Prerequisite &&
            data.QuestTable.TryGetValue(request.RelatedId, out QuestData required))
        {
            related = $"선행 퀘스트: {required.ID} · {required.Name}\n";
        }
        else if (request.Reason == QuestRequestResult.Code.KillTarget &&
            data.MonsterTable.TryGetValue(request.RelatedId, out MonsterData monster))
        {
            related = $"목표 몬스터: {monster.ID} · {monster.Name}\n";
        }
        outcome.RequestSummary = request.Succeeded ? reason : $"{(claim ? "보상 수령" : "퀘스트 수락")} 실패 — {reason}";
        string message = request.Reason == QuestRequestResult.Code.ItemTarget
            ? "모든 수집 목표를 충족해야 보상을 받을 수 있습니다." : request.Message;
        var detail = new StringBuilder($"퀘스트: {quest.ID} · {quest.Name}\n" + related + message);
        if (request.ItemProgress.Count > 0)
        {
            detail.AppendLine("\n수령 판정 당시 수집 아이템별 상태:");
            foreach (ItemRequirementProgress row in request.ItemProgress)
            {
                detail.AppendLine($"{row.ItemId} · {row.Name}: {row.Current}/{row.Required} · " +
                    (row.Satisfied ? "충족" : $"{row.Missing}개 부족"));
            }
        }
        outcome.RequestDetail = detail.ToString();
        outcome.NextAction = next;
        return outcome;
    }
}
