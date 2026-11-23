using System;

/// <summary>
/// 캐릭터 선택 씬과 플레이 씬 사이에서 선택된 저장 슬롯을 전달
/// </summary>
public sealed class CharacterSession
{
    private int? _selectedSlotId;

    public bool HasSelectedCharacter => _selectedSlotId.HasValue;

    /// <summary>
    /// 선택된 캐릭터 슬롯을 반환
    /// 캐릭터가 선택되지 않았다면 초기화 오류로 처리
    /// </summary>
    public int SelectedSlotId
    {
        get
        {
            if (!_selectedSlotId.HasValue)
            {
                throw new InvalidOperationException("선택된 캐릭터 슬롯이 없습니다.");
            }

            return _selectedSlotId.Value;
        }
    }

    /// <summary>
    /// 플레이 씬에서 사용할 캐릭터 슬롯을 선택
    /// </summary>
    public void SelectCharacter(int slotId)
    {
        if (slotId < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(slotId),
                slotId,
                "캐릭터 슬롯 ID는 0 이상이어야 합니다.");
        }

        _selectedSlotId = slotId;
    }

    /// <summary>
    /// 타이틀 또는 캐릭터 선택 화면으로 돌아갈 때 기존 선택을 해제
    /// </summary>
    public void Clear()
    {
        _selectedSlotId = null;
    }

}
