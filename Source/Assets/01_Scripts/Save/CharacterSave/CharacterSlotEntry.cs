
/// <summary>
/// 캐릭터 슬롯 조회 결과를 나타내는 클래스
/// </summary>
public sealed class CharacterSlotEntry
{
    public int SlotId { get; }
    public CharacterSlotState State { get; }
    public CharacterSlotSummary Summary { get; }
    public string ErrorMessage { get; }



    public CharacterSlotEntry(
        int slotId,
        CharacterSlotState state,
        CharacterSlotSummary summary,
        string errorMessage)
    {
        SlotId = slotId;
        State = state;
        Summary = summary;
        ErrorMessage = errorMessage;
    }
}
