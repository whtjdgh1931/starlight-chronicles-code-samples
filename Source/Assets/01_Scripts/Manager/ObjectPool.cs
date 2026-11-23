using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Pool;

/// <summary>
/// Unity IObjectPool 기반의 GameObject 풀을 관리합니다.
/// </summary>
public class ObjectPool : MonoBehaviour
{
    [SerializeField] private PoolableObjectSO _poolableObjectSO;
    private CombatSoundPlayer _combatSoundPlayer;

    /// <summary>
    /// 풀 객체에 전달할 공통 전투 사운드 연결
    /// </summary>
    public void SetCombatSoundPlayer(CombatSoundPlayer soundPlayer)
    {
        _combatSoundPlayer = soundPlayer;
    }

    private readonly Dictionary<GameObject, IObjectPool<GameObject>> _pools =
        new Dictionary<GameObject, IObjectPool<GameObject>>();

    /// <summary>
    /// 대상 객체와 자식의 풀 사용 컴포넌트 연결
    /// 객체 생성 또는 맵 변경 시 호출
    /// </summary>
    public void Bind(GameObject target)
    {
        foreach (MonoBehaviour component in target.GetComponentsInChildren<MonoBehaviour>(true))
        {
            if (component is IObjectPoolReceiver receiver)
            {
                receiver.SetObjectPool(this);
            }
            if (component is ICombatSoundReceiver soundReceiver)
            {
                soundReceiver.SetCombatSoundPlayer(_combatSoundPlayer);
            }
        }
    }

    public void Initialize()
    {
        if (_poolableObjectSO == null || _poolableObjectSO.PoolList == null)
        {
            Debug.LogWarning("PoolableObjectSO 등록 오류");
            return;
        }

        foreach (var element in _poolableObjectSO.PoolList)
        {
            if (element.Prefab == null)
            {
                continue;
            }

            CreateNewPool(element.Prefab, element.InitCnt, element.maxCnt);
        }
    }

    /// <summary>
    /// 풀에서 오브젝트를 가져옵니다.
    /// 등록되지 않은 프리팹은 새로운 풀을 생성합니다.
    /// </summary>
    public GameObject Pop(
        GameObject prefab,
        Transform parentTransform = null)
    {
        if (prefab == null)
        {
            throw new MissingReferenceException(
                $"{name}: Pop에 전달된 프리팹이 없습니다.");
        }

        if (!_pools.TryGetValue(prefab, out var pool))
        {
            pool = CreateNewPool(prefab);
        }

        GameObject obj = pool.Get();

        if (parentTransform != null)
        {
            obj.transform.SetParent(parentTransform, false);
        }

        return obj;
    }

    /// <summary>
    /// 오브젝트를 원본 풀에 반환합니다.
    /// </summary>
    public void Push(GameObject obj)
    {
        if (obj == null)
        {
            return;
        }

        if (!obj.TryGetComponent(out PoolableObject poolableObject))
        {
            throw new MissingComponentException(
                $"{obj.name}: PoolableObject가 필요합니다.");
        }

        poolableObject.Release();
    }

    /// <summary>
    /// 등록되지 않은 프리팹의 풀을 생성합니다.
    /// </summary>
    private IObjectPool<GameObject> CreateNewPool(
        GameObject prefab,
        int initialCount = 5,
        int maxCount = 0)
    {
        if (_pools.TryGetValue(prefab, out var existingPool))
        {
            return existingPool;
        }

        Transform root = new GameObject($"Pool_{prefab.name}").transform;
        root.SetParent(transform, false);

        IObjectPool<GameObject> newPool = null;

        newPool = new ObjectPool<GameObject>(
            createFunc: () =>
            {
                GameObject obj = Instantiate(prefab, root);

                if (!obj.TryGetComponent(out PoolableObject poolableObject))
                {
                    poolableObject = obj.AddComponent<PoolableObject>();
                }

                Bind(obj);
                poolableObject.SetOriginPool(newPool);
                return obj;
            },
            actionOnGet: (obj) =>
            {
                obj.SetActive(true);
            },
            actionOnRelease: (obj) =>
            {
                obj.transform.SetParent(root, false);
                obj.SetActive(false);
            },
            actionOnDestroy: (obj) =>
            {
                Destroy(obj);
            },
            collectionCheck: true,
            defaultCapacity: initialCount,
            maxSize: maxCount == 0 ? int.MaxValue : maxCount);

        _pools.Add(prefab, newPool);

        List<GameObject> warmUpObjects =
            new List<GameObject>(initialCount);

        for (int i = 0; i < initialCount; i++)
        {
            warmUpObjects.Add(newPool.Get());
        }

        foreach (GameObject obj in warmUpObjects)
        {
            newPool.Release(obj);
        }

        return newPool;
    }
}