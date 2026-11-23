using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 플레이어의 공격을 담당하는 컴포넌트
/// </summary>
public class PlayerAttack : MonoBehaviour
{
    private Player _player;
    private IAttackPowerProvider _attackPowerProvider;
    private readonly HashSet<MonsterBase> _hitMonsters = new();

    [Tooltip("데미지 배율 배열 (예: 1.0*2, 1.5*1)")]
    [SerializeField] private string _basicAttackDamageFormat = "1.0*2, 1.5*1";

    [Tooltip("타수별 최대 타겟수 배열 (예: 1*2, 5*1)")]
    [SerializeField] private string _basicAttackTargetsFormat = "1*2, 5*1";

    [Tooltip("타수별 범위 배열 (예: (1.4,0.9)*2, (2.4,1.2)*1)")]
    [SerializeField] private string _basicAttackRangesFormat = "(1.4,0.9)*2, (2.4,1.2)*1";

    [Tooltip("타수별 오프셋 배열 (예: (0.7,0.0)*2, (1.2,0.1)*1)")]
    [SerializeField] private string _basicAttackOffsetsFormat = "(0.7,0.0)*2, (1.2,0.1)*1";

    [Tooltip("몬스터 레이어 마스크 설정")]
    [SerializeField] private LayerMask _monsterLayer;

    private SkillData _testSkillData;

    // 실제 타격 순간의 월드 좌표와 판정 크기
    private Vector2 _lastAttackCenter;
    private Vector2 _lastAttackRange;

    public event Action OnAttackTrigger;

    /// <summary>
    /// 초기화
    /// </summary>
    public void Initialize(Player player, IAttackPowerProvider attackPowerProvider)
    {
        _player = player != null ? player : throw new ArgumentNullException(nameof(player));

        _attackPowerProvider = attackPowerProvider ?? throw new ArgumentNullException(nameof(attackPowerProvider));

        _lastAttackRange = Vector2.zero;

        // 테스트용 데이터 파싱
        _testSkillData = new SkillData(
            id: 999,
            name: "Test Basic Attack",
            targets: SkillParser.ParseIntArray(_basicAttackTargetsFormat),
            percents: SkillParser.ParseFloatArray(_basicAttackDamageFormat),
            ranges: SkillParser.ParseVector2Array(_basicAttackRangesFormat),
            offsets: SkillParser.ParseVector2Array(_basicAttackOffsetsFormat));
    }

    /// <summary>
    /// 행동 가능 상태 확인 후 기본 공격 시작
    /// 오프라인 플레이어 또는 서버 권한 경로에서 호출
    /// </summary>
    public bool TryBasicAttack()
    {
        if (_player.CheckIsStun())
        {
            return false;
        }

        // 이벤트 발행 전 공격 상태 확정으로 중복 실행 방지
        _player.IsAttacking = true;
        OnAttackTrigger?.Invoke();
        StartCoroutine(ExecuteSkill(_testSkillData));
        return true;
    }

    private IEnumerator ExecuteSkill(SkillData skillData)
    {
        int totalHits = skillData.DamagePercents.Length;
        int hitCount = 0;

        while (hitCount < totalHits)
        {
            // 데이터 추출
            float currentDamageMultiplier = skillData.DamagePercents[hitCount];
            int maxTargets = skillData.MaxTargets[hitCount];
            Vector2 range = skillData.AttackRanges[hitCount];
            Vector2 offset = skillData.AttackOffsets[hitCount];

            // 방향에 따른 오프셋 반전
            int direction = _player.GetPlayerSightDir() > 0 ? 1 : -1;

            Vector2 adjustedOffset = new Vector2(offset.x * direction, offset.y);
            Vector2 attackCenter = (Vector2)transform.position + adjustedOffset;

            // 이동·방향 전환 이후에도 실제 타격 영역 유지
            _lastAttackCenter = attackCenter;
            _lastAttackRange = range;

            // 물리 오버랩 캐스트 (지정된 레이어만 검출)
            Collider2D[] hitColliders = Physics2D.OverlapBoxAll(attackCenter, range, 0f, _monsterLayer);

            _hitMonsters.Clear();
            int currentHitTargets = 0;
            foreach (Collider2D hit in hitColliders)
            {
                if (currentHitTargets >= maxTargets) break;

                if (hit.isTrigger || !hit.TryGetComponent(out MonsterBase monster) || !_hitMonsters.Add(monster))
                {
                    continue;
                }

                float finalDamage = _attackPowerProvider.EffectiveAttack * currentDamageMultiplier;
                monster.TakeDamage(finalDamage, transform.position, _player);
                currentHitTargets++;
            }

            hitCount++;

            // 다단히트 간격 (추후 데이터로 뺄 수 있음)
            yield return new WaitForSeconds(0.1f);
        }
        _player.IsAttacking = false;
    }

    private void OnDrawGizmos()
    {
        if (_lastAttackRange == Vector2.zero) { return; }

        Gizmos.color = Color.red;
        Gizmos.DrawWireCube(_lastAttackCenter, _lastAttackRange);
    }

    /// <summary>
    /// 진행 중 기본 공격과 후속 타격 취소
    /// </summary>
    public void Cancel()
    {
        StopAllCoroutines();
        _hitMonsters.Clear();
        if (_player != null)
        {
            _player.IsAttacking = false;
        }
    }

    private void OnDisable() => Cancel();
}
