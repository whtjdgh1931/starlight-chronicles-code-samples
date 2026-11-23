#if UNITY_EDITOR
/// <summary>
/// 삭제 가능한 퀘스트 검증 도구의 저장 담당 접근 확장
/// </summary>
public partial class PlayerSaveController
{
    public int SlotIdForTesting
    {
        get
        {
            EnsureInitialized();
            return _saveService.SlotIdForTesting;
        }
    }

    public int SimulatedWriteFailureCount
    {
        get
        {
            EnsureInitialized();
            return _saveService.SimulatedWriteFailureCount;
        }
    }

    /// <summary>
    /// using 범위에 한정한 저장 쓰기 실패 주입
    /// </summary>
    public System.IDisposable FailNextWriteForTesting()
    {
        EnsureRuntimeReady();
        return _saveService.FailNextWriteForTesting();
    }
}
#endif
