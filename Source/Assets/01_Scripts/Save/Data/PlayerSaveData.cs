using System;

/// <summary>
/// 플레이어 상황 저장용 클래스
/// </summary>
[Serializable]
public sealed class PlayerSaveData
{
    public PlayerClass Class;
    public int Level;
    public float CurrentHp;
    public float CurrentMp;
    public float CurrentExp;
}
