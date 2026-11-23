using System;

/// <summary>
/// 캐릭터 선택창에 표시할 요약 정보를 담은 클래스
/// </summary>
public sealed class CharacterSlotSummary
{
    public int SlotId { get; }
    public PlayerClass Class { get; }
    public int Level { get; }

    public CharacterSlotSummary(int slotId, PlayerClass playerClass, int level)
    {
        if (slotId < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(slotId));
        }

        if (playerClass == PlayerClass.None || !Enum.IsDefined(typeof(PlayerClass), playerClass))
        {
            throw new ArgumentOutOfRangeException(nameof(playerClass));
        }

        if (level < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(level));
        }

        SlotId = slotId;
        Class = playerClass;
        Level = level;
    }
}
