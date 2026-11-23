using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlayerMove : MonoBehaviour, IPlayerForcedMovement
{
    /// <summary>
    /// 리지드바디
    /// </summary>
    [SerializeField] private Rigidbody2D _rb;
    public Rigidbody2D Rb => _rb;

    /// <summary>
    /// 플레이어 콜라이더
    /// </summary>
    [SerializeField] private Collider2D _playerCollider;
    public Collider2D PlayerCollider => _playerCollider;

    /// <summary>
    /// 넉백을 우선하는 이동 제어 상태
    /// </summary>
    private enum MovementControlState
    {
        Normal,
        ForcedMovement,
        Knockback
    }

    private MovementControlState _movementControlState;
    public bool IsKnockback => _movementControlState == MovementControlState.Knockback;
    public bool IsForcedMoving => _movementControlState == MovementControlState.ForcedMovement;

    public event Action<Vector2> OnMoved;

    [SerializeField] private LayerMask _groundLayerMask = 1 << Constants.LAYER_Ground;
    [SerializeField] private LayerMask _wallLayerMask = 1 << Constants.LAYER_Wall;
    [SerializeField] private bool _isGrounded;
    public bool IsGrounded => _isGrounded && !_isDownJumping;

    #region move
    [Header("Move Setting")]

    /// <summary>
    /// 이동 방향 벡터
    /// </summary>
    [SerializeField] private Vector2 _moveVector;
    public Vector2 MoveVector { get { return _moveVector; } set { _moveVector = value; } }

    [SerializeField] private float _verticalDirection;
    public float VerticalDirection { get { return _verticalDirection; } set { _verticalDirection = value; } }



    /// <summary>
    /// 바라보는 방향(오른쪽을 바라보는지) 1이면 오른쪽, -1이면 왼쪽
    /// </summary>
    [SerializeField] private int _sightDir;
    public int SightDirection => _sightDir;

    /// <summary>
    /// 이동속도
    /// </summary>
    [SerializeField] private float _moveSpeed;

    [SerializeField, Min(0f)] private float _groundAcceleration = 70f;
    [SerializeField, Min(0f)] private float _groundDeceleration = 95f;
    #endregion

    #region jump
    [Header("Jump Setting")]

    // 조작감 튜닝 중 시트 데이터보다 Inspector 점프 설정 우선
    [SerializeField] private bool _useLocalJumpSettings;

    [SerializeField, Range(0.1f, 1.5f), Tooltip("일반·윗점프와 플래시 점프의 상승 힘 배율")]
    private float _jumpUpwardMultiplier = 0.8f;

    /// <summary>
    /// 최대 점프 회수
    /// </summary>
    [SerializeField] private int _jumpCnt;

    /// <summary>
    /// 현재 점프 카운트
    /// </summary>
    [SerializeField] private int _currentJumpCnt;

    /// <summary>
    /// 일반 점프 높이
    /// </summary>
    [SerializeField] private float _normalJumpPower;

    /// <summary>
    /// 위 점프 파워
    /// </summary>
    [SerializeField] private float _highJumpPower;

    /// <summary>
    /// 더블 점프 정면 파워
    /// </summary>
    [SerializeField] private float _doubleJumpForwardPower;

    /// <summary>
    /// 더블 점프 위 파워
    /// </summary>
    [SerializeField] private float _doubleJumpUpPower;

    /// <summary>
    /// 공중에서 이동 방향으로 속도를 높이는 가속도
    /// </summary>
    [SerializeField, Min(0f)] private float _airAcceleration = 9f;

    /// <summary>
    /// 반대 방향 입력 시 기존 궤도를 미세하게 보정하는 가속도
    /// </summary>
    [SerializeField, Min(0f)] private float _airTurnAcceleration = 0.1f;

    /// <summary>
    /// 입력 해제와 초과 속도에 적용할 완만한 감속도
    /// </summary>
    [SerializeField, Min(0f)] private float _airDeceleration = 7.5f;

    /// <summary>
    /// 밑점 중인지
    /// </summary>
    [SerializeField] private bool _isDownJumping;
    private Collider2D _currentGroundCollider;
    private Collider2D _ignoredPlatformCollider;

    /// <summary>
    /// 밑점프 시작 속도
    /// </summary>
    [SerializeField] private float _downJumpPower;
    [SerializeField, Min(0f)] private float _maxDownJumpDistance = 5f;

    [SerializeField, Min(0.01f)] private float _groundCheckDistance = 0.05f;
    [SerializeField, Range(0f, 89f)] private float _maxGroundAngle = 60f;

    private readonly List<RaycastHit2D> _groundHits = new();
    private ContactFilter2D _groundContactFilter;

    private float _currentGroundSurfaceY;
    private float _ignoredPlatformSurfaceY;
    #endregion

    /// <summary>
    /// 초기화용 메서드
    /// </summary>
    public void Initialize(PlayerMoveData moveData)
    {
        CheckComponent();
        InitializeGroundCheck();

        if (moveData == null)
        {
            throw new ArgumentNullException(nameof(moveData));
        }

        // 시트 바인딩 전 로컬 점프 설정 보관
        Vector3 localJumpPowers = new Vector3(_normalJumpPower, _highJumpPower, _doubleJumpUpPower);
        DataBinder.Bind(moveData, this);

        if (_useLocalJumpSettings)
        {
            _normalJumpPower = localJumpPowers.x;
            _highJumpPower = localJumpPowers.y;
            _doubleJumpUpPower = localJumpPowers.z;
        }

        // 방향 입력 전 플래시 점프는 오른쪽으로 실행
        if (_sightDir == 0)
        {
            _sightDir = 1;
        }

    }

    private void CheckComponent()
    {
        if (_rb == null || _playerCollider == null)
        {
            throw new MissingReferenceException($"{name}: Rigidbody2D와 플레이어 Collider2D 참조 필요");
        }
    }

    private void InitializeGroundCheck()
    {
        _groundContactFilter = new ContactFilter2D();
        _groundContactFilter.SetLayerMask(_groundLayerMask);
        _groundContactFilter.useTriggers = false;
    }

    /// <summary>
    /// 접지·시선·속도 갱신
    /// 싱글은 자체 FixedUpdate, 온라인은 서버 컨트롤러에서 틱당 한 번 호출
    /// </summary>
    public void SimulateMovement()
    {
        CheckGround();

        // 착지 후 현재 입력 방향 반영과 공중 시선 유지
        if (IsGrounded && !IsForcedMoving && Mathf.Abs(_moveVector.x) > Constants.FLOAT_CheckFloat)
        {
            int direction = _moveVector.x > 0f ? 1 : -1;
            if (_sightDir != direction)
            {
                _sightDir = direction;
                OnMoved?.Invoke(new Vector2(_sightDir, 0f));
            }
        }

        if (_movementControlState == MovementControlState.Normal)
        {
            Move(_moveVector);
        }

        if (_rb.linearVelocity.y < Constants.FLOAT_MaxFallSpeed)
        {
            _rb.linearVelocity = new Vector2(_rb.linearVelocity.x, Constants.FLOAT_MaxFallSpeed);
        }
    }

    /// <summary>
    /// 일반 이동 상태에서만 강제 이동 시작
    /// </summary>
    public bool TryBeginForcedMovement()
    {
        if (_movementControlState != MovementControlState.Normal)
        {
            return false;
        }

        _movementControlState = MovementControlState.ForcedMovement;
        return true;
    }

    /// <summary>
    /// 강제 이동 중 지정된 수평 속도 적용
    /// </summary>
    public void MoveAtSpeed(float directionX, float speed)
    {
        if (!IsForcedMoving)
        {
            throw new InvalidOperationException($"{name}: 강제 이동 시작 전에 속도를 적용할 수 없음");
        }

        float normalizedDirectionX = directionX > 0f ? 1f : -1f;
        _rb.linearVelocity = new Vector2(normalizedDirectionX * speed, _rb.linearVelocity.y);
    }

    /// <summary>
    /// 바닥과 분리된 벽 레이어의 진행 방향 충돌 감지
    /// </summary>
    public bool HasWallAhead(float directionX)
    {
        if (Mathf.Abs(directionX) <= Constants.FLOAT_CheckFloat)
        {
            return false;
        }

        float normalizedDirectionX = directionX > 0f ? 1f : -1f;
        Bounds bounds = _playerCollider.bounds;
        Vector2 rayOrigin = new Vector2(bounds.center.x + normalizedDirectionX * bounds.extents.x, bounds.center.y);
        Vector2 rayDirection = new Vector2(normalizedDirectionX, 0f);
        RaycastHit2D hit = Physics2D.Raycast(rayOrigin, rayDirection, Constants.FLOAT_WallCheck, _wallLayerMask);

        return hit.collider != null;
    }

    /// <summary>
    /// 넉백 시작
    /// </summary>
    /// <param name="force"></param>
    public void StartKnockback(Vector2 force)
    {
        _movementControlState = MovementControlState.Knockback;
        _rb.linearVelocity = Vector2.zero;
        _rb.AddForce(force, ForceMode2D.Impulse);
    }

    public void InputMoveVector(Vector2 moveVector)
    {
        //좌우 이동 입력 세팅
        if (Mathf.Abs(moveVector.x) > Constants.FLOAT_CheckFloat)
        {
            //좌우 이동 
            _moveVector.x = moveVector.x > 0 ? 1f : -1f;

        }
        else
        {
            //입력 없으면 정지
            _moveVector.x = 0f;
        }

        //상하 입력 세팅
        if (Mathf.Abs(moveVector.y) > Constants.FLOAT_CheckFloat)
        {
            _verticalDirection = moveVector.y > 0f ? 1f : -1f;
        }
        else
        {
            _verticalDirection = 0f;
        }

    }

    /// <summary>
    /// 지상 이동과 관성을 유지하는 공중 수평 제어
    /// </summary>
    public void Move(Vector2 moveVector)
    {
        float inputX = Mathf.Clamp(moveVector.x, -1f, 1f);
        float targetSpeed = inputX * _moveSpeed;

        if (IsGrounded)
        {
            float groundAcceleration = Mathf.Abs(inputX) > Constants.FLOAT_CheckFloat ? _groundAcceleration : _groundDeceleration;
            float groundSpeed = Mathf.MoveTowards(_rb.linearVelocityX, targetSpeed, groundAcceleration * Time.fixedDeltaTime);
            _rb.linearVelocity = new Vector2(groundSpeed, _rb.linearVelocityY);
            return;
        }

        float currentSpeed = _rb.linearVelocityX;
        float acceleration;

        if (Mathf.Abs(inputX) <= Constants.FLOAT_CheckFloat)
        {
            // 입력 해제 시 관성을 남기면서 완만하게 감속
            targetSpeed = 0f;
            acceleration = _airDeceleration;
        }
        else if (currentSpeed * inputX < 0f)
        {
            // 반대 방향 입력에도 기존 점프 궤도를 대부분 유지
            acceleration = _airTurnAcceleration;
        }
        else if (Mathf.Abs(currentSpeed) > Mathf.Abs(targetSpeed))
        {
            // 플래시 점프 등의 초과 속도를 급격히 깎지 않는 감속
            acceleration = _airDeceleration;
        }
        else
        {
            acceleration = _airAcceleration;
        }

        float nextSpeed = Mathf.MoveTowards(
            currentSpeed, targetSpeed, acceleration * Time.fixedDeltaTime);

        _rb.linearVelocity = new Vector2(nextSpeed, _rb.linearVelocityY);
    }

    /// <summary>
    /// 접지 상태에서는 일반 점프, 공중에서는 플래시 점프 실행
    /// </summary>
    public void Jump()
    {
        if (IsGrounded)
        {
            if (_verticalDirection < -0.5f)
            {
                if (!CanDownJump())
                {
                    return;
                }

                StartCoroutine(DownJumpCoroutine());
                _currentJumpCnt = 1;
                return;
            }

            float jumpPower = _verticalDirection > Constants.FLOAT_CheckFloat
                ? _highJumpPower
                : _normalJumpPower;

            _rb.linearVelocity = new Vector2(_rb.linearVelocityX, 0f);
            _rb.AddForce(Vector2.up * (jumpPower * _jumpUpwardMultiplier), ForceMode2D.Impulse);

            _currentJumpCnt = 1;
            return;
        }

        if (_currentJumpCnt >= _jumpCnt)
        {
            return;
        }

        FlashJump();
    }

    /// <summary>
    /// 텔레포트나 강제 이동 후 위치 동기화
    /// </summary>
    /// <param Name="syncVector"></param>
    public void SyncPosition(Vector2 syncVector)
    {
        _rb.position = syncVector;
    }

    /// <summary>
    /// 부활 시 남은 이동 속도와 회전 속도 초기화
    /// </summary>
    public void ResetVelocity()
    {
        _rb.linearVelocity = Vector2.zero;
        _rb.angularVelocity = 0f;
    }

    private void FixedUpdate() => SimulateMovement();

    /// <summary>
    /// 공중에서 남은 점프를 소비하고 전방으로 도약
    /// </summary>
    private void FlashJump()
    {
        // 낙하 속도를 유지하여 플래시 점프의 과도한 재상승 방지
        _rb.linearVelocity = new Vector2(0f, Mathf.Min(_rb.linearVelocityY, 0f));

        Vector2 flashJumpForce = new Vector2(
            _sightDir * _doubleJumpForwardPower,
            _doubleJumpUpPower * _jumpUpwardMultiplier);

        _rb.AddForce(flashJumpForce, ForceMode2D.Impulse);

        // 낙하 중 사용해도 추가 플래시 점프가 발생하지 않도록 모두 소비
        _currentJumpCnt = _jumpCnt;
    }

    /// <summary>
    /// 제한 거리 내 다음 발판 존재 여부 확인
    /// </summary>
    private bool CanDownJump()
    {
        if (_isDownJumping || _currentGroundCollider == null ||
            !_currentGroundCollider.TryGetComponent(out PlatformEffector2D _))
        {
            return false;
        }

        _groundHits.Clear();
        int hitCount = _playerCollider.Cast(Vector2.down, _groundContactFilter, _groundHits, _maxDownJumpDistance);
        float minimumGroundNormalY = Mathf.Cos(_maxGroundAngle * Mathf.Deg2Rad);

        for (int i = 0; i < hitCount; i++)
        {
            RaycastHit2D hit = _groundHits[i];

            if (hit.collider == _currentGroundCollider || hit.normal.y < minimumGroundNormalY)
            {
                continue;
            }

            float dropDistance = _currentGroundSurfaceY - hit.point.y;

            if (dropDistance > _groundCheckDistance && dropDistance <= _maxDownJumpDistance)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// 밑점 로직
    /// </summary>
    /// <returns></returns>
    private IEnumerator DownJumpCoroutine()
    {
        if (_isDownJumping || _currentGroundCollider == null)
        {
            yield break;
        }

        if (!_currentGroundCollider.TryGetComponent(out PlatformEffector2D _))
        {
            yield break;
        }

        _ignoredPlatformCollider = _currentGroundCollider;
        _ignoredPlatformSurfaceY = _currentGroundSurfaceY;
        _isDownJumping = true;
        _isGrounded = false;

        Physics2D.IgnoreCollision(_playerCollider, _ignoredPlatformCollider, true);
        _rb.linearVelocity = new Vector2(_rb.linearVelocityX, -_downJumpPower);

        // 플레이어가 현재 발판 아래로 완전히 내려갈 때까지 충돌을 유지하지 않음
        while (_playerCollider.bounds.max.y >= _ignoredPlatformSurfaceY - _groundCheckDistance)
        {
            yield return new WaitForFixedUpdate();
        }

        Physics2D.IgnoreCollision(_playerCollider, _ignoredPlatformCollider, false);

        _ignoredPlatformCollider = null;
        RestoreIgnoredPlatformCollision();
        _currentGroundCollider = null;
        _isDownJumping = false;
    }

    private void RestoreIgnoredPlatformCollision()
    {
        if (_ignoredPlatformCollider == null)
        {
            return;
        }

        Physics2D.IgnoreCollision(_playerCollider, _ignoredPlatformCollider, false);
        _ignoredPlatformCollider = null;
        _isDownJumping = false;
    }

    /// <summary>
    /// 플레이어 Collider 아래의 이동 가능한 지면을 검사
    /// </summary>
    private void CheckGround()
    {
        if (_rb.linearVelocityY > Constants.FLOAT_CheckFloat || _isDownJumping)
        {
            ClearGroundState();
            return;
        }

        _groundHits.Clear();

        int hitCount = _playerCollider.Cast(
            Vector2.down,
            _groundContactFilter,
            _groundHits,
            _groundCheckDistance);

        float minimumGroundNormalY = Mathf.Cos(_maxGroundAngle * Mathf.Deg2Rad);

        for (int i = 0; i < hitCount; i++)
        {
            RaycastHit2D hit = _groundHits[i];

            if (hit.normal.y < minimumGroundNormalY)
            {
                continue;
            }

            _isGrounded = true;
            _currentJumpCnt = 0;
            _currentGroundCollider = hit.collider;
            _currentGroundSurfaceY = hit.point.y;
            return;
        }

        ClearGroundState();
    }

    /// <summary>
    /// 강제 이동 종료와 수평 속도 초기화
    /// </summary>
    public void EndForcedMovement()
    {
        // 종료된 돌진이 이후 시작한 넉백을 덮어쓰지 않도록 제한
        if (!IsForcedMoving)
        {
            return;
        }

        _movementControlState = MovementControlState.Normal;
        _rb.linearVelocity = new Vector2(0f, _rb.linearVelocity.y);
    }

    /// <summary>
    /// 넉백 끝
    /// </summary>
    public void EndKnockback()
    {
        if (IsKnockback)
        {
            _movementControlState = MovementControlState.Normal;
        }
    }

    private void ClearGroundState()
    {
        _isGrounded = false;
        _currentGroundCollider = null;
        _currentGroundSurfaceY = 0f;
    }

    /// <summary>
    /// 온라인 캐릭터 재사용 전 입력·접지·점프·물리 상태 초기화
    /// 이동 수치와 지형 설정 유지
    /// </summary>
    public void ResetNetworkMovement()
    {
        CancelTransientMovement();
        _moveVector = Vector2.zero;
        _verticalDirection = 0f;
        _isGrounded = false;
        _currentJumpCnt = 0;
        _sightDir = 1;
        _rb.linearVelocity = Vector2.zero;
        _rb.angularVelocity = 0f;
    }

    private void OnDisable() => CancelTransientMovement();

    /// <summary>
    /// 씬 전환·네트워크 제거 시 하강 점프와 임시 충돌 무시 해제
    /// 자체 FixedUpdate를 비활성화한 서버에서도 명시적 정리
    /// </summary>
    public void CancelTransientMovement()
    {
        StopAllCoroutines();
        if (_ignoredPlatformCollider != null)
        {
            Physics2D.IgnoreCollision(_playerCollider, _ignoredPlatformCollider, false);
            _ignoredPlatformCollider = null;
        }
        RestoreIgnoredPlatformCollision();
        _isDownJumping = false;
        _movementControlState = MovementControlState.Normal;
        _rb.linearVelocity = new Vector2(0f, _rb.linearVelocity.y);
    }

}
