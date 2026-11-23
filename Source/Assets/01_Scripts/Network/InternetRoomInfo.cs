/// <summary>
/// 공개 방 목록에 표시할 접속 정보
/// </summary>
public sealed class InternetRoomInfo
{
    public string Id { get; }
    public string Name { get; }
    public int PlayerCount { get; }
    public int MaxPlayers { get; }

    public InternetRoomInfo(string id, string name, int playerCount, int maxPlayers)
    {
        Id = id;
        Name = name;
        PlayerCount = playerCount;
        MaxPlayers = maxPlayers;
    }
}
