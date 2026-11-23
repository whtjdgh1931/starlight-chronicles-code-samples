using System;

/// <summary>
/// 강화 요청 전달과 확정 결과 수신
/// </summary>
public interface IEnhancementRequestHandler
{
    /// <summary>
    /// 요청 식별자, 강화 결과, 확정 강화 단계 전달
    /// </summary>
    event Action<Guid, EnhancementResult, int> Completed;

    /// <summary>
    /// 로컬 아이템 참조를 사용한 강화 요청
    /// 네트워크 전송 시 별도 아이템 식별 정보로 변환 필요
    /// </summary>
    void RequestEnhancement(Guid requestId, InventoryItemData item);
}
