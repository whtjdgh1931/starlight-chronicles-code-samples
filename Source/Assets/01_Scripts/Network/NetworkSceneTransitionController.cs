/// <summary>
/// 공동 씬 이동 단계와 신규 참가 허용 여부 관리
/// NGO 실행 흐름에서 사용하는 전이 조건과 참가 정책
/// </summary>
public sealed class NetworkSceneTransitionController
{
    private enum TransitionStage
    {
        Idle,
        Loading,
        Preparing,
        Playing
    }

    private TransitionStage _stage;

    /// <summary>
    /// 출발 전 신규 참가 허용 여부
    /// HostScene·정원·종료 조건은 세션에서 확인
    /// </summary>
    public bool CanAcceptParticipants => _stage == TransitionStage.Idle;
    public bool IsLoading => _stage == TransitionStage.Loading;
    public bool IsPreparing => _stage == TransitionStage.Preparing;
    public bool IsTransitioning => IsLoading || IsPreparing;

    /// <summary>
    /// 서버 출발 조건 확인 후 로딩 단계 진입
    /// 중복 출발 요청은 상태 변경 없이 거절
    /// </summary>
    public bool TryBeginLoading()
    {
        if (_stage != TransitionStage.Idle)
        {
            return false;
        }

        _stage = TransitionStage.Loading;
        return true;
    }

    /// <summary>
    /// 대상 씬 로딩 완료 후 맵 준비 단계 진입
    /// </summary>
    public bool TryBeginPreparing()
    {
        if (_stage != TransitionStage.Loading)
        {
            return false;
        }

        _stage = TransitionStage.Preparing;
        return true;
    }

    /// <summary>
    /// 참가자 전원의 맵 준비 확인 후 전투 단계 진입
    /// </summary>
    public bool TryBeginPlaying()
    {
        if (_stage != TransitionStage.Preparing)
        {
            return false;
        }

        _stage = TransitionStage.Playing;
        return true;
    }

    /// <summary>
    /// 세션 종료 시 다음 접속을 위한 상태 초기화
    /// </summary>
    public void Reset()
    {
        _stage = TransitionStage.Idle;
    }
}
