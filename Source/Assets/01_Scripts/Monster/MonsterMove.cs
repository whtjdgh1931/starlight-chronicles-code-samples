using UnityEngine;

/// <summary>
/// 몬스터의 이동을 관리하는 컴포넌트
/// </summary>
public class MonsterMove : MonoBehaviour
{
    [SerializeField] private LayerMask _wallLayerMask = 1 << Constants.LAYER_Wall;

    private MonsterBase _monster;
    private Rigidbody2D _rb;
    private Collider2D _collider;



    public void Initialize(MonsterBase monster)
    {
        _monster = monster;
        _rb = _monster.Rb;
        _collider = _monster.BodyCollider;

        _rb.bodyType = RigidbodyType2D.Dynamic;
        _rb.linearVelocity = Vector2.zero;
    }

    /// <summary>
    /// 몬스터 이동
    /// </summary>
    /// <param Name="direction">방향 지정</param>
    public void Move(Vector2 direction)
    {
        Move(direction, _monster.Data.Speed);
    }

    /// <summary>
    /// 지정 속도 기반 몬스터 이동
    /// </summary>
    public void Move(Vector2 direction, float speed)
    {
        float targetVelocityX = direction.x * speed;
        _rb.linearVelocity = new Vector2(targetVelocityX, _rb.linearVelocity.y);
    }

    /// <summary>
    /// 진행 방향의 낭떠러지 또는 벽을 감지
    /// </summary>
    public bool WallCheck(float directionX)
    {
        if (Mathf.Abs(directionX) <= Constants.FLOAT_CheckFloat)
        {
            return false;
        }

        float normalizedDirectionX = directionX > 0f ? 1f : -1f;
        return IsCliffAhead(normalizedDirectionX) || HasWallAhead(normalizedDirectionX);
    }

    /// <summary>
    /// 넉백 구현
    /// </summary>
    /// <param Name="force"></param>
    public void ApplyForce(Vector2 force)
    {
        _rb.linearVelocity = Vector2.zero;
        _rb.AddForce(force, ForceMode2D.Impulse);
    }

    /// <summary>
    /// 낭떠러지 감지
    /// </summary>
    /// <param name="directionX"></param>
    /// <returns></returns>
    private bool IsCliffAhead(float directionX)
    {
        int groundLayerMask = 1 << Constants.LAYER_Ground;
        Bounds bounds = _collider.bounds;
        float groundCheckDistance = bounds.extents.y + Constants.FLOAT_WallCheck;
        Vector2 rayOrigin = new Vector2(bounds.center.x + directionX * (bounds.extents.x + Constants.FLOAT_WallCheck), bounds.center.y);
        RaycastHit2D hit = Physics2D.Raycast(rayOrigin, Vector2.down, groundCheckDistance, groundLayerMask);
        return hit.collider == null;
    }

    /// <summary>
    /// 벽 감지
    /// </summary>
    /// <param name="directionX"></param>
    /// <returns></returns>
    private bool HasWallAhead(float directionX)
    {
        Bounds bounds = _collider.bounds;

        Vector2 rayOrigin = new Vector2(bounds.center.x + directionX * bounds.extents.x, bounds.center.y);
        Vector2 rayDirection = new Vector2(directionX, 0f);

        RaycastHit2D hit = Physics2D.Raycast(rayOrigin, rayDirection, Constants.FLOAT_WallCheck, _wallLayerMask);
        return hit.collider != null;
    }

    public void OnDie()
    {
        _rb.linearVelocity = Vector2.zero;
        _rb.bodyType = RigidbodyType2D.Kinematic;
    }

    /// <summary>
    /// 수평 이동 정지
    /// </summary>
    public void Stop()
    {
        _rb.linearVelocity = new Vector2(0f, _rb.linearVelocity.y);
    }
}
