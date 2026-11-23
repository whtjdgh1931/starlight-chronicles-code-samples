using System;

public sealed class CharacterSlotSummaryFactory
{
    public bool TryCreate(
            int slotId,
            SaveData saveData,
            out CharacterSlotSummary summary,
            out string errorMessage)
    {
        summary = null;

        if (saveData == null)
        {
            return Fail("저장 데이터가 없습니다.", out errorMessage);
        }

        saveData.MigrateLegacyVersion();
        if (saveData.Version != SaveData.CurrentVersion)
        {
            return Fail($"지원하지 않는 저장 버전입니다. Version: {saveData.Version}", out errorMessage);
        }

        if (saveData.Player == null)
        {
            return Fail("플레이어 저장 데이터가 없습니다.", out errorMessage);
        }

        PlayerSaveData playerData = saveData.Player;

        if (playerData.Class == PlayerClass.None || !Enum.IsDefined(typeof(PlayerClass), playerData.Class))
        {
            return Fail($"올바르지 않은 직업입니다. Class: {playerData.Class}", out errorMessage);
        }

        if (playerData.Level < 1)
        {
            return Fail($"올바르지 않은 레벨입니다. Level: {playerData.Level}", out errorMessage);
        }

        summary = new CharacterSlotSummary(slotId, playerData.Class, playerData.Level);

        errorMessage = string.Empty;
        return true;
    }

    private static bool Fail(string message, out string errorMessage)
    {
        errorMessage = message;
        return false;
    }
}
