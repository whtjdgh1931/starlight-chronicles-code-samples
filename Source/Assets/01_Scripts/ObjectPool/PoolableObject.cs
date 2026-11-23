using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Pool;

/// <summary>
/// 풀링되는 GameObject의 반환 상태와 지연 반환을 관리합니다.
/// </summary>
public class PoolableObject : MonoBehaviour
{
    [SerializeField]
    [Tooltip("0보다 크면 지정한 시간이 지난 후 풀에 반환됩니다.")]
    private float _releaseDelay;

    private IObjectPool<GameObject> _originPool;
    private Coroutine _releaseCoroutine;
    private bool _isReleased;

    private void OnEnable()
    {
        _isReleased = false;

        if (_releaseDelay > Constants.FLOAT_CheckFloat)
        {
            ScheduleRelease(_releaseDelay);
        }
    }

    /// <summary>
    /// 이 오브젝트를 관리하는 원본 풀을 설정합니다.
    /// </summary>
    public void SetOriginPool(IObjectPool<GameObject> pool)
    {
        _originPool = pool;
    }

    /// <summary>
    /// 지정 시간 이후 오브젝트 풀 반환 예약
    /// </summary>
    public void ScheduleRelease(float delay)
    {
        StopReleaseCoroutine();
        _releaseCoroutine = StartCoroutine(CoReleaseAfterDelay(delay));
    }

    private IEnumerator CoReleaseAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);

        _releaseCoroutine = null;
        Release();
    }

    /// <summary>
    /// 오브젝트를 원본 풀에 반환합니다.
    /// 중복 호출은 무시됩니다.
    /// </summary>
    public void Release()
    {
        if (_isReleased)
        {
            return;
        }

        if (_originPool == null)
        {
            throw new InvalidOperationException(
                $"{name}: 원본 오브젝트 풀이 설정되지 않았습니다.");
        }

        _isReleased = true;
        StopReleaseCoroutine();
        _originPool.Release(gameObject);
    }

    private void OnDisable()
    {
        StopReleaseCoroutine();
    }

    private void StopReleaseCoroutine()
    {
        if (_releaseCoroutine == null)
        {
            return;
        }

        StopCoroutine(_releaseCoroutine);
        _releaseCoroutine = null;
    }
}