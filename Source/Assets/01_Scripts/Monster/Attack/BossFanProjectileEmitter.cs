using UnityEngine;

/// <summary>
/// 지정 위치에서 부채꼴 투사체를 생성하는 공통 발사기
/// </summary>
public sealed class BossFanProjectileEmitter : MonoBehaviour, IObjectPoolReceiver
{
    private const float MinimumAimDistance = 0.00001f;
    private ObjectPool _objectPool;
    private NetworkObjectPool _networkPool;

    /// <summary>
    /// 생성 또는 맵 진입 시 사용할 풀 연결
    /// </summary>
    public void SetObjectPool(ObjectPool objectPool)
    {
        _objectPool = objectPool;
    }

    /// <summary>
    /// 네트워크 생성 시 세션의 투사체 풀 연결
    /// </summary>
    public void SetNetworkPool(NetworkObjectPool networkPool)
    {
        _networkPool = networkPool;
    }

    [SerializeField] private MonsterProjectile _projectilePrefab;
    [SerializeField] private NetworkBossProjectile _networkProjectilePrefab;

    private readonly System.Collections.Generic.List<Vector2> _directions = new();

    /// <summary>
    /// 투사체 프리팹 참조 검증
    /// </summary>
    public void Initialize()
    {
        if (_projectilePrefab == null)
        {
            throw new MissingReferenceException($"{name}: 투사체 Prefab 참조 필요");
        }
    }

    /// <summary>
    /// 지정 방향 중심의 부채꼴 투사체 발사
    /// </summary>
    /// <returns>발사 섬광과 공유할 중심 조준 방향</returns>
    public Vector2 Fire(Vector2 firePosition, Vector2 targetPosition, int projectileCount, float spreadAngle, float damage, float speed, float lifetime, MonsterBase owner)
    {
        Vector2 aimDirection = CalculateDirections(firePosition, targetPosition, projectileCount, spreadAngle, owner.FacingDirection, _directions);
        FireAlongPaths(firePosition, _directions, damage, speed, lifetime, owner);
        return aimDirection;
    }

    /// <summary>확정된 발사점과 탄별 방향으로 투사체 생성</summary>
    public void FireAlongPaths(Vector2 firePosition, System.Collections.Generic.IReadOnlyList<Vector2> directions, float damage, float speed, float lifetime, MonsterBase owner)
    {
        for (int index = 0; index < directions.Count; index++)
        {
            SpawnProjectile(firePosition, directions[index], damage, speed, lifetime, owner);
        }
    }

    /// <summary>
    /// 실제 발사와 예고가 공유하는 방향 계산, 겹친 목표는 보스 시선 사용
    /// </summary>
    /// <param name="facingDirection">겹친 목표의 대체 방향, 음수는 왼쪽</param>
    /// <returns>탄막과 발사 섬광의 중심 단위 방향</returns>
    public Vector2 CalculateDirections(Vector2 firePosition, Vector2 targetPosition, int projectileCount, float spreadAngle, float facingDirection, System.Collections.Generic.List<Vector2> directions)
    {
        directions.Clear();

        Vector2 offset = targetPosition - firePosition;
        float distance = offset.magnitude;
        Vector2 centerDirection = distance > MinimumAimDistance ? offset / distance : facingDirection < 0f ? Vector2.left : Vector2.right;
        float startAngle = projectileCount > 1 ? -spreadAngle * 0.5f : 0f;
        float angleStep = projectileCount > 1 ? spreadAngle / (projectileCount - 1) : 0f;

        for (int index = 0; index < projectileCount; index++)
        {
            directions.Add(Quaternion.Euler(0f, 0f, startAngle + angleStep * index) * centerDirection);
        }
        return centerDirection;
    }

    private void SpawnProjectile(Vector2 position, Vector2 direction, float damage, float speed, float lifetime, MonsterBase owner)
    {
        // 온라인 보스 프리팹에만 네트워크 투사체 연결
        if (_networkProjectilePrefab != null)
        {
            float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
            Quaternion rotation = Quaternion.Euler(0f, 0f, angle);
            var networkObject = _networkPool.Pop(_networkProjectilePrefab.gameObject, position, rotation);
            NetworkBossProjectile projectile = networkObject.GetComponent<NetworkBossProjectile>();
            projectile.NetworkObject.Spawn(destroyWithScene: true);
            projectile.Launch(damage, direction, speed, lifetime, owner);
            return;
        }

        GameObject projectileObject = _objectPool.Pop(_projectilePrefab.gameObject);
        projectileObject.transform.position = position;
        projectileObject.GetComponent<MonsterProjectile>().Initialize(damage, direction, speed, lifetime, owner);
    }
}
