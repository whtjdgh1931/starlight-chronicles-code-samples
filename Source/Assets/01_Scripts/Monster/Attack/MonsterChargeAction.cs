using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 몬스터 돌진 이동과 충돌 피해 관리
/// </summary>
public sealed class MonsterChargeAction : MonoBehaviour
{
    [SerializeField] private BoxCollider2D _attackCollider;
    [SerializeField] private MonsterChargeEffect _chargeEffect;
    /// <summary>
    /// 돌진 충돌 범위의 월드 중심
    /// </summary>
    public Vector2 AttackCenter => _attackCollider.transform.TransformPoint(_attackCollider.offset);

    /// <summary>
    /// 돌진 충돌 범위의 월드 너비
    /// </summary>
    public float AttackWidth => _attackCollider.size.x * Mathf.Abs(_attackCollider.transform.lossyScale.x);

    /// <summary>
    /// 돌진 충돌 범위의 월드 높이
    /// </summary>
    public float AttackHeight => _attackCollider.size.y * Mathf.Abs(_attackCollider.transform.lossyScale.y);

    /// <summary>
    /// 서버 돌진 시작 방향과 종료 알림
    /// </summary>
    public event Action<float> ChargeStarted;
    public event Action ChargeStopped;

    private MonsterBase _monster;
    private KnockbackData _knockbackData;
    private float _directionX;
    private float _damage;
    private float _speed;
    private float _endTime;
    private bool _isCharging;
    private readonly HashSet<Player> _hitPlayers = new();

    /// <summary>
    /// 벽 충돌로 사용하지 못한 돌진 시간
    /// </summary>
    private float _remainingIdleDuration;
    public float RemainingIdleDuration => _remainingIdleDuration;
    public void Initialize(MonsterBase monster)
    {
        if (_attackCollider == null)
        {
            throw new MissingReferenceException($"{name}: 돌진 공격 BoxCollider2D 참조 필요");
        }

        if (_chargeEffect == null)
        {
            throw new MissingReferenceException($"{name}: MonsterChargeEffect 참조 필요");
        }

        _chargeEffect.Initialize(useBossStyle: monster is BossMonster);
        _monster = monster;
        Stop();
    }

    public void SetRemainingIdleDuration(float remainingIdleDuration)
    {
        _remainingIdleDuration = remainingIdleDuration;
    }

    public void Begin(float directionX, float damage, float speed, float duration, KnockbackData knockbackData)
    {
        _directionX = directionX;
        _damage = damage;
        _speed = speed;
        _endTime = Time.time + duration;
        _knockbackData = knockbackData;
        _hitPlayers.Clear();
        _remainingIdleDuration = 0f;
        _isCharging = true;
        _chargeEffect.Play(_directionX);
        _monster.SetContactDamageEnabled(false);
        _attackCollider.enabled = true;
        ChargeStarted?.Invoke(_directionX);
    }

    public bool PhysicsUpdate()
    {
        if (!_isCharging)
        {
            return false;
        }

        if (Time.time >= _endTime)
        {
            return true;
        }

        if (_monster.WallCheck(_directionX))
        {
            _remainingIdleDuration = Mathf.Max(0f, _endTime - Time.time);
            return true;
        }

        _monster.MoveAtSpeed(new Vector2(_directionX, 0f), _speed);
        return false;
    }

    /// <summary>
    /// 돌진 중 접촉한 플레이어별 단일 피해 적용
    /// </summary>
    private void TryApplyDamage(Collider2D collision)
    {
        if (!_isCharging || !collision.TryGetComponent(out Player player) || !_hitPlayers.Add(player))
        {
            return;
        }

        player.TakeDamage(_damage, _monster.transform.position, _knockbackData);
    }

    private void OnTriggerEnter2D(Collider2D collision)
    {
        TryApplyDamage(collision);
    }

    private void OnTriggerStay2D(Collider2D collision)
    {
        TryApplyDamage(collision);
    }

    /// <summary>
    /// 클라이언트 돌진 표시 초기화, AI·이동·피해 판정 제외
    /// </summary>
    public void InitializeReplicaPresentation()
    {
        _chargeEffect.Initialize(useBossStyle: true);
    }

    /// <summary>
    /// 서버 돌진 방향에 따른 잔상·먼지 재생, 0은 정지
    /// </summary>
    public void ApplyReplicaPresentation(int direction, bool emitLaunchDust = true)
    {
        if (direction == 0) { _chargeEffect.Stop(); }
        else { _chargeEffect.Play(direction, emitLaunchDust); }
    }

    public void Stop()
    {
        bool wasCharging = _isCharging;
        _isCharging = false;
        _hitPlayers.Clear();
        _attackCollider.enabled = false;
        _chargeEffect.Stop();
        if (wasCharging) { ChargeStopped?.Invoke(); }
        if (_monster == null)
        {
            return;
        }

        _monster.StopMovement();
        _monster.SetContactDamageEnabled(_monster.Data.ContactDamageEnabled);
    }
}
