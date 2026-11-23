using System.Text;
using Unity.Collections;
using Unity.Netcode;

/// <summary>
/// 캐릭터 접속 JSON의 UTF-8 메시지 변환과 크기 검사
/// </summary>
public static class CharacterJoinMessage
{
    public const string Name = "CharacterJoinRequest";
    private static readonly UTF8Encoding Utf8 = new(false, true);

    public static bool IsWithinLimit(string json, int maxBytes)
    {
        int byteCount = Utf8.GetByteCount(json);
        return byteCount > 0 && byteCount <= maxBytes;
    }

    /// <summary>
    /// 연결 완료 후 준비된 요청을 서버에 1회 전송
    /// </summary>
    public static void Send(NetworkManager networkManager, string json)
    {
        byte[] payload = Utf8.GetBytes(json);
        using var writer = new FastBufferWriter(payload.Length, Allocator.Temp);
        writer.WriteBytesSafe(payload);
        networkManager.CustomMessagingManager.SendNamedMessage(
            Name, NetworkManager.ServerClientId, writer, NetworkDelivery.ReliableFragmentedSequenced);
    }

    /// <summary>
    /// 외부 메시지의 크기를 확인한 뒤 JSON 복원
    /// </summary>
    public static bool TryRead(FastBufferReader reader, int maxBytes, out string json, out string errorMessage)
    {
        json = string.Empty;
        int byteCount = reader.Length - reader.Position;
        if (byteCount <= 0 || byteCount > maxBytes)
        {
            errorMessage = "접속 요청 크기 오류";
            return false;
        }

        byte[] payload = new byte[byteCount];
        reader.ReadBytesSafe(ref payload, byteCount);
        try
        {
            json = Utf8.GetString(payload);
        }
        catch (DecoderFallbackException)
        {
            errorMessage = "접속 요청 UTF-8 형식 오류";
            return false;
        }

        errorMessage = string.Empty;
        return true;
    }
}
