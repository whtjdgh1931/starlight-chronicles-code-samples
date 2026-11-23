# 보스 투사체 예고

보스는 준비 중 플레이어를 추적하고, 준비 종료 0.12초 전에 발사점과 각 탄의 방향을 확정합니다. 예고를 그릴 때 사용한 좌표와 방향을 실제 공격 실행에서도 사용합니다. 네트워크에서 중간 예고는 전송 간격을 적용하고, 최종 예고는 전송 간격과 관계없이 전달합니다.

## BossFanProjectileAttack 발췌

```csharp
protected override void UpdateTelegraph(Player target)
{
    if (!_aimLocked)
    {
        ShowProjectilePaths(target, PreparationTimeRemaining <= _finalAimLeadTime);
    }
}

protected override bool TryExecuteAttack(Player target)
{
    float damage = MonsterStats.Atk * AttackData.DamageMultiplier;
    _projectileEmitter.FireAlongPaths(_preparedFirePosition, _preparedDirections, damage, AttackData.ProjectileSpeed, AttackData.ProjectileLifetime, Monster);
    Telegraph.PlayMuzzleFlash(_preparedFirePosition, _preparedAimDirection);
    return true;
}

private void ShowProjectilePaths(Player target, bool isFinal)
{
    if (target.transform.position.x != Monster.transform.position.x) { FaceTarget(target); }

    _preparedFirePosition = GetFirePosition();
    Vector2 targetPosition = target.transform.position;
    _preparedAimDirection = _projectileEmitter.CalculateDirections(_preparedFirePosition, targetPosition, _projectileCount, _spreadAngle, Monster.FacingDirection, _preparedDirections);
    _aimLocked = isFinal;
    Telegraph.ShowPaths(_preparedFirePosition, _preparedDirections, AttackData.ProjectileSpeed * AttackData.ProjectileLifetime, AttackData.PreparationDuration, isFinal);
}
```

## NetworkBossTelegraph 발췌

```csharp
private void HandlePathsShown(Vector2 origin, IReadOnlyList<Vector2> directions, float length, float duration, bool isFinal)
{
    double now = Time.unscaledTimeAsDouble;
    if (!IsSpawned || (!isFinal && now < _nextPathsAt))
    {
        return;
    }
    _nextPathsAt = now + _positionInterval;
    _positionAt = double.PositiveInfinity;
    var copy = new Vector2[directions.Count];
    for (int i = 0; i < copy.Length; i++)
    {
        copy[i] = directions[i];
    }
    ShowPathsRpc(origin, copy, length, duration);
}
```

## 검증 기록

- 실제 보스 FSM과 Animator 공격 이벤트로 5발을 생성했습니다. 5발 모두 마지막 예고 방향과 일치했고, 준비 중 피해 없이 실제 투사체 충돌 후 HP가 감소했습니다.
- 별도 로컬 NGO 호스트·클라이언트 검사에서는 연속 예고 중 갱신 한 건이 전송 간격에 걸려 생략됐고, 최종 예고는 전달됐습니다. 참가자가 받은 마지막 발사점과 모든 방향이 서버 값과 일치했습니다.
