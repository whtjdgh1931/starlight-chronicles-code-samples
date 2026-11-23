/// <summary>
/// 직렬화된 캐릭터 데이터의 저장 쓰기 계약
/// </summary>
public interface ISaveWriter
{
    bool TryWrite(string json, out string errorMessage);
}
