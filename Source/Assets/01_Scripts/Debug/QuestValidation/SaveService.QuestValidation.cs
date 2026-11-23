#if UNITY_EDITOR
/// <summary>
/// 삭제 가능한 퀘스트 검증 도구의 저장 실패 주입 확장
/// </summary>
public sealed partial class SaveService
{
    internal int SlotIdForTesting => _slotId;
    internal int SimulatedWriteFailureCount { get; private set; }

    internal System.IDisposable FailNextWriteForTesting()
    {
        if (_writer is SaveFailureScope)
        {
            throw new System.InvalidOperationException("저장 실패 검증 범위가 이미 활성화되어 있습니다.");
        }
        var scope = new SaveFailureScope(this, _writer);
        _writer = scope;
        return scope;
    }

    /// <summary>
    /// 실제 파일 저장기를 감싼 일회성 실패와 원래 저장기 복구
    /// </summary>
    private sealed class SaveFailureScope : ISaveWriter, System.IDisposable
    {
        private SaveService _owner;
        private readonly ISaveWriter _original;
        private int _remaining = 1;

        internal SaveFailureScope(SaveService owner, ISaveWriter original)
        {
            _owner = owner;
            _original = original;
        }

        public bool TryWrite(string json, out string errorMessage)
        {
            if (_remaining > 0)
            {
                _remaining--;
                _owner.SimulatedWriteFailureCount++;
                errorMessage = "[검증 도구] 저장 쓰기 1회 실패 주입";
                return false;
            }
            return _original.TryWrite(json, out errorMessage);
        }

        public void Dispose()
        {
            if (_owner == null) { return; }
            _owner._writer = _original;
            _owner = null;
        }
    }
}
#endif
