using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 오브젝트 풀링 설정 용 ScriptableObject 컨테이너
/// </summary>
[CreateAssetMenu(fileName = "PoolableObjectSO", menuName = "Scriptable Objects/PoolableObjectSO", order = 1)]
public class PoolableObjectSO : ScriptableObject
{
    [System.Serializable]
    public struct PoolData
    {
        [Tooltip("풀링 프리팹")]
        public GameObject Prefab;

        [Tooltip("미리 생성할 개수")]
        public int InitCnt;

        [Tooltip("활성화 최대 개수(0이면 무제한)")]
        public int maxCnt;
    }

    [SerializeField]
    private List<PoolData> _poolList = new List<PoolData>();
    public List<PoolData> PoolList => _poolList;



}
