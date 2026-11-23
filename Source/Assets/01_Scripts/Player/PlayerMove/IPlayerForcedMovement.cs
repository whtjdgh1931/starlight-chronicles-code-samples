/// <summary>
/// 플레이어 강제 이동 계약
/// </summary>
public interface IPlayerForcedMovement
{
    int SightDirection { get; }
    bool IsForcedMoving { get; }

    /// <summary>
    /// 일반 이동 상태에서 강제 이동 시작 요청
    /// </summary>
    bool TryBeginForcedMovement();

    void MoveAtSpeed(float directionX, float speed);

    void EndForcedMovement();

    bool HasWallAhead(float directionX);
}