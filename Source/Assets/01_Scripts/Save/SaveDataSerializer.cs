using System;
using UnityEngine;

public sealed class SaveDataSerializer
{
    public string Serialize(SaveData saveData, bool prettyPrint = true)
    {
        return JsonUtility.ToJson(saveData, prettyPrint);
    }

    public bool TryDeserialize(string json, out SaveData saveData, out string errorMessage)
    {
        saveData = null;

        if (string.IsNullOrWhiteSpace(json))
        {
            return Fail("저장 JSON이 비어 있습니다.", out errorMessage);
        }

        try
        {
            saveData = JsonUtility.FromJson<SaveData>(json);
        }
        catch (ArgumentException exception)
        {
            return Fail($"저장 JSON 형식이 올바르지 않습니다. {exception.Message}", out errorMessage);
        }

        if (saveData == null)
        {
            return Fail("저장 JSON에서 데이터를 생성하지 못했습니다.", out errorMessage);
        }

        errorMessage = string.Empty;
        return true;
    }

    private static bool Fail(string message, out string errorMessage)
    {
        errorMessage = message;
        return false;
    }

}
