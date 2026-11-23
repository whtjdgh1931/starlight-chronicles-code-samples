using System;

/// <summary>사냥터 난이도와 구역별 몬스터 생성 규칙</summary>
[Serializable]
public sealed class HuntSpawnData
{
    public int ID;
    public HuntDifficulty Difficulty;
    public HuntSpawnZone ZoneId;
    public int MonsterID;
    public int MaxAlive;
    public int BatchCount;
    public float SpawnInterval;
    public float InitialDelay;
}
