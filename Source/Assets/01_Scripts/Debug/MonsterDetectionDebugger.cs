using UnityEngine;

/// <summary>
/// 선택한 몬스터의 탐지 범위와 실제 상태 표시
/// </summary>
[DisallowMultipleComponent]
public sealed class MonsterDetectionDebugger : MonoBehaviour
{
    [SerializeField] private MonsterBase _monster;
    [SerializeField] private MonsterFSM _monsterFSM;
    [SerializeField] private MonsterTargetDetector _targetDetector;
    [SerializeField] private bool _showDetection = true;
    [SerializeField] private Color _detectionColor = Color.green;
    [SerializeField] private Color _trackingColor = Color.yellow;
    [SerializeField] private Color _targetColor = Color.cyan;
    [SerializeField, Min(0f)] private float _labelOffset = 0.5f;

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        if (!enabled || !_showDetection)
        {
            return;
        }

        if (_monster == null || _monsterFSM == null || _targetDetector == null)
        {
            UnityEditor.Handles.Label(transform.position, "탐지 표시 필수 참조 누락");
            return;
        }

        if (!_targetDetector.TryGetDetectionBounds(out Bounds detectionBounds, out Bounds trackingBounds))
        {
            UnityEditor.Handles.Label(transform.position, "탐지 데이터 초기화 전");
            return;
        }

        Color previousColor = Gizmos.color;

        try
        {
            Gizmos.color = _detectionColor;
            Gizmos.DrawWireCube(detectionBounds.center, detectionBounds.size);

            Gizmos.color = _trackingColor;
            Gizmos.DrawWireCube(trackingBounds.center, trackingBounds.size);

            string targetName = "없음";

            if (_monster.TryGetTarget(out Player target))
            {
                targetName = target.name;
                Gizmos.color = _targetColor;
                Gizmos.DrawLine(detectionBounds.center, target.transform.position);
            }

            string stateName = GetStateName(_monsterFSM.CurrentState);
            string aggression = _monster.IsAggressive ? "켜짐" : "꺼짐";
            string simulation = _monsterFSM.enabled ? "활성" : "정지";
            Vector3 labelPosition = new Vector3(detectionBounds.center.x, detectionBounds.max.y + _labelOffset, detectionBounds.center.z);

            UnityEditor.Handles.Label(labelPosition, $"상태: {stateName} / AI: {simulation}\n선공: {aggression} / 타깃: {targetName}");
        }
        finally
        {
            Gizmos.color = previousColor;
        }
    }

    private static string GetStateName(MonsterState state)
    {
        return state switch
        {
            MonsterIdleState => "대기",
            MonsterPatrolState => "순찰",
            MonsterChaseState => "추적",
            MonsterAttackState => "공격",
            MonsterHitState => "피격",
            MonsterDieState => "사망",
            null => "초기화 전",
            _ => state.GetType().Name
        };
    }
#endif
}
