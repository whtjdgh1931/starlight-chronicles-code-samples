# 협동 플레이 입력 반응

협동 보스전에서 참가자는 입력을 보낸 뒤 서버 위치와 행동이 돌아올 때까지 기다립니다. 이동과 점프에는 참가자가 조작하는 캐릭터의 위치 보간 시간을 40ms로 제한하고, 기본 공격에는 입력 순간 짧은 준비 자세를 표시했습니다.

호스트 및 다른 플레이어의 보간 설정에는 기존 값을 사용합니다. 참가자의 네트워크 객체가 사라질 때 임시로 바꾼 보간 시간을 복원합니다. 실제 공격 실행, 투사체 생성, 피해 계산은 서버에서 처리합니다. 참가자의 준비 자세는 시각 피드백이며, 서버가 확정한 공격 모션이 재생될 때 종료됩니다.

## NetworkPlayerController 발췌

```csharp
private float _ownerPositionInterpolationTime = 0.04f;

private void ApplyOwnerPositionInterpolation()
{
    if (!IsClient || !IsOwner || IsServer ||
        _networkTransform.PositionMaxInterpolationTime <= _ownerPositionInterpolationTime) { return; }

    _originalPositionInterpolationTime = _networkTransform.PositionMaxInterpolationTime;
    _networkTransform.PositionMaxInterpolationTime = _ownerPositionInterpolationTime;
}

/// <summary>네트워크 객체 반환 시 기존 보간 설정 복원</summary>
private void RestorePositionInterpolation()
{
    if (_originalPositionInterpolationTime is not float originalTime) { return; }

    _networkTransform.PositionMaxInterpolationTime = originalTime;
    _originalPositionInterpolationTime = null;
}
private void HandleBasicAttackInput()
{
    if (_playBasicAttackWindup && IsSpawned && IsOwner && !IsServer &&
        _isVisualReady && _isOwnerInputSubscribed && _life.CanAct && !_player.CheckIsStun())
    {
        _playerVisual.PlayBasicAttackWindup();
    }
    SendInput(false, AttackIntent.Basic);
}
```
