using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

/// <summary>
/// 서버 검기의 이동·관통 피해·수명과 NGO 생성·제거 관리
/// 클라이언트는 NetworkTransform 위치와 발사 연출 표시
/// </summary>
public sealed class NetworkPlayerProjectile : NetworkBehaviour
{
    [SerializeField] private Rigidbody2D _body;
    [SerializeField] private SwordWavePresentation _presentation;
    private readonly HashSet<MonsterBase> _hitMonsters = new();
    private Player _attacker;
    private ulong _soundOwnerClientId;
    private float _damage;
    private int _maxTargets;
    private double _expiresAt;


    /// <summary>
    /// 서버 생성 이전 실제 발사자와 최초 동기화할 소유자 ID 준비
    /// 투사체 자체의 서버 소유권과 발사자 소유권 구분
    /// </summary>
    public void PrepareAttacker(Player attacker)
    {
        _attacker = attacker;
        _soundOwnerClientId = attacker.GetComponent<NetworkObject>().OwnerClientId;
    }

    /// <summary>초기 생성 정보에 발사자 ID를 한 번 포함</summary>
    protected override void OnSynchronize<T>(ref BufferSerializer<T> serializer)
    {
        serializer.SerializeValue(ref _soundOwnerClientId);
    }

    public override void OnNetworkSpawn()
    {
        MapManager.Instance.ObjectPool.Bind(gameObject);
        _body.linearVelocity = Vector2.zero;
        _body.angularVelocity = 0f;
        _body.simulated = IsServer;
        if (IsClient)
        {
            _presentation.SetSoundOwnership(_soundOwnerClientId == NetworkManager.LocalClientId);
            _presentation.PlayLaunch();
        }
    }

    /// <summary>
    /// 서버 Spawn 직후 시트 기반 피해·속도·수명·관통 설정
    /// </summary>
    public void Launch(float damage, Vector2 direction, float speed, float lifetime, int maxTargets)
    {
        _damage = damage;
        _maxTargets = maxTargets;
        _expiresAt = Time.timeAsDouble + lifetime;
        _body.linearVelocity = direction.normalized * speed;
    }

    private void FixedUpdate()
    {
        if (IsSpawned && IsServer && (Time.timeAsDouble >= _expiresAt || _attacker == null || !_attacker.CanBeMonsterTarget))
        {
            NetworkObject.Despawn(destroy: true);
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!IsSpawned || !IsServer || _attacker == null || !_attacker.CanBeMonsterTarget)
        {
            return;
        }
        if (other.gameObject.layer == Constants.LAYER_Wall)
        {
            NetworkObject.Despawn(destroy: true);
            return;
        }

        MonsterBase monster = other.GetComponentInParent<MonsterBase>();
        if (monster == null || !_hitMonsters.Add(monster))
        {
            return;
        }

        Vector2 hitPosition = other.ClosestPoint(transform.position);
        monster.TakeDamage(_damage, transform.position, _attacker, hitPosition);
        if (_hitMonsters.Count >= _maxTargets)
        {
            NetworkObject.Despawn(destroy: true);
        }
    }

    public override void OnNetworkDespawn()
    {
        _body.linearVelocity = Vector2.zero;
        _body.angularVelocity = 0f;
        _body.simulated = false;
        _attacker = null;
        _soundOwnerClientId = 0;
        _damage = 0f;
        _expiresAt = 0d;
        _maxTargets = 0;
        _hitMonsters.Clear();
        // 실제 풀 반환은 NGO 핸들러 한 곳에서 처리
    }
}
