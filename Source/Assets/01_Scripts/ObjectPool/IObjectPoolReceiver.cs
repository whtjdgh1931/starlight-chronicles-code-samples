/// <summary>
/// 생성 또는 맵 전환 시 필요한 오브젝트 풀 수신
/// </summary>
public interface IObjectPoolReceiver
{
    void SetObjectPool(ObjectPool objectPool);
}
