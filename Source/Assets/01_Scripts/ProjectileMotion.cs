using System;
using UnityEngine;

/// <summary>
/// 풀링 투사체의 직선 이동과 수명 관리
/// </summary>
[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(PoolableObject))]
public sealed class ProjectileMotion : MonoBehaviour
{
    [SerializeField] private Rigidbody2D _rigidbody;
    [SerializeField] private PoolableObject _poolableObject;
    [SerializeField] private float _rotationOffset;

    /// <summary>
    /// 필수 참조 검증
    /// </summary>
    private void ValidateReferences()
    {
        if (_rigidbody == null)
        {
            throw new MissingReferenceException($"{name}: Rigidbody2D 참조 필요");
        }

        if (_poolableObject == null)
        {
            throw new MissingReferenceException($"{name}: PoolableObject 참조 필요");
        }
    }

    /// <summary>
    /// 투사체 이동과 반환 예약 시작
    /// </summary>
    public void Launch(Vector2 direction, float speed, float lifetime)
    {
        ValidateReferences();

        if (direction.sqrMagnitude <= Constants.FLOAT_CheckFloat * Constants.FLOAT_CheckFloat)
        {
            throw new ArgumentException("투사체 이동 방향 필요", nameof(direction));
        }

        Vector2 normalizedDirection = direction.normalized;

        _rigidbody.linearVelocity = normalizedDirection * speed;

        float rotationZ = Mathf.Atan2(normalizedDirection.y, normalizedDirection.x) * Mathf.Rad2Deg;
        transform.rotation = Quaternion.Euler(0f, 0f, rotationZ + _rotationOffset);

        _poolableObject.ScheduleRelease(lifetime);
    }

    /// <summary>
    /// 투사체 풀 반환
    /// </summary>
    public void Release()
    {
        _poolableObject.Release();
    }

    private void OnDisable()
    {
        _rigidbody.linearVelocity = Vector2.zero;
        _rigidbody.angularVelocity = 0f;
    }
}
