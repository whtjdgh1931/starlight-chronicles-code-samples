using Unity.Netcode;
using UnityEngine;

/// <summary>
/// 서버 보스 투사체의 이동·단일 피해·수명과 NGO 제거 처리
/// </summary>
public sealed class NetworkBossProjectile : NetworkBehaviour
{
    [SerializeField] private Rigidbody2D _body;
    private MonsterBase _owner;
    private TrailRenderer _trail;
    private float _damage;
    private double _expiresAt;

    public override void OnNetworkSpawn()
    {
        _body.linearVelocity = Vector2.zero;
        _body.angularVelocity = 0f;
        _body.simulated = IsServer;
        _trail = BossProjectileTrail.Play(transform, _trail);
    }

    /// <summary>
    /// 서버 Spawn 직후 패턴 데이터와 발사자 연결
    /// </summary>
    public void Launch(float damage, Vector2 direction, float speed, float lifetime, MonsterBase owner)
    {
        _owner = owner;
        _damage = damage;
        _expiresAt = Time.timeAsDouble + lifetime;
        _body.linearVelocity = direction.normalized * speed;
    }

    private void FixedUpdate()
    {
        if (IsSpawned && IsServer &&
            (Time.timeAsDouble >= _expiresAt || _owner == null || !_owner.CanDealDamage))
        {
            NetworkObject.Despawn(destroy: true);
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!IsSpawned || !IsServer || _owner == null || !_owner.CanDealDamage)
        {
            return;
        }
        if (other.CompareTag(Constants.TAG_Ground))
        {
            NetworkObject.Despawn(destroy: true);
            return;
        }
        Player player = other.GetComponentInParent<Player>();
        if (player == null || !player.IsNetworkControlled || player.IsDead)
        {
            return;
        }

        // 피해 이벤트 이전 물리 접촉 중단으로 중복 충돌 차단
        _body.simulated = false;
        player.TakeDamage(_damage, transform.position);
        NetworkObject.Despawn(destroy: true);
    }

    public override void OnNetworkDespawn()
    {
        BossProjectileTrail.Stop(_trail);
        _body.linearVelocity = Vector2.zero;
        _body.angularVelocity = 0f;
        _body.simulated = false;
        _owner = null;
        _damage = 0f;
        _expiresAt = 0d;
        // 실제 풀 반환은 NGO 핸들러 한 곳에서 처리
    }
}
