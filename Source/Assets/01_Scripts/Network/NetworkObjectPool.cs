using System;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 기존 ObjectPool과 NGO 객체 생성·반환 핸들러 연결
/// 보관 풀은 세션 객체 아래 유지, 사용 중 객체는 현재 게임 씬에 배치
/// </summary>
public sealed class NetworkObjectPool
{
    private readonly ObjectPool _pool;

    public NetworkObjectPool(Transform sessionRoot)
    {
        var root = new GameObject("NetworkObjectPool");
        root.transform.SetParent(sessionRoot, false);
        _pool = root.AddComponent<ObjectPool>();
        _pool.SetCombatSoundPlayer(GameManager.Instance.CombatSoundPlayer);
    }

    /// <summary>
    /// 호스트·클라이언트 시작 전 등록된 네트워크 프리팹의 생성·반환 경로 등록
    /// 세션 재시작 시 NGO 핸들러도 재등록
    /// </summary>
    public void Register(NetworkManager manager)
    {
        manager.NetworkConfig.Prefabs.Initialize();
        foreach (NetworkPrefab entry in manager.NetworkConfig.Prefabs.Prefabs)
        {
            GameObject prefab = entry.Prefab;
            manager.PrefabHandler.RemoveHandler(prefab);
            if (!manager.PrefabHandler.AddHandler(prefab, new Handler(this, prefab)))
            {
                throw new InvalidOperationException($"{prefab.name}: 네트워크 풀 등록 실패");
            }
        }
    }

    /// <summary>
    /// 풀에서 가져온 객체를 네트워크 생성 전 월드 위치에 배치
    /// NGO 부모 동기화를 피하기 위해 풀 보관 부모에서 분리
    /// </summary>
    public NetworkObject Pop(GameObject prefab, Vector3 position, Quaternion rotation)
    {
        GameObject instance = _pool.Pop(prefab);
        instance.transform.SetParent(null, false);
        SceneManager.MoveGameObjectToScene(instance, SceneManager.GetActiveScene());
        instance.transform.SetPositionAndRotation(position, rotation);
        return instance.GetComponent<NetworkObject>();
    }

    /// <summary>
    /// 네트워크 생성 전 초기화 실패 객체의 원본 풀 반환
    /// 생성된 객체는 NGO Despawn 경로 사용
    /// </summary>
    public void Push(NetworkObject networkObject)
    {
        _pool.Push(networkObject.gameObject);
    }

    private sealed class Handler : INetworkPrefabInstanceHandler
    {
        private readonly NetworkObjectPool _pool;
        private readonly GameObject _prefab;

        public Handler(NetworkObjectPool pool, GameObject prefab)
        {
            _pool = pool;
            _prefab = prefab;
        }

        public NetworkObject Instantiate(ulong ownerClientId, Vector3 position, Quaternion rotation)
        {
            return _pool.Pop(_prefab, position, rotation);
        }

        public void Destroy(NetworkObject networkObject)
        {
            _pool.Push(networkObject);
        }
    }
}
